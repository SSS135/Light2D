# Light2D

Light2D is a GPU-based dynamic 2D lighting package for Unity. It renders light obstacles, direct lights, and optional ambient propagation into low-resolution textures, then composites the result over a camera. Lights and obstacles can move at runtime and do not require physics colliders. Version 2.0.0 targets **Unity 6000.4** (developed with **Unity 6000.4.7f1**) and supports the Built-in Render Pipeline and URP 17.4.

## Repository layout

This repository root is a Unity 6000.4.7f1 project used to develop and test the package. The package itself lives in [`Packages/com.sss13594.light2d/`](Packages/com.sss13594.light2d/). To develop or run the tests, open the **repository root** in Unity, not a subfolder — the package is embedded and auto-discovered under `Packages/`.

| Path | Purpose |
| --- | --- |
| `Packages/com.sss13594.light2d/` | The `com.sss13594.light2d` UPM package (runtime, editor, tests, samples, docs) |
| `Assets/` | Development-host scenes, sample install, and test automation |
| `ProjectSettings/`, `Packages/manifest.json` | Unity project configuration for the host |

## Install into your own project

Install the package by its subpath within this repository (not the repository root).

**Git URL:** In **Window > Package Management > Package Manager**, select **Install package from git URL...** and enter:

```text
https://github.com/SSS135/Light2D.git?path=/Packages/com.sss13594.light2d
```

Pin a tag, branch, or commit by appending a revision after the `path` query, for example:

```text
https://github.com/SSS135/Light2D.git?path=/Packages/com.sss13594.light2d#v2.0.0
```

**Local disk / embedded:** Select **Install package from disk...** and choose `Packages/com.sss13594.light2d/package.json`, or embed the folder directly under your project's `Packages/`. An equivalent manifest entry is:

```json
"com.sss13594.light2d": "file:X:/src/unity/Light2D/Packages/com.sss13594.light2d"
```

Unity installs the dependencies declared in the package `package.json`, including URP 17.4 even when the consuming project uses the Built-in Render Pipeline.

## Documentation

Full setup, architecture, migration, and testing guides live with the package:

- [Package README](Packages/com.sss13594.light2d/README.md)
- [Installation and setup](Packages/com.sss13594.light2d/Documentation~/INSTALLATION.md)
- [Components, settings, and render architecture](Packages/com.sss13594.light2d/Documentation~/ARCHITECTURE.md)
- [Legacy `Assets/Light2D` migration](Packages/com.sss13594.light2d/Documentation~/MIGRATION.md)
- [Troubleshooting](Packages/com.sss13594.light2d/Documentation~/TROUBLESHOOTING.md)
- [Development and testing](Packages/com.sss13594.light2d/Documentation~/TESTING.md)
- [Diagnostics and benchmarking](Packages/com.sss13594.light2d/Documentation~/BENCHMARKING.md)
- [Changelog](Packages/com.sss13594.light2d/CHANGELOG.md)

## License

Light2D is available under the [MIT License](LICENSE.md).
