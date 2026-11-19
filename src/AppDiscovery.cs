using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web.Script.Serialization;

namespace MfeLauncher
{
    static class AppDiscovery
    {
        const string NxPackagePath = @"node_modules\nx";

        /// <summary>Nx apps with a serve target in the root workspace and any direct child workspace (e.g. a submodule).</summary>
        public static List<MfeApp> Discover(string root)
        {
            var workspaces = new[] { root }
                .Concat(Directory.GetDirectories(root))
                .Where(dir => File.Exists(Path.Combine(dir, "nx.json")));

            return workspaces
                .SelectMany(DiscoverWorkspace)
                .OrderBy(app => app.Port == 0 ? int.MaxValue : app.Port)
                .ThenBy(app => app.Name)
                .ToList();
        }

        /// <summary>Nx fails to match its own files when the workspace path differs in casing (e.g. "d:" vs "D:").</summary>
        public static string RealCase(string path)
        {
            var driveRoot = Path.GetPathRoot(Path.GetFullPath(path));
            var full = Path.GetFullPath(path).TrimEnd('\\', '/');
            if (full.Length <= driveRoot.TrimEnd('\\').Length) return driveRoot.ToUpperInvariant();

            var dir = new DirectoryInfo(full);
            var onDisk = dir.Parent.GetDirectories(dir.Name).FirstOrDefault();
            return Path.Combine(RealCase(dir.Parent.FullName), onDisk?.Name ?? dir.Name);
        }

        static IEnumerable<MfeApp> DiscoverWorkspace(string workspaceDir)
        {
            var appsDir = Path.Combine(workspaceDir, "apps");
            var nxBin = FindNxBin(workspaceDir);
            if (!Directory.Exists(appsDir) || nxBin == null) yield break;

            var json = new JavaScriptSerializer();
            foreach (var projectFile in Directory.GetDirectories(appsDir).Select(d => Path.Combine(d, "project.json")))
            {
                if (!File.Exists(projectFile)) continue;

                var project = json.DeserializeObject(File.ReadAllText(projectFile)) as Dictionary<string, object>;
                var targets = Get(project, "targets");
                if (Get(targets, "serve") == null) continue;

                var name = Get(project, "name") as string ?? Path.GetFileName(Path.GetDirectoryName(projectFile));
                yield return new MfeApp(name, workspaceDir, nxBin, ServePort(targets));
            }
        }

        static int ServePort(object targets)
        {
            foreach (var target in new[] { "serve-original", "serve" })
            {
                if (Get(targets, target, "options", "port") is int port && port > 0) return port;
            }
            return 0;
        }

        /// <summary>Nearest installed nx, resolved through its package.json "bin" (the path moved between nx versions).</summary>
        static string FindNxBin(string fromDir)
        {
            for (var dir = new DirectoryInfo(fromDir); dir != null; dir = dir.Parent)
            {
                var packageDir = Path.Combine(dir.FullName, NxPackagePath);
                var packageJson = Path.Combine(packageDir, "package.json");
                if (!File.Exists(packageJson)) continue;

                var package = new JavaScriptSerializer().DeserializeObject(File.ReadAllText(packageJson));
                var bin = Get(package, "bin", "nx") as string ?? Get(package, "bin") as string;
                if (bin != null) return Path.GetFullPath(Path.Combine(packageDir, bin));
            }
            return null;
        }

        static object Get(object node, params string[] keys)
        {
            foreach (var key in keys)
            {
                if (!(node is Dictionary<string, object> dict) || !dict.TryGetValue(key, out node)) return null;
            }
            return node;
        }
    }
}
