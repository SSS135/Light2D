<!-- GENERATED from Docs~/WikiSource + tool schema on domain reload. Do not edit; edit Docs~/WikiSource/http-gateway.md. -->

# `http-gateway`

A live Unity Editor exposes an in-process **MCP-spec / JSON-RPC 2.0** HTTP gateway on loopback. Two routes only: `POST /mcp` and `GET /health`. No external server, no SignalR bridge, no CLI, no stdio — your MCP client (Claude Code, Cursor, Copilot, etc.) connects to the URL natively.

---

## 1. Port discovery

The gateway binds a fixed default port `17777`. It also writes the active port to **`<projectRoot>/Library/EditorAutomationHttp/port.txt`** every time it starts. The file's only content is the port number (e.g. `17777`) — no JSON, no trailing newline.

- **Default URL:** `http://127.0.0.1:17777/mcp`
- **If the port file is missing**, the Editor is not running with the gateway enabled — stop and tell the user.
- **If port 17777 is already bound** (a second Unity Editor on the same machine), the user has to close one Editor or edit the `DefaultPort` constant in `HttpGateway.cs` and recompile. There is no env override.

---

## 2. Health

`GET <url>/health` → `200 OK` with `{"ready":true,"port":17777,...}`. Returns `503` if the listener is alive but disposed. Not part of MCP; useful only for reconnect-loop polling after a domain reload and for debugging the transport when `/mcp` itself fails.

---

## 3. Wire protocol — JSON-RPC 2.0

`POST <url>/mcp` with `Content-Type: application/json`. Body is either a single JSON-RPC 2.0 envelope or a batch array (see section 7):

```json
{"jsonrpc":"2.0","id":1,"method":"<method>","params":{...}}
```

Success:

```json
{"jsonrpc":"2.0","id":1,"result":{...}}
```

Error:

```json
{"jsonrpc":"2.0","id":1,"error":{"code":-32601,"message":"...","data":{"identifier":"METHOD_NOT_FOUND"}}}
```

- `jsonrpc` is always the literal string `"2.0"`.
- `id` is **type-preserving and echoed**: string in, string out; number in, number out; null only on parse failure where no id could be recovered.
- Error `code` is a spec integer (`-32700` parse, `-32600` invalid request, `-32601` method not found, `-32602` invalid params, `-32603` internal). The original string identifier is preserved in `error.data.identifier`.

---

## 4. Capabilities and handshake

This gateway implements exactly five protocol methods:

| Method | Purpose |
|---|---|
| `initialize` | Client handshake. Returns `protocolVersion: "2025-06-18"`, `capabilities: { tools: { listChanged: false } }`, `serverInfo`. |
| `notifications/initialized` | Client → server one-way. HTTP **202 + empty body**, no JSON-RPC response object. |
| `ping` | Liveness check. Returns `{}`. |
| `tools/list` | Returns exactly **one** tool descriptor named `call` (see section 5). |
| `tools/call` | Invokes the `call` tool (see section 5). |

There is no `Mcp-Session-Id` header. There is no SSE / `GET /mcp` / server-push. There are no MCP progress tokens (long-running ops use the job-ticket pattern in section 8). There is no auth — the gateway binds loopback only.

---

## 5. The single `call` tool — three argument shapes

`tools/list` returns exactly one descriptor:

```json
{"name":"call","description":"..."}
```

> **For MCP-client agents:** Claude Code, Cursor, Copilot, etc. invoke this tool transparently as `mcp__editor-automation__call({ method, args })` — you do not assemble JSON-RPC envelopes or hit `curl` yourself. The `curl` examples below are for direct-HTTP debugging and for non-MCP-client consumers that speak raw JSON-RPC 2.0 against the gateway.

All Editor Automation dotted method names (e.g. `assets.copy`, `gameobject.create`, `ping`) ride in `arguments.method`. The three argument shapes drive what happens:

| `arguments` shape | Result |
|---|---|
| `{}` *or omitted* | **Wiki root** — markdown index of all namespaces and methods. |
| `{ "method": "gameobject" }` | **Namespace wiki** — every method in `gameobject`. |
| `{ "method": "gameobject.find" }` *(no `args` key)* | **Method wiki** — summary, parameter table, JSON schema, curl example. |
| `{ "method": "gameobject.find", "args": { "name": "Main Camera" } }` | **Execute** the method with `args`. Present `args` — even as `{}` — to execute. |

Example execute call:

```bash
curl -s -X POST http://127.0.0.1:17777/mcp \
  -H "Content-Type: application/json" \
  -d '{"jsonrpc":"2.0","id":1,"method":"tools/call","params":{"name":"call","arguments":{"method":"ping","args":{}}}}'
```

Example wiki call (discover a method):

```bash
curl -s -X POST http://127.0.0.1:17777/mcp \
  -H "Content-Type: application/json" \
  -d '{"jsonrpc":"2.0","id":2,"method":"tools/call","params":{"name":"call","arguments":{"method":"gameobject.find"}}}'
```

The wiki body comes back inside the standard MCP `tools/call` envelope as a single text content block.

---

## 6. Error semantics

There are two error layers:

