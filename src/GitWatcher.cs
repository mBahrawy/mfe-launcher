using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace MfeLauncher
{
    /// <summary>Raises Changed when git moves a ref (pull, merge, checkout, submodule update) in any of the given work trees.</summary>
    sealed class GitWatcher : IDisposable
    {
        static readonly HashSet<string> RefFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "HEAD", "ORIG_HEAD", "FETCH_HEAD", "packed-refs"
        };

        readonly List<FileSystemWatcher> watchers = new List<FileSystemWatcher>();

        public GitWatcher(IEnumerable<string> workTrees)
        {
            var gitDirs = workTrees.Select(GitDir).Where(dir => dir != null).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

            // A submodule's git dir lives under the superproject's .git/modules, so one recursive watcher covers both.
            foreach (var dir in gitDirs.Where(dir => !gitDirs.Any(other => IsInside(dir, other))))
            {
                var watcher = new FileSystemWatcher(dir)
                {
                    IncludeSubdirectories = true,
                    InternalBufferSize = 64 * 1024,
                    NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite
                };
                watcher.Changed += OnFileEvent;
                watcher.Created += OnFileEvent;
                watcher.Renamed += OnFileEvent;
                watcher.Error += (_, _) => Changed?.Invoke();
                watcher.EnableRaisingEvents = true;
                watchers.Add(watcher);
            }
        }

        /// <summary>Raised on a background thread, possibly many times per git command.</summary>
        public event Action Changed;

        public bool IsWatching => watchers.Count > 0;

        public void Dispose() => watchers.ForEach(w => w.Dispose());

        void OnFileEvent(object sender, FileSystemEventArgs e)
        {
            var name = Path.GetFileName(e.FullPath);
            var isBranchRef = e.FullPath.IndexOf(@"\refs\heads\", StringComparison.OrdinalIgnoreCase) >= 0
                && !name.EndsWith(".lock", StringComparison.OrdinalIgnoreCase);

            if (RefFiles.Contains(name) || isBranchRef) Changed?.Invoke();
        }

        /// <summary>The work tree's git dir: a ".git" folder, or the folder a ".git" file points to (submodules, worktrees).</summary>
        static string GitDir(string workTree)
        {
            var dotGit = Path.Combine(workTree, ".git");
            if (Directory.Exists(dotGit)) return Path.GetFullPath(dotGit);
            if (!File.Exists(dotGit)) return null;

            const string prefix = "gitdir:";
            var line = File.ReadAllLines(dotGit).FirstOrDefault(l => l.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
            if (line == null) return null;

            var target = Path.GetFullPath(Path.Combine(workTree, line.Substring(prefix.Length).Trim()));
            return Directory.Exists(target) ? target : null;
        }

        static bool IsInside(string dir, string other) =>
            !string.Equals(dir, other, StringComparison.OrdinalIgnoreCase)
            && dir.StartsWith(other.TrimEnd('\\') + "\\", StringComparison.OrdinalIgnoreCase);
    }
}
