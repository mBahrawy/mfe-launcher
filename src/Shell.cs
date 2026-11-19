using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace MfeLauncher
{
    static class Shell
    {
        /// <summary>Runs a hidden console command to completion and returns its exit code with stdout and stderr combined.</summary>
        public static (int ExitCode, string Output) Run(string file, string args, string workingDir = null, IDictionary<string, string> env = null)
        {
            var startInfo = new ProcessStartInfo(file, args)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };
            if (workingDir != null) startInfo.WorkingDirectory = workingDir;
            if (env != null) foreach (var pair in env) startInfo.EnvironmentVariables[pair.Key] = pair.Value;

            var output = new StringBuilder();
            void Collect(object _, DataReceivedEventArgs e)
            {
                if (e.Data == null) return;
                lock (output) output.AppendLine(e.Data);
            }

            using (var process = new Process { StartInfo = startInfo })
            {
                process.OutputDataReceived += Collect;
                process.ErrorDataReceived += Collect;
                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
                process.WaitForExit();
                return (process.ExitCode, output.ToString());
            }
        }
    }
}
