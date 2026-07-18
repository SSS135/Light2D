<!-- GENERATED from Docs~/WikiSource + tool schema on domain reload. Do not edit; edit Docs~/WikiSource/initial-setup.md. -->

# `initial-setup`

Editor Automation runs entirely inside the Unity Editor as an HTTP gateway on
`http://127.0.0.1:17777/mcp`. There is no standalone server, no SignalR bridge, no CLI, and
no installer — your MCP client connects to the gateway directly.

---

## 1. Install the package

Add the `com.sss135` OpenUPM scope to your project's `Packages/manifest.json` and add the
package entry:

```json
{
  "scopedRegistries": [
    {
      "name": "package.openupm.com",
      "url": "https://package.openupm.com",
      "scopes": ["com.sss135"]
    }
  ],
  "dependencies": {
    "com.sss135.editor-automation-mcp": "0.72.1"
  }
}
```

Open the project in Unity (`6000.x` or newer). The HTTP gateway auto-starts on port `17777`
once the Editor finishes loading.

---

## 2. Point your MCP client at the gateway

Configure your MCP client (Claude Code, Cursor, Copilot, etc.) to connect to
`http://127.0.0.1:17777/mcp`.

Example MCP-client config:

```json
{
  "mcpServers": {
    "editor-automation": {
      "url": "http://127.0.0.1:17777/mcp"
    }
  }
}
```

The client uses spec-compliant MCP / JSON-RPC 2.0 — every modern MCP client supports this
shape natively. No CLI, no helper scripts, no auth tokens.

---

## 3. Verify

Send `tools/list` from your MCP client. You should see a single tool named `call`; the
dotted Editor Automation method names (`assets.copy`, `gameobject.create`, etc.) ride in
`arguments.method` on each `tools/call` invocation. From an MCP-client agent (Claude Code,
Cursor, Copilot, etc.) the tool surfaces as `mcp__editor-automation__call({ method, args })` —
client UIs handle the JSON-RPC envelope for you.

---

## Troubleshooting

- **Gateway not starting**: check `ProjectSettings/EditorAutomationHttpGateway.asset` for
  `EnableHttpGateway = true`. The asset is auto-created with sane defaults on first run.
- **Port 17777 already bound**: another Unity Editor on the same machine owns the port. Close
  it, or edit the `DefaultPort` constant in `HttpGateway.cs` and recompile.
- **Client config still pointing at a deleted `editor-automation-server` binary**: the old SignalR
  server is gone in this version. Rewrite the client config to use the HTTP gateway URL above.

