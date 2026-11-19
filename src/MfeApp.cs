using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace MfeLauncher
{
    enum AppStatus { Stopped, Starting, Running, External, Error, Crashed, Stopping }

    sealed class MfeApp
    {
        const int MaxLogLines = 5000;
        static readonly TimeSpan PortReleaseTimeout = TimeSpan.FromSeconds(20);
        static readonly Regex AnsiCodes = new Regex(@"\x1B\[[0-?]*[ -/]*[@-~]");
        static readonly Regex ErrorLine = new Regex(
            @"\[ERROR\]|bundle generation failed|already in use|^\s*(Error|error|ERR!)[:\s]|Cannot find (project|module)",
            RegexOptions.Compiled);
        static readonly Regex BuildSucceeded = new Regex(@"bundle generation complete|compiled successfully", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        static readonly Regex ErrorPrefix = new Regex(@"^\W*\[ERROR\]\s*|^\s*NX\s+", RegexOptions.Compiled);

        readonly Queue<string> log = new Queue<string>();
        Process process;
        volatile bool stopping;
        volatile bool crashed;

        public MfeApp(string name, string workspaceDir, string nxBin, int port)
        {
            Name = name;
            WorkspaceDir = workspaceDir;
            NxBin = nxBin;
            Port = port;
        }

        public string Name { get; }
        public string WorkspaceDir { get; }
        public string Workspace => Path.GetFileName(WorkspaceDir);
        public string NxBin { get; }
        public int Port { get; }
        public bool IsOwnAlive => process is Process p && !p.HasExited;

        /// <summary>The first error of the current failed build, or why the server stopped; null when healthy.</summary>
        public string LastError { get; private set; }

        /// <summary>Raised on a background thread.</summary>
        public event Action<string> LineAdded;

        /// <summary>Raised on a background thread once per failed build or unexpected exit, with a one-line reason.</summary>
        public event Action<MfeApp, string> Failed;

        /// <summary>The PID holding this app's port when it is not a server this launcher started.</summary>
        public int? ForeignPortOwner(IDictionary<int, int> listeners) =>
            !IsOwnAlive && Port > 0 && listeners.TryGetValue(Port, out var pid) ? pid : (int?)null;

        public AppStatus GetStatus(IDictionary<int, int> listeners)
        {
            if (stopping) return AppStatus.Stopping;

            var listening = Port > 0 && listeners.ContainsKey(Port);
            if (IsOwnAlive && LastError != null) return AppStatus.Error;
            if (IsOwnAlive) return listening ? AppStatus.Running : AppStatus.Starting;
            if (crashed) return AppStatus.Crashed;
            return listening ? AppStatus.External : AppStatus.Stopped;
        }

        public string[] LogSnapshot()
        {
            lock (log) return log.ToArray();
        }

        public void ClearLog()
        {
            lock (log) log.Clear();
        }

        public void Start(ProcessJob job)
        {
            if (IsOwnAlive) return;
            crashed = false;
            LastError = null;

            var startInfo = new ProcessStartInfo("node", $"\"{NxBin}\" serve {Name}")
            {
                WorkingDirectory = WorkspaceDir,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };
            startInfo.EnvironmentVariables["NX_WORKSPACE_ROOT_PATH"] = WorkspaceDir;
            startInfo.EnvironmentVariables["FORCE_COLOR"] = "0";

            var started = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
            started.OutputDataReceived += (_, e) => AddLine(e.Data);
            started.ErrorDataReceived += (_, e) => AddLine(e.Data);
            started.Exited += (_, _) => OnExited(started);

            AddLine($"> node nx serve {Name}   ({WorkspaceDir})");
            try
            {
                started.Start();
            }
            catch (Win32Exception ex)
            {
                AddLine($"[launcher] could not start node: {ex.Message}");
                crashed = true;
                ReportFailure($"Could not start node: {ex.Message}");
                return;
            }
            job.Add(started);
            started.BeginOutputReadLine();
            started.BeginErrorReadLine();
            process = started;
        }

        public Task StopAsync() => Task.Run(Stop);

        /// <summary>Stops whatever serves this app's port (ours or not), then starts it here.</summary>
        public Task RestartAsync(ProcessJob job) => Task.Run(() =>
        {
            Stop();
            Start(job);
        });

        void Stop()
        {
            var pid = IsOwnAlive ? process.Id : ForeignPortOwner(Ports.ListeningOwners());
            if (pid == null) return;

            stopping = true;
            try
            {
                AddLine($"[launcher] stopping process tree {pid}");
                Shell.Run("taskkill", $"/PID {pid} /T /F");
                WaitForPortRelease();
            }
            finally
            {
                process = null;
                crashed = false;
                stopping = false;
            }
        }

        void OnExited(Process exited)
        {
            AddLine($"[launcher] process exited with code {exited.ExitCode}");
            if (stopping || exited != process) return;

            crashed = true;
            var reason = $"Server stopped (exit code {exited.ExitCode})";
            ReportFailure(LastError == null ? reason : $"{reason}: {LastError}");
        }

        void TrackBuildState(string line)
        {
            if (BuildSucceeded.IsMatch(line))
            {
                LastError = null;
                return;
            }
            if (LastError != null || !ErrorLine.IsMatch(line)) return;

            ReportFailure(ErrorPrefix.Replace(line, "").Trim());
        }

        void ReportFailure(string reason)
        {
            LastError = reason;
            Failed?.Invoke(this, reason);
        }

        void WaitForPortRelease()
        {
            var deadline = DateTime.UtcNow + PortReleaseTimeout;
            while (Port > 0 && Ports.ListeningOwners().ContainsKey(Port) && DateTime.UtcNow < deadline)
            {
                Thread.Sleep(250);
            }
        }

        void AddLine(string line)
        {
            if (line == null) return;
            line = AnsiCodes.Replace(line, "");
            lock (log)
            {
                log.Enqueue(line);
                while (log.Count > MaxLogLines) log.Dequeue();
            }
            if (!line.StartsWith("[launcher]")) TrackBuildState(line);
            LineAdded?.Invoke(line);
        }
    }
}
