# tool

Live tool-registry surface. Use `tool.*` to enumerate the methods that are
currently registered with the gateway and to toggle individual tools on or off
at runtime. Pair this namespace with [`system`](system.md) for the basic
liveness / introspection plumbing, and with [`wiki`](wiki.md) for routing
rules.

## When to reach for `tool.*`

- **Discovery before scripting.** Call `tool.list` with a `regexSearch` to
  confirm a method exists under the name you expect before chaining several
  calls into a workflow. This is cheaper than reading the full root index and
  faster than trial-and-error against `call("<method>")`.
- **Mute a noisy or destructive tool.** Disable a single tool by name with
  `tool.set-enabled-state` when you want it kept out of agent reach for the
  rest of the session.
- **Restore the surface.** Re-enable with the same call, flipping `Enabled`
  back to `true`.

A disabled tool stops appearing in the gateway's advertised tool list and
returns an error if called by id. Enable state is persisted across the current
Editor session by the registry — there is no per-call override.

### tool.list

`includeDescription` and `includeInputs` both default to off — the bare call
returns just tool names. Turn them on only when you actually need the wider
payload; for most agent flows the name list plus a targeted `call("<method>")`
to fetch the page is cheaper than dumping every description inline.

`regexSearch` matches against the tool name, the tool description, *and* each
argument's name and description, so a search like `regexSearch: "prefab"` will
also surface methods that merely accept a `prefabPath` argument. Anchor the
pattern (`^assets\.prefab-`) when you want a strict namespace filter.

### tool.set-enabled-state

The `tools` array entries use `Name` / `Enabled` (PascalCase) — the input
field on the request itself is the lowercase `tools` shown in the generated
schema. The response's `Success` dictionary keys back the **original** input
name verbatim (case preserved), with `false` for unknown, ambiguous, or empty
names. Always inspect `Success` rather than assuming a non-throwing call
means every toggle landed.

Set `includeLogs: true` when a toggle silently fails — the per-entry log
trail is the only signal that explains *why* a name was rejected.
