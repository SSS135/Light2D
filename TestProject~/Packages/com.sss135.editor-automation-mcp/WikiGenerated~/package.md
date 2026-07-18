<!-- GENERATED from Docs~/WikiSource + tool schema on domain reload. Do not edit; edit Docs~/WikiSource/package.md. -->

# `package`

Drive the Unity Package Manager — add, remove, list, and search. Calls
mutate `Packages/manifest.json` and go through `UnityEditor.PackageManager`,
so add/remove participate in Unity's package resolution and domain reloads.

## The domain-reload gap

`package.add` and `package.remove` both trigger UPM resolution, which on a
real change ends in a **domain reload**. While the reload runs, the
plugin's HTTP gateway is unreachable — no managed code runs in the Editor
between `beforeAssemblyReload` and the next domain's autostart, so the
response only lands after the reload completes.

Foreground Editor settles in seconds. **Unfocused**, Unity throttles
`EditorApplication.update`, autostart fires late, and the gateway looks
hung. Mitigation is the external wake-unity helper described in
[`wiki`](wiki.md) — run [`wake-unity.ps1`](../../Tools/wake-unity.ps1)
(at `Packages/com.sss135.editor-automation-mcp/Tools/wake-unity.ps1`) immediately
before each `package.add` / `package.remove` from an unfocused Editor to
bring the window forward so the reload can complete.

`package.list` and `package.search` are read-only — safe to call any time
the gateway is up.

## Cross-cluster overlap

- **`package.add` then code that uses the new types** — the new assemblies
  aren't in the type system until the reload settles. Probe
  [`editor.application-get-state`](editor.md) and wait for
  `IsCompiling` / `IsUpdating` to flip to `false` before invoking
  [`script.execute`](script.md) or constructing components from the new
  package.
- **`package.search` vs `package.list`** — `search` covers registry plus
  installed packages with ranked matches; `list` only enumerates the
  current project. "Is X installed?" → `list` with `nameFilter`. "What
  versions of X exist?" → `search` with `offlineMode: false`.

## See also

- [`wiki`](wiki.md) — wake-unity workaround for the unfocused-reload case.
- [`workflows`](workflows.md) — "Install a UPM package" entry.

To read a method's wiki, call `tools/call` with `arguments = { method: "<dotted>" }` (omit `args`). Include `args` (even `{}`) to execute.

## Methods

- `package.add` — Install a package from the Unity Package Manager registry, Git URL, or local path. This operation modifies the project's manifest.json and triggers package resolution. Note: Package installation may t…
- `package.list` — List all packages installed in the Unity project (UPM packages). Returns information about each installed package including name, version, source, and description. Use this to check which packages are…
- `package.remove` — Remove (uninstall) a package from the Unity project. This removes the package from the project's manifest.json and triggers package resolution. Note: Built-in packages and packages that are dependenci…
- `package.search` — Search for packages in both Unity Package Manager registry and installed packages. Use this to find packages by name before installing them. Returns available versions and installation status. Searche…

