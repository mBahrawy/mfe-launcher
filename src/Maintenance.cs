using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace MfeLauncher
{
    /// <summary>Cache clean-ups behind the header's ⋯ menu. Each throws with the command's output on failure.</summary>
    static class Maintenance
    {
        const int OutputTailLines = 15;

        /// <summary>`nx reset` (daemon, .nx cache and workspace data) plus .angular/cache in every workspace.</summary>
        public static string ResetNx(IEnumerable<MfeApp> apps)
        {
            var notes = new List<string>();
            foreach (var workspace in apps.GroupBy(a => a.WorkspaceDir, StringComparer.OrdinalIgnoreCase))
            {
                var dir = workspace.Key;
                var env = new Dictionary<string, string> { ["NX_WORKSPACE_ROOT_PATH"] = dir };
                var result = Shell.Run("node", $"\"{workspace.First().NxBin}\" reset", dir, env);
                if (result.ExitCode != 0) throw new InvalidOperationException($"nx reset failed in {Path.GetFileName(dir)}:\n\n{Tail(result.Output)}");

                notes.Add(Path.GetFileName(dir) + (TryDelete(Path.Combine(dir, ".angular", "cache")) ? "" : " (.angular/cache partly in use)"));
            }
            return "Nx reset, caches cleared: " + string.Join(", ", notes);
        }

        public static string CleanNpmCache(string root)
        {
            var result = Shell.Run("cmd.exe", "/d /c npm cache clean --force", root);
            if (result.ExitCode != 0) throw new InvalidOperationException($"npm cache clean failed:\n\n{Tail(result.Output)}");
            return "npm cache cleaned.";
        }

        static bool TryDelete(string dir)
        {
            if (!Directory.Exists(dir)) return true;
            try
            {
                Directory.Delete(dir, recursive: true);
                return true;
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                return false;
            }
        }

        static string Tail(string output) =>
            string.Join(Environment.NewLine, output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).Reverse().Take(OutputTailLines).Reverse());
    }
}
