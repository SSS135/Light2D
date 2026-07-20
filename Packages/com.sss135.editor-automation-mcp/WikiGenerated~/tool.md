<!-- GENERATED from Docs~/WikiSource + tool schema on domain reload. Do not edit; edit Docs~/WikiSource/tool.md. -->

# `tool`

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

To read a method's wiki, call `tools/call` with `arguments = { method: "<dotted>" }` (omit `args`). Include `args` (even `{}`) to execute.

## Methods

- `tool.list` — List all available MCP tools. Optionally filter by regex across tool names, descriptions, and arguments.

