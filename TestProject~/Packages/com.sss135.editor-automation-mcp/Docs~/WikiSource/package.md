# package

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

### package.add

Resolves the package, writes `Packages/manifest.json`, and (when the
manifest changed) triggers the domain reload above. The response arrives
**after** the reload completes — do not chain dependent RPCs into the same
batch.

`packageId` accepts four shapes (exact syntax lives in the generated
parameter schema): **registry latest** (`com.unity.textmeshpro`), **registry
pinned** (`com.unity.textmeshpro@3.0.6`), **Git URL** with optional
`#branch-or-tag` revision suffix, and **local path** (`file:…` directory or
tarball). Pin a version or Git revision whenever the package is
load-bearing — leaving the install at "latest" makes future project
reproductions susceptible to upstream version drift.
