# Getting started

## 1. Build the exe

You need any **.NET SDK 6 or later**. Check with `dotnet --version`.

The project targets **.NET Framework 4.8**, so the exe runs on every Windows 10/11 machine without installing a runtime. The SDK is only needed to build it. The reference assemblies come from the `Microsoft.NETFramework.ReferenceAssemblies` NuGet package, so you don't need the .NET Framework Developer Pack either.

```powershell
git clone <this-repo-url> mfe-launcher
cd mfe-launcher
.\build.ps1
```

Output:

```
Build succeeded.
    0 Warning(s)
    0 Error(s)
Built ...\mfe-launcher\dist\mfe-launcher.exe
```

`build.ps1` options:

| Command | Result |
|---|---|
| `.\build.ps1` | Builds `dist\mfe-launcher.exe` |
| `.\build.ps1 -Dest "C:\code\my-workspace"` | Also copies the exe into that folder |

If PowerShell refuses to run the script ("running scripts is disabled"), run it with a one-off bypass:

```powershell
powershell -ExecutionPolicy Bypass -File .\build.ps1
```

You can also build without the script:

```powershell
dotnet build src\MfeLauncher.csproj -c Release
# exe: src\bin\Release\net48\mfe-launcher.exe
```

## 2. Install it in a workspace

1. Copy `mfe-launcher.exe` into the **root of your Nx workspace**, the folder that contains `nx.json` and `node_modules`.
2. Add it to the workspace's `.gitignore`:
   ```gitignore
   mfe-launcher.exe
   ```
3. Make sure dependencies are installed: run `npm install` (or `pnpm install` / `yarn`).

The exe is self-contained, so one copy per workspace is all you need. If your workspace includes a nested Nx workspace, for example a git submodule with its own `nx.json`, **one exe in the root covers both**.

## 3. First run

1. Double-click `mfe-launcher.exe`.
   - **SmartScreen:** the exe isn't code-signed, so Windows may show "Windows protected your PC". Click **More info → Run anyway**. It only asks once per file.
2. The window lists every app it found, sorted by port. The header shows the workspace name and path.
   - Apps you already started from a terminal show a **blue** lamp (**External**) and a ⚠ icon, because their port is held by a process the launcher didn't start.
3. Click **Start** on an app:
   - the lamp turns **amber** (*Starting*) while Angular builds;
   - it turns **green** (*Running*) once the port is listening;
   - it turns **red** (*Build error*), with the first error line, if the build fails.
4. Click **Logs** to see that app's output, or the `localhost:<port>` link to open it.
5. Close the window to stop every app the launcher started. It asks you to confirm first.

The launcher also adds a tray icon. Error notifications appear there, and clicking one opens the failing app's logs.

## 4. After `git pull`

If the workspace is a git repository, you don't need to do anything. The footer says *"Watching git"*. When a pull, checkout, merge or submodule update moves a ref, the list re-checks itself about 3 seconds later. New apps are added (with a notification), and deleted apps are removed. Apps that are running are kept.

If the workspace isn't a git repository, click **Re-check apps** after updating.

## 5. Updating the launcher

1. Close the running launcher. This stops the apps it started, because the exe is locked while it runs.
2. Pull this repo and run `.\build.ps1 -Dest "<your workspace>"`.
3. Start the new exe.
