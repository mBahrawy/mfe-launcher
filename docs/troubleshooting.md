# Troubleshooting

## The window opens but no apps are listed

The empty state shows the folder the launcher scanned. Check that:

- the exe is in the **workspace root**, next to `nx.json`;
- `node_modules/nx` exists, so run `npm install` first. Without a local `nx`, the workspace is skipped;
- each app has `apps/<app>/project.json` with an explicit **`serve`** target.

Then click **Re-check apps**.

## "Could not start node"

`node` isn't on `PATH` for the launcher. Install Node.js, or fix `PATH`, then restart the launcher. Windows apps only see `PATH` changes after they restart, and sometimes only after you sign out and back in.

## An app stays on "Starting"

- The first build can take a while. Check its **Logs**.
- The app has no fixed port, or the port in `project.json` isn't the one the server uses. The launcher only marks an app **Running** when it sees the configured port listening.

## Every app shows "External" with ⚠

The ports are already in use: by servers started from a terminal (for example `npm start`), by another launcher window, or by leftovers from an earlier session. You have three options:

- keep them, since External servers work normally;
- **Close all** to stop them;
- click **Start** on an app and confirm, to take it over in the launcher.

## "Nx … not found in TypeScript compilation" or path errors in a nested workspace

This happens when Nx sees the workspace path with different drive-letter casing (`d:\` vs `D:\`). The launcher always passes the real casing in `NX_WORKSPACE_ROOT_PATH`. If you start the same app from a terminal, `cd` into the folder using the exact casing.

## Builds behave strangely after switching branches

Use **⋯ → Reset Nx & clear caches**. It stops the launcher's apps, runs `nx reset` and deletes `.angular/cache` in every workspace. If npm itself misbehaves (corrupted tarballs, `EINTEGRITY`), use **⋯ → Clean npm cache**, then run `npm install` again.

## The exe can't be replaced ("file in use")

A running launcher locks its own exe. Close it first (this stops the apps it started), then copy the new exe in.

## Windows SmartScreen blocks the exe

The exe isn't code-signed. Click **More info → Run anyway**. Alternatively, build it yourself with `build.ps1`: files you build locally aren't marked as downloaded from the internet.

## The list didn't refresh after `git pull`

- The footer should say *"Watching git"*. If it says *"Not a git repository"*, the root has no `.git`, so click **Re-check apps**.
- The re-check runs about 3 seconds after git stops writing. Apps that are running are never removed by a re-check.

## Dev servers left running after the launcher was killed

Servers the launcher started belong to a Job Object, so Windows ends them when the launcher's process ends, even if it's killed from Task Manager. Servers started from a terminal aren't touched. If a port stays busy, the ⚠ tooltip shows which process holds it.
