# ThanhDV Package Manager

A Unity editor window, like Unity's Package Manager, for the ThanhDV package portal:
browse, import and remove `.unitypackage` files, and install UPM packages from Verdaccio.

Requires Unity 2022.3 or newer.

## Install

### From Git URL

In Unity: **Window > Package Manager > + > Add package from git URL...**, then paste:

```
https://github.com/ThanhDV143/PackageManager.git?path=/Unity.PackageManager/Packages/thanhdv.packagemanager
```

Add `#v1.0.1` at the end to pin a release tag instead of the latest `main`.

### From the ThanhDV registry

Add the registry and the package to `Packages/manifest.json`:

```json
{
  "scopedRegistries": [
    {
      "name": "ThanhDV",
      "url": "https://upm.thanhdv.com",
      "scopes": [ "thanhdv" ]
    }
  ],
  "dependencies": {
    "thanhdv.packagemanager": "1.0.1"
  }
}
```

## Setup

Open **Project Settings > ThanhDV Package Manager** and enter the portal URL (the web app).

## Use

Open **Tools > ThanhDV > Package Manager**.

- **Unity Packages**: pick a version, then **Download & Import**. **Remove** moves the package's files to the trash.
- **UPM**: **Install**, **Switch to** another version, **Remove**, or **Open in Package Manager**.
- **In Project**: everything this project already uses from the portal.

Imported packages are tracked in `ProjectSettings/ThanhDV.PackageManager.json`. Commit it so the team shares the same state.