- **Transport / protocol errors** — JSON-RPC `error` object on the outer envelope. Used for `parse error` (`-32700`), `invalid request` (`-32600`), `method not found` (`-32601`, including for unknown dotted methods inside `arguments.method`), `invalid params` (`-32602`, including auto-validation of required parameters **before** the handler runs), and `internal error` (`-32603`).
- **Handler-domain errors** — `result.isError: true` inside a successful `tools/call` response, per MCP spec. The tool body still returns 200, the envelope still has `result` (not `error`), and the content block carries the failure message.

Required-parameter validation happens at the dispatcher: an `arguments.args` payload missing a required parameter returns `-32602` with the missing names in `error.data` and the handler is **not** invoked.

---

## 7. JSON-RPC batch arrays

The gateway accepts JSON-RPC 2.0 batch arrays as the request body and dispatches each element through the same protocol switch as a single envelope. Batching is **gateway-level**: it does not change the `mcp__editor-automation__call` MCP tool surface — MCP-client agents still issue one `tools/call` per call. Batch arrays are reserved for raw-HTTP clients that drive the gateway directly (curl, scripts, internal Editor tooling).

Semantics:

- **Sequential execution.** Elements are dispatched in input order on the dispatcher's existing main-thread marshal — no parallelism, no reordering.
- **Fail-fast.** The loop halts on the first response whose envelope is a JSON-RPC `error`, **or** whose successful `tools/call` result carries `status: "Processing"` (async tools are not allowed inside a batch). The triggering response is the final entry in the returned array; subsequent elements are not dispatched.
- **Notifications.** Per JSON-RPC 2.0 §6, batch elements without an `id` member are notifications: the handler still runs, but they contribute no response entry. A notification cannot fail-fast the batch on its own.
- **Empty batch `[]`** returns a single `-32600 invalid request` envelope (not an array), id `null` — matching the spec.
- **All-notifications batch** returns **HTTP 202 + empty body** (no JSON-RPC envelope), mirroring the single-notification code path.
- **Mixed batch (notifications + requests)** returns **HTTP 200** with a response array containing only the request entries; the notification slots are absent.
- **Per-call timeout** wraps the whole batch dispatch — the `HttpDefaultTimeoutMs` / `HttpMaxTimeoutMs` bounds apply once, not per element.
- **Modal pre-check** runs per element; the first element to hit a modal returns a modal-blocked error envelope and the batch fails fast.

Example two-ping batch:

```bash
curl -s -X POST http://127.0.0.1:17777/mcp \
  -H "Content-Type: application/json" \
  -d '[{"jsonrpc":"2.0","id":1,"method":"ping"},{"jsonrpc":"2.0","id":2,"method":"ping"}]'
```

Returns:

```json
[
  {"jsonrpc":"2.0","id":1,"result":{}},
  {"jsonrpc":"2.0","id":2,"result":{}}
]
```

---

## 8. Long-running operations — job tickets

Long-running tools (package add/remove, test runs, script utilities) return immediately with `McpStatus.Processing` and a ticket id of the form `j_<utc_compact_stamp>_<8-hex>`. Three control RPCs let you poll, list, and cancel:

| Dotted method | Purpose |
|---|---|
| `system.job-status` | Status / progress / final result for one ticket. |
| `system.job-list` | Slim entries `{ ticket_id, method, status, started_at }` for all live tickets. |
| `system.job-cancel` | Best-effort cancel. |

Progress events are also appended to a JSONL monitor at `<projectRoot>/Library/EditorAutomationHttp/jobs.jsonl`. The monitor is kept across gateway restarts and pruned to the newest 30 one-line JSON entries. Tickets are TTL-evicted after completion (configurable in `HttpGatewaySettings`).

---

## 9. Response spill (large outputs)

When a `tools/call` result body exceeds the spill threshold (~10,000 characters), the gateway writes the full JSON to a file under `Library/EditorAutomationHttp/responses/` and replaces the response body with a pointer:

```json
{
  "outputTooLong": true,
  "file": {
    "path": "<absolute path to the spill file>",
    "contentType": "application/json",
    "characters": 48211,
    "threshold": 10000
  }
}
```

Read the file directly from disk (path is absolute). The gateway sweeps old spill files automatically. To opt out per-call, pass `outputTooLong: false` inside `arguments.args`.

---

## Minimal session

```bash
URL=http://127.0.0.1:17777/mcp

# Handshake
curl -s -X POST $URL -H "Content-Type: application/json" \
  -d '{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2025-06-18","capabilities":{},"clientInfo":{"name":"cli","version":"0"}}}'

# Notify initialized (HTTP 202 + empty body)
curl -s -X POST $URL -H "Content-Type: application/json" \
  -d '{"jsonrpc":"2.0","method":"notifications/initialized"}'

# List tools (one descriptor: `call`)
curl -s -X POST $URL -H "Content-Type: application/json" \
  -d '{"jsonrpc":"2.0","id":2,"method":"tools/list","params":{}}'

# Wiki root
curl -s -X POST $URL -H "Content-Type: application/json" \
  -d '{"jsonrpc":"2.0","id":3,"method":"tools/call","params":{"name":"call","arguments":{}}}'

# Execute
curl -s -X POST $URL -H "Content-Type: application/json" \
  -d '{"jsonrpc":"2.0","id":4,"method":"tools/call","params":{"name":"call","arguments":{"method":"ping","args":{}}}}'
```

