# ThanhDV Package Manager

A private package hub for Unity projects: a web portal that stores `.unitypackage` files and lists the UPM
packages on Verdaccio, plus a Unity editor tool to install them straight into a project.

## Parts

| Folder | What it is |
| --- | --- |
| [`Web/`](Web/README.md) | Portal: ASP.NET Core API + Blazor WebAssembly UI, SQLite, one Docker image |
| [`Unity.PackageManager/`](Unity.PackageManager/Packages/com.thanhdv.packagemanager/README.md) | Unity project hosting the editor tool `com.thanhdv.packagemanager` |

## Features

**Web portal**
- Browse `.unitypackage` packages with several versions each, descriptions and images.
- Fill package details from an Asset Store link.
- Upload new versions and sort files already in the storage folder into packages (admin).
- List UPM packages from Verdaccio with README, versions and an install snippet.
- Search across both sources.

**Unity tool** (Unity 2022.3+, **Tools > ThanhDV > Package Manager**)
- A window like Unity's Package Manager for both sources.
- Download and import any `.unitypackage` version; remove it later, keeping assets shared with other packages.
- Install, switch version or remove Verdaccio packages; adds the scoped registry to the manifest.
- Imported packages are tracked in `ProjectSettings/`, so the whole team sees the same state.

## CI

- Push to `main` with changes under `Web/`: tests, then the portal image `ghcr.io/thanhdv143/upkgs-web`.
- Push a `v*` tag: publishes the Unity tool to Verdaccio (`https://upm.thanhdv.com`).
