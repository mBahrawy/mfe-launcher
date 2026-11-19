# How it works

MFE Launcher is a C# **WinForms** app that targets **.NET Framework 4.8**. It has no NuGet runtime dependencies, so it compiles to a single exe of about 100 KB that runs on any Windows 10/11 machine.

## Startup

1. `Program.cs` enables DPI awareness and resolves the exe's folder to its real on-disk casing (`AppDiscovery.RealCase`).
2. `AppDiscovery.Discover(root)` scans:
   - the exe's folder, and each direct child folder that contains `nx.json`;
   - in each of those workspaces, every `apps/*/project.json` that has a `targets.serve` entry;
   - for each app, the port from `serve-original.options.port`, falling back to `serve.options.port`;
   - and the nearest `node_modules/nx`, walking up from the workspace.
3. `MainForm` builds one `AppCard` per app, sorted by port, and starts the following:
   - a **1 s status timer**, which reads the TCP table and updates every card;
   - a **500 ms pulse timer**, which animates the Starting/Stopping lamps;
   - a **git watcher**.

## Status logic

`MfeApp.GetStatus` checks these in order:

```
stopping                         → Stopping
own process alive + LastError    → Build error
own process alive + port open    → Running
own process alive                → Starting
own process exited unexpectedly  → Crashed
port open (someone else)         → External
otherwise                        → Stopped
```

The **port → PID** map comes from the Win32 `GetExtendedTcpTable` API, covering IPv4 and IPv6 listeners (`Ports.cs`). This is faster and more reliable than parsing `netstat`, and it also identifies who owns a busy port for the ⚠ warning.

## Process handling

| Concern | Implementation |
|---|---|
| Start | `Process` running `node <nx bin> serve <app>`, hidden, with stdout and stderr redirected and read asynchronously |
| Stop | `taskkill /PID <pid> /T /F` kills the whole tree (nx → node → esbuild workers…), then the launcher waits up to 20 s for the port to free |
| Stop an External server | Same, using the PID that owns the port |
| Launcher exits or crashes | Every child is assigned to a **Win32 Job Object** with `JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE`, so Windows kills them all when the launcher's handle closes (`ProcessJob.cs`) |
| Restart | Stop, then Start, on a background task so the UI stays responsive |

## Error detection

Each output line is stripped of ANSI codes, kept in a 5,000-line ring buffer, and checked by `MfeApp.TrackBuildState`:

- A line matching `bundle generation complete` or `compiled successfully` **clears** the error.
- Otherwise, the first line that matches the error pattern becomes `LastError`, and the `Failed` event fires once. The pattern covers `[ERROR]`, `bundle generation failed`, `already in use`, `Error:`, `ERR!` and `Cannot find project|module`.
- When the process exits without being stopped by the launcher, the status becomes **Crashed** and `Failed` fires with the exit code and the last error.

`Failed` drives the tray notification, the red card and the taskbar flash (`MainForm.OnAppFailed`).

## Git watching

`GitWatcher` resolves each workspace's git directory. That's either a `.git` folder, or the folder a `.git` file points to, as with submodules and worktrees. A submodule's git dir sits inside the parent's `.git/modules`, so one recursive `FileSystemWatcher` covers both.

The watcher reacts to changes to `HEAD`, `ORIG_HEAD`, `FETCH_HEAD`, `packed-refs` and `refs/heads/*`. Events are debounced for 3 s, then `Recheck()` merges the new app list into the current one, keeping running apps and their logs.

## Maintenance actions

| Action | Command |
|---|---|
| Reset Nx & clear caches | For each workspace: `node <nx bin> reset` with `NX_WORKSPACE_ROOT_PATH`, then delete `<workspace>/.angular/cache` |
| Clean npm cache | `cmd /d /c npm cache clean --force` in the root folder |

Both run through `Shell.Run`, which starts a hidden process and captures its output. If a command fails, the last 15 lines of its output are shown.

## Source map

| File | Responsibility |
|---|---|
| `Program.cs` | Entry point, DPI awareness |
| `AppDiscovery.cs` | Workspaces, apps, ports, the nx binary, path casing |
| `MfeApp.cs` | One app: process, status, log buffer, error detection, start/stop/restart |
| `MainForm.cs` | Window, header, actions menu, bulk actions, re-check, tray notifications |
| `AppCard.cs` | One app row: lamp, name, ⚠ warning, details link, badge, buttons |
| `LogForm.cs` | Colour-coded log viewer |
| `GitWatcher.cs` | Git ref watcher |
| `Maintenance.cs` | `nx reset`, `.angular/cache`, `npm cache clean` |
| `Ports.cs` | Port → PID table via `GetExtendedTcpTable` |
| `ProcessJob.cs` | Job Object that kills children on exit |
| `Shell.cs` | Runs a hidden command and captures its output |
| `Theme.cs` | Colours, fonts, DPI scaling, Segoe MDL2 icon glyphs |
| `UiControls.cs` | Custom-drawn `FlatButton`, `Lamp`, `Pill`, `Card` |

## Building

`src/MfeLauncher.csproj` is an SDK-style project:

- `TargetFramework` is `net48` and `UseWindowsForms` is on;
- `LangVersion` is `latest`, so modern C# compiles down to .NET Framework;
- `Microsoft.NETFramework.ReferenceAssemblies` is a build-only package, so any .NET 6+ SDK can compile it;
- `app.ico` is used as the application icon and embedded as a resource for the window and tray icons.
