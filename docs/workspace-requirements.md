# Workspace requirements

MFE Launcher is made for **micro-frontend (MFE) architectures in an Nx monorepo**. In that setup, a *host* (shell) application loads several *remote* applications at runtime, and each one runs on its own dev-server port. During development you usually need several of them running at once. This tool replaces the "one terminal per app" routine.

## Micro-frontend pattern

```
my-workspace/                 ← Nx monorepo root (put mfe-launcher.exe here)
├─ nx.json
├─ package.json
├─ node_modules/              ← must contain nx (npm install)
├─ apps/
│  ├─ shell/project.json      host            → port 4200
│  ├─ catalog/project.json    remote          → port 4201
│  ├─ checkout/project.json   remote          → port 4202
│  └─ account/project.json    remote          → port 4203
├─ libs/                      shared libraries (ignored by the launcher)
└─ shared-ui/                 optional nested Nx workspace (e.g. a git submodule)
   ├─ nx.json
   └─ apps/design-system/project.json         → port 4210
```

- **Host and remotes.** Every app with a `serve` target is listed, whether it's a host or a remote. The launcher doesn't need to know which is which.
- **Nested workspaces.** A direct child folder with its own `nx.json` is scanned too, and its apps are shown with that folder's name. This fits a setup where shared remotes live in a git submodule. Apps there are run with the child folder as the working directory, and `NX_WORKSPACE_ROOT_PATH` points to it. The `nx` binary is taken from the nearest `node_modules` going up, so a submodule can use the parent's install.

## Supported federation setups

| Setup | How it's served | Port read from | Status |
|---|---|---|---|
| **Native Federation**: `@angular-architects/native-federation`, esbuild | `serve` = federation builder that wraps `serve-original` (`@angular/build:dev-server`) | `targets.serve-original.options.port` | ✅ Tested (Angular 22, Native Federation 22, Nx 23.1) |
| **Nx Module Federation**: `@nx/angular`, webpack | `serve` = `@nx/angular:module-federation-dev-server` (host) / `@nx/angular:dev-server` (remotes) | `targets.serve.options.port` | ✅ Supported, see the note below |
| **Module Federation plugin**: `@angular-architects/module-federation`, webpack | `serve` = Angular dev-server with a custom webpack config | `targets.serve.options.port` | ✅ Supported |
| **Plain Nx apps** (no federation) | any `serve` executor | `targets.serve.options.port` | ✅ Start, stop and status. Error detection is generic. |
| Non-Angular Nx apps (React, Vue, Node…) | any `serve` executor | `targets.serve.options.port` | ⚠️ Start, stop and status only. Build-error lines may not be recognised. |

> **Note on Nx Module Federation hosts.** `nx serve <host>` with `module-federation-dev-server` also serves the host's remotes, as static servers or with `--devRemotes`. If you start the host from the launcher, those remotes show up as **External**. To develop a remote, start it from the launcher **before** the host, or close the static copy and start the remote on its port with the port-in-use prompt.

## Version requirements

| Tool | Requirement | Tested with |
|---|---|---|
| **Windows** | 10 or 11 (.NET Framework 4.8 is built in) | Windows 10 22H2 |
| **Node.js** | On `PATH`. Any version your Angular/Nx version supports. | 22.22 |
| **Nx** | Installed locally in `node_modules`. Projects are defined in `project.json` files. | 23.1.0 |
| **Angular** | Any version for start, stop and status. **17+ recommended** for full build-error detection. | 22.0.6 |
| **Federation** | Optional: Native Federation, Nx Module Federation, or `@angular-architects/module-federation` | Native Federation 22.0.6 |

### Angular version and build-error detection

Start, stop, restart and the Running/External/Crashed statuses depend only on processes and ports, so they work with any Angular version, or any framework. Detecting **Build error** and clearing it again relies on the dev server's output:

| Angular | Builder | Failure detected from | Cleared by |
|---|---|---|---|
| **17 – 22+** (recommended) | esbuild: `@angular/build:dev-server` / `application` (Native Federation uses this) | `✘ [ERROR] …`, `Application bundle generation failed` | `Application bundle generation complete` |
| 12 – 16, or 17+ still on `browser` | webpack: `@angular-devkit/build-angular:dev-server` | lines starting with `Error:` / `error` | `Compiled successfully` |

Lines such as `Cannot find module …`, `Cannot find project …`, `port … already in use` and `npm ERR!` are detected with every builder.

## What each app needs

1. A `project.json` at **`apps/<app>/project.json`**.
2. A **`serve`** target declared in that file. Targets inferred only by Nx plugins aren't visible to the launcher.
3. A **fixed port**, in `serve-original.options.port` (Native Federation) or `serve.options.port`. Without a fixed port, the app can still be started, but it stays at **Starting** because the launcher can't see it listening.

### Example: Native Federation app

```json
{
  "name": "catalog",
  "targets": {
    "serve": {
      "executor": "@angular-architects/native-federation:build",
      "options": {
        "target": "catalog:serve-original:development",
        "dev": true,
        "devServer": true,
        "port": 0
      }
    },
    "serve-original": {
      "executor": "@angular/build:dev-server",
      "options": { "port": 4201 }
    }
  }
}
```

### Example: Nx Module Federation remote

```json
{
  "name": "catalog",
  "targets": {
    "serve": {
      "executor": "@nx/angular:dev-server",
      "options": { "port": 4201, "publicHost": "http://localhost:4201" }
    }
  }
}
```

## How an app is started

For each app, the launcher runs the same command you would type yourself:

```
node <workspace>/node_modules/nx/<bin> serve <app>
```

- **Working directory:** the app's workspace folder.
- **`NX_WORKSPACE_ROOT_PATH`** is set to the workspace folder, with the drive letter in its real casing. Nx fails to match its own files when the casing differs, for example `d:\` vs `D:\`.
- **`FORCE_COLOR=0`** keeps the log output plain.

The path to `nx` comes from the `bin` field of `node_modules/nx/package.json`, so it keeps working when Nx moves its entry file between versions.
