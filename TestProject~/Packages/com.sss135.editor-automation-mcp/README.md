# Unity Editor Automation

Drive the Unity Editor from AI agents through a single, self-describing MCP tool.

## What it is

Unity Editor Automation exposes the Unity Editor to AI agents (Claude Code,
Gemini, Copilot, Cursor, and any MCP client) through one consolidated tool:

- **Single `call` MCP tool** with live wiki navigation — agents browse an
  in-Editor wiki to discover available operations instead of memorizing a wide
  tool surface.
- **Attribute-driven tool framework** — any C# method becomes an automation
  tool with a single attribute, no manual schema wiring.
- **`script.execute`** — run arbitrary C# in-Editor via Roslyn compilation.
- **Recorder + QueryEngine** — record Editor activity and query scene/asset
  state declaratively.
- **HTTP gateway** — the Editor hosts an HTTP endpoint that MCP clients connect
  to while the Editor process is responsive.
- **Jobs** — long-running and deferred work scheduled against the Editor loop.
- **wake-unity watchdog** — brings an unfocused Editor window to the foreground
  so recompiles and domain reloads triggered over MCP can complete instead of
  stalling.

## Quick start

1. Open your Unity project (the package runs inside the Editor).
2. Point your MCP client at the `editor-automation` server:

   ```
   http://127.0.0.1:17777/mcp
   ```

3. Issue a `call` and follow the live wiki to discover operations.

## Credits / Based on Unity-MCP

Unity Editor Automation is a derivative work based on
[Unity-MCP](https://github.com/IvanMurzak/Unity-MCP) by Ivan Murzak, licensed
under the Apache License, Version 2.0. The original copyright is retained; see
[NOTICE](./NOTICE) for attribution details.

## License

Apache-2.0. See [LICENSE](./LICENSE).
