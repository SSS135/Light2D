#nullable enable
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using com.IvanMurzak.ReflectorNet.Utils;
using com.SSS135.EditorAutomation.Editor.Framework;
using com.SSS135.EditorAutomation.Editor.Framework.Builder;
using com.SSS135.EditorAutomation.Editor.Framework.Wiki;
using com.SSS135.EditorAutomation.Editor.Utils;
using UnityEditor;

namespace com.SSS135.EditorAutomation.Editor.Http
{
    /// <summary>
    /// Outcome of <see cref="RpcDispatcher.DispatchAsync"/>. The HTTP gateway uses
    /// <see cref="StatusCode"/> to decide between a normal 200 response (with
    /// <see cref="Body"/> populated) and a 202 + empty body for spec MCP notifications.
    /// </summary>
    public sealed class DispatchResult
    {
        public int StatusCode { get; }
        public JsonNode? Body { get; }

        public DispatchResult(int statusCode, JsonNode? body)
        {
            StatusCode = statusCode;
            Body = body;
        }

        public static DispatchResult Ok(JsonNode body) => new DispatchResult(200, body);

        /// <summary>HTTP 202 + empty body, used for <c>notifications/initialized</c>.</summary>
        public static DispatchResult Accepted() => new DispatchResult(202, null);
    }

    /// <summary>
    /// Dispatches a parsed JSON-RPC 2.0 request to the appropriate MCP protocol
    /// handler. Implements spec MCP <c>initialize</c> / <c>notifications/initialized</c> /
    /// <c>ping</c> / <c>tools/list</c> / <c>tools/call</c>, with the single MCP tool
    /// named <c>call</c> that wraps every dotted Editor Automation method id.
    /// </summary>
    /// <remarks>
    /// Per-param validation is performed dispatcher-side against the tool's
    /// <see cref="IMcpTool.InputSchema"/> <c>required</c> array before invocation.
    /// </remarks>
    public static class RpcDispatcher
    {
        public const string ProtocolVersion = "2025-06-18";

        // The single MCP tool descriptor exposed via tools/list. Dotted method id rides in
        // arguments.method; optional args ride in arguments.args. Omitted args = wiki mode.
        public const string CallToolName = "call";

        // Reentrancy depth counter. The marshal to the main thread already yields via TaskCompletionSource
        // routed through EditorApplication.update, so reentrant dispatch from a tool that itself issues
        // an HTTP request is unlikely to deadlock the listener (HttpListener accepts on worker threads).
        // Kept as a simple counter for visibility; the deferred queue below is wired but pass-through.
        // TODO_REENTRANCY: v2 — when a reentrant call is detected on the main thread, enqueue into
        // _deferred and drain on the next EditorApplication.update tick so a synchronous self-call cannot
        // wedge the editor update loop.
        static int _depth;
        static readonly ConcurrentQueue<Func<Task>> _deferred = new();

        static readonly WikiMarkdownEmitter _wikiEmitter = new WikiMarkdownEmitter();

        public static async Task<DispatchResult> DispatchAsync(JsonRpcParsed parsed, int timeoutMs, CancellationToken ct)
        {
            if (parsed.ParseErrorCode is int parseCode)
            {
                return DispatchResult.Ok(JsonRpc.BuildError(
                    id: null,
                    code: parseCode,
                    message: parsed.ParseErrorMessage ?? "Parse error.",
                    identifier: parsed.ParseErrorIdentifier));
            }

            // JSON-RPC 2.0 batch: route through DispatchBatchAsync. Each element is dispatched
            // sequentially through DispatchOneAsync; the loop stops on the first error response
            // or McpStatus.Processing result (fail-fast). Notifications consume no response slot.
            if (parsed.IsBatch)
            {
                if (parsed.Items.Count == 0)
                {
                    // JSON-RPC 2.0 §6: an empty batch is a single Invalid Request response,
                    // not an array.
                    return DispatchResult.Ok(JsonRpc.BuildError(
                        id: null,
                        code: JsonRpc.ErrInvalidRequest,
                        message: "Empty batch.",
                        identifier: JsonRpc.IdInvalidRequest));
                }

                return await DispatchBatchAsync(parsed, timeoutMs, ct).ConfigureAwait(false);
            }

            if (parsed.Items.Count == 0)
            {
                return DispatchResult.Ok(JsonRpc.BuildError(
                    id: null,
                    code: JsonRpc.ErrInvalidRequest,
                    message: "Empty request",
                    identifier: JsonRpc.IdInvalidRequest));
            }

            return await DispatchOneAsync(parsed.Items[0], timeoutMs, ct).ConfigureAwait(false);
        }

        /// <summary>
        /// Dispatch a JSON-RPC 2.0 batch array sequentially with fail-fast semantics. Notifications
        /// produce no response slot; request elements append their response and stop the loop on
        /// the first error envelope or <see cref="McpStatus.Processing"/> result. An all-notifications
        /// batch returns HTTP 202 + empty body (mirrors the single-notification path).
        /// </summary>
        static async Task<DispatchResult> DispatchBatchAsync(JsonRpcParsed parsed, int timeoutMs, CancellationToken ct)
        {
            var responses = new JsonArray();
            foreach (var item in parsed.Items)
            {
                var stepResult = await DispatchOneAsync(item, timeoutMs, ct).ConfigureAwait(false);

                // Notifications: side effects already executed inside DispatchOneAsync (it returns
                // DispatchResult.Accepted() with Body=null). Per JSON-RPC 2.0 §6, a notification
                // contributes no response object and cannot fail-fast the batch on its own.
                if (item.IsNotification || stepResult.Body == null)
                    continue;

                responses.Add(stepResult.Body.DeepClone());

                if (IsBatchFailFastResponse(stepResult.Body))
                    break;
            }

            // All-notifications batch (or fail-fast-from-empty edge): no JSON-RPC objects to emit.
            // Mirror HttpGateway's notifications/initialized handling (HTTP 202 + empty body).
            if (responses.Count == 0)
                return DispatchResult.Accepted();

            return DispatchResult.Ok(responses);
        }

        /// <summary>
        /// Return <c>true</c> when a batch step's response envelope should halt the batch:
        /// either an explicit JSON-RPC error envelope, or a successful tools/call result whose
        /// <c>status</c> field is <see cref="McpStatus.Processing"/> (async tools are not allowed
        /// to ride inside a batch — they break the sequential contract).
        /// </summary>
        static bool IsBatchFailFastResponse(JsonNode response)
        {
            if (response is not JsonObject obj) return false;

            if (obj["error"] is JsonObject) return true;

            // BuildToolCallResult stamps result.status with McpStatus.<value>.ToString().
            // Processing tools surface here; treat as fail-fast trigger.
            if (obj["result"] is JsonObject result
                && result["status"] is JsonValue statusValue
                && statusValue.TryGetValue<string>(out var statusStr)
                && string.Equals(statusStr, McpStatus.Processing.ToString(), StringComparison.Ordinal))
            {
                return true;
            }

            return false;
        }

        static async Task<DispatchResult> DispatchOneAsync(JsonRpcRequest req, int timeoutMs, CancellationToken ct)
        {
            // JSON-RPC 2.0 §4.1: a notification (no `id` member) receives no response.
            // We still execute the handler when it has meaningful side effects (e.g.
            // notifications/initialized signals readiness), then return HTTP 202 + empty body.
            if (req.IsNotification)
            {
                // Only notifications/initialized carries a defined effect; others are no-ops.
                return DispatchResult.Accepted();
            }

            if (string.IsNullOrEmpty(req.Method))
            {
                return DispatchResult.Ok(JsonRpc.BuildError(
                    id: req.Id,
                    code: JsonRpc.ErrInvalidRequest,
                    message: "method is required",
                    identifier: JsonRpc.IdInvalidRequest));
            }

            // Spec MCP protocol methods routed by name.
            switch (req.Method)
            {
                case "initialize":
                    return DispatchInitialize(req);

                case "notifications/initialized":
                    // Spec: server returns HTTP 202 + empty body; no JSON-RPC response object.
                    // Allowed even when the client incorrectly sent an id.
                    return DispatchResult.Accepted();

                case "ping":
                    return DispatchResult.Ok(JsonRpc.BuildSuccess(req.Id, new JsonObject()));

                case "tools/list":
                    return DispatchToolsList(req);

                case "tools/call":
                    return await DispatchToolsCallAsync(req, timeoutMs, ct).ConfigureAwait(false);
            }

            return DispatchResult.Ok(JsonRpc.BuildError(
                id: req.Id,
                code: JsonRpc.ErrMethodNotFound,
                message: $"Unknown protocol method: {req.Method}",
                identifier: JsonRpc.IdMethodNotFound));
        }

        // ── initialize ───────────────────────────────────────────────────────

        static DispatchResult DispatchInitialize(JsonRpcRequest req)
        {
            var result = new JsonObject
            {
                ["protocolVersion"] = ProtocolVersion,
                ["capabilities"] = new JsonObject
                {
                    ["tools"] = new JsonObject
                    {
                        ["listChanged"] = false,
                    }
                },
                ["serverInfo"] = new JsonObject
                {
                    ["name"] = "unity-mcp-http-gateway",
                    ["version"] = EditorAutomationVersion.Current,
                }
            };
            return DispatchResult.Ok(JsonRpc.BuildSuccess(req.Id, result));
        }

        // ── tools/list ───────────────────────────────────────────────────────

        static DispatchResult DispatchToolsList(JsonRpcRequest req)
        {
            // Single MCP tool descriptor. The dotted Editor Automation method id is passed inside
            // arguments.method; the per-method named arguments object is passed as arguments.args.
            // Omit arguments.args entirely to fetch the method's wiki markdown instead of executing.
            var schema = new JsonObject
            {
                ["type"] = "object",
                ["properties"] = new JsonObject
                {
                    ["method"] = new JsonObject
                    {
                        ["type"] = "string",
                        ["description"] = "Dotted Editor Automation method id (e.g. 'assets.refresh'). Omit or leave blank to fetch the root wiki index.",
                    },
                    ["args"] = new JsonObject
                    {
                        ["type"] = "object",
                        ["description"] = "Named parameters for the underlying method. Omit entirely to fetch the wiki page instead of executing.",
                        ["additionalProperties"] = true,
                    }
                },
                ["additionalProperties"] = false,
            };

            var tool = new JsonObject
            {
                ["name"] = CallToolName,
                ["description"] = "Invoke any Editor Automation method by dotted id, or fetch its wiki page when 'args' is omitted.",
                ["inputSchema"] = schema,
            };

            var tools = new JsonArray { tool };
            var result = new JsonObject { ["tools"] = tools };
            return DispatchResult.Ok(JsonRpc.BuildSuccess(req.Id, result));
        }

        // ── tools/call ───────────────────────────────────────────────────────

        static async Task<DispatchResult> DispatchToolsCallAsync(JsonRpcRequest req, int timeoutMs, CancellationToken ct)
        {
            var p = req.Params;
            if (p == null)
            {
                return DispatchResult.Ok(JsonRpc.BuildError(
                    id: req.Id,
                    code: JsonRpc.ErrInvalidParams,
                    message: "tools/call requires a 'params' object with 'name' and 'arguments'.",
                    identifier: JsonRpc.IdInvalidParams));
            }

            var toolName = p["name"]?.GetValue<string>();
            if (string.IsNullOrEmpty(toolName))
            {
                return DispatchResult.Ok(JsonRpc.BuildError(
                    id: req.Id,
                    code: JsonRpc.ErrInvalidParams,
                    message: "tools/call params.name is required.",
                    identifier: JsonRpc.IdInvalidParams));
            }
            if (toolName != CallToolName)
            {
                return DispatchResult.Ok(JsonRpc.BuildError(
                    id: req.Id,
                    code: JsonRpc.ErrMethodNotFound,
                    message: $"Unknown tool: {toolName}. The gateway exposes a single MCP tool named '{CallToolName}'.",
                    identifier: JsonRpc.IdMethodNotFound));
            }

            var arguments = p["arguments"] as JsonObject;
            var method = arguments?["method"]?.GetValue<string>() ?? string.Empty;
            var hasArgs = arguments != null && arguments.ContainsKey("args");
            var argsObj = hasArgs ? arguments!["args"] as JsonObject : null;

            // Argument-shape routing — same envelope, three modes:
            //   (1) no method + no args → wiki root reference (WikiGenerated~ dir + root page)
            //   (2) method only         → wiki page reference for the dotted method (or namespace)
            //   (3) method + args       → execute
            // Wiki modes return a path reference into the on-disk WikiGenerated~ tree (written by
            // WikiDiskGenerator on domain reload), not inline markdown — the caller reads / greps
            // the file. When the generated file isn't on disk yet (cold start before the first
            // reload), both modes fall back to the live inline render so discovery never breaks.
            if (string.IsNullOrEmpty(method) && !hasArgs)
                return DispatchResult.Ok(JsonRpc.BuildSuccess(req.Id, BuildWikiRootResult()));

            if (!hasArgs)
                return DispatchResult.Ok(JsonRpc.BuildSuccess(req.Id, BuildWikiPageResult(method)));

            return await ExecuteToolAsync(req, method, argsObj, timeoutMs, ct).ConfigureAwait(false);
        }

        static async Task<DispatchResult> ExecuteToolAsync(
            JsonRpcRequest req,
            string method,
            JsonObject? argsObj,
            int timeoutMs,
            CancellationToken ct)
        {
            if (string.IsNullOrEmpty(method))
            {
                return DispatchResult.Ok(JsonRpc.BuildError(
                    id: req.Id,
                    code: JsonRpc.ErrInvalidParams,
                    message: "tools/call execute mode requires arguments.method.",
                    identifier: JsonRpc.IdInvalidParams));
            }

            var tool = ToolRegistry.FindTool(method);
            if (tool == null)
            {
                return DispatchResult.Ok(JsonRpc.BuildError(
                    id: req.Id,
                    code: JsonRpc.ErrMethodNotFound,
                    message: $"Unknown method: {method}",
                    identifier: JsonRpc.IdMethodNotFound));
            }

            // Strip the spill opt-out marker if the caller placed it inside args. This must run
            // before we hand argsObj to the binder so the tool method never sees the transport flag.
            var skipSpill = HttpResponseSpill.ShouldSkipForParams(argsObj);

            // Auto-param validation BEFORE invocation: any required property absent or null in
            // argsObj produces -32602 INVALID_PARAMS with no side effects from the handler.
            if (TryValidateRequired(tool.InputSchema, argsObj, out var missing))
            {
                return DispatchResult.Ok(JsonRpc.BuildError(
                    id: req.Id,
                    code: JsonRpc.ErrInvalidParams,
                    message: $"Missing required parameter(s): {string.Join(", ", missing)}.",
                    identifier: JsonRpc.IdInvalidParams,
                    data: new JsonObject { ["missing"] = new JsonArray(missing.Select(m => (JsonNode)JsonValue.Create(m)!).ToArray()) }));
            }

            var docs = new List<JsonDocument>();
            try
            {
                var namedParameters = BuildNamedParameters(argsObj, docs);

                var settings = HttpGatewaySettings.instance;
                var maxTimeout = settings.HttpMaxTimeoutMs > 0 ? settings.HttpMaxTimeoutMs : 300_000;
                var effectiveTimeoutMs = Math.Min(timeoutMs > 0 ? timeoutMs : maxTimeout, maxTimeout);

                using var perCallCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                perCallCts.CancelAfter(effectiveTimeoutMs);

                var requestId = Guid.NewGuid().ToString("N");
                McpResponse response;
                Interlocked.Increment(ref _depth);
                try
                {
                    Task<McpResponse> innerTask;
                    if (tool.MainThreadRequired)
                    {
                        // Modal pre-check: if a native Win32 modal dialog is blocking the Editor's main
                        // thread, marshalling here would hang until the user (or another caller) dismisses
                        // it. Short-circuit with a structured payload so the caller can react — typically
                        // by calling system-editor-modal-respond. Worker-thread tools bypass this entirely
                        // (they don't touch the main thread), so the modal-detection tools themselves
                        // remain callable while a modal is up. Pre-check runs BEFORE the watchdog is armed
                        // — modals don't get woken; they need a button click.
                        var modalBlock = settings.DispatcherModalPreCheckEnabled
                            ? TryBuildModalBlockedResponse()
                            : null;
                        if (modalBlock != null)
                        {
                            innerTask = Task.FromResult(modalBlock);
                        }
                        else
                        {
                            // Main-thread-bound tools (the default) still marshal through MainThread.RunAsync.
                            // The watchdog wakes the Editor window if the marshal stalls past the configured
                            // threshold — the typical cause is an unfocused Editor with a pending recompile.
                            var marshal = MainThread.Instance.RunAsync(() => tool.InvokeAsync(requestId, namedParameters, perCallCts.Token));
                            StartWakeWatchdog(marshal, settings, perCallCts.Token);
                            innerTask = marshal.Unwrap();
                        }
                    }
                    else
                    {
                        // Worker-thread tools (opt-in via [McpTool(MainThreadRequired = false)]):
                        // invoke directly, no main-thread marshal, no watchdog. Tools that take this
                        // opt-out must be thread-safe and must not touch Unity APIs that require the
                        // main thread (AssetDatabase, Scene, GameObject mutation, etc.).
                        innerTask = tool.InvokeAsync(requestId, namedParameters, perCallCts.Token);
                    }
                    response = await innerTask.ConfigureAwait(false);
                    if (response == null)
                    {
                        return DispatchResult.Ok(JsonRpc.BuildError(
                            id: req.Id,
                            code: JsonRpc.ErrInternalError,
                            message: "Tool returned a null response.",
                            identifier: JsonRpc.IdInternalError));
                    }
                }
                catch (OperationCanceledException) when (perCallCts.IsCancellationRequested && !ct.IsCancellationRequested)
                {
                    return DispatchResult.Ok(JsonRpc.BuildError(
                        id: req.Id,
                        code: JsonRpc.ErrTimeout,
                        message: $"Request timed out after {effectiveTimeoutMs}ms (method={method}).",
                        identifier: JsonRpc.IdTimeout));
                }
                catch (Exception ex)
                {
                    return DispatchResult.Ok(JsonRpc.BuildError(
                        id: req.Id,
                        code: JsonRpc.ErrInternalError,
                        message: ex.Message,
                        identifier: JsonRpc.IdInternalError));
                }
                finally
                {
                    Interlocked.Decrement(ref _depth);
                }

                // Spec MCP tools/call uses in-band `isError: true` for handler-domain failures so the
                // client can still inspect content blocks. Map McpStatus.Error onto that shape.
                var isError = response.Status == McpStatus.Error;

                JsonNode? toolResultNode;
                try
                {
                    toolResultNode = BuildToolCallResult(response, isError);
                }
                catch (Exception ex)
                {
                    return DispatchResult.Ok(JsonRpc.BuildError(
                        id: req.Id,
                        code: JsonRpc.ErrInternalError,
                        message: $"Result serialization failed: {ex.Message}",
                        identifier: JsonRpc.IdInternalError));
                }

                // Processing responses carry a job-ticket id in their structuredContent; forward
                // as-is without spilling so the caller's eyes-on body is the synchronous ticket
                // payload (the final result arrives via system.job-status).
                if (response.Status == McpStatus.Processing)
                    return DispatchResult.Ok(JsonRpc.BuildSuccess(req.Id, toolResultNode!));

                var settingsAgain = HttpGatewaySettings.instance;
                var threshold = HttpResponseSpill.ClampThresholdCharacters(settingsAgain.HttpResponseSpillThresholdCharacters);
                var maybe = HttpResponseSpill.MaybeSpill(toolResultNode!, threshold, skipSpill);
                return DispatchResult.Ok(JsonRpc.BuildSuccess(req.Id, maybe));
            }
            catch (Exception ex)
            {
                return DispatchResult.Ok(JsonRpc.BuildError(
                    id: req.Id,
                    code: JsonRpc.ErrInternalError,
                    message: ex.Message,
                    identifier: JsonRpc.IdInternalError));
            }
            finally
            {
                foreach (var doc in docs)
                {
                    try { doc.Dispose(); } catch { }
                }
            }
        }

        /// <summary>
        /// Build the MCP spec <c>tools/call</c> result envelope (<c>content[]</c> + optional
        /// <c>structuredContent</c> + <c>isError</c>) from an <see cref="McpResponse"/>.
        /// </summary>
        static JsonNode BuildToolCallResult(McpResponse response, bool isError)
        {
            var result = new JsonObject();
            var contentArray = new JsonArray();

            if (response.Content != null)
            {
                foreach (var block in response.Content)
                {
                    if (block == null) continue;
                    var node = new JsonObject { ["type"] = block.Type ?? "text" };
                    if (block.Text != null) node["text"] = block.Text;
                    if (block.Data != null) node["data"] = block.Data;
                    if (block.MimeType != null) node["mimeType"] = block.MimeType;
                    contentArray.Add(node);
                }
            }

            result["content"] = contentArray;

            if (response.StructuredResult != null)
                result["structuredContent"] = response.StructuredResult.DeepClone();

            if (response.Metadata != null && response.Metadata.Count > 0)
                result["_meta"] = response.Metadata.DeepClone();

            if (isError)
                result["isError"] = true;

            // Stamp the per-call status so callers that care about Processing / Cancelled vs Success
            // can branch without re-parsing content. Spec MCP doesn't mandate this; harmless extra.
            result["status"] = response.Status.ToString();

            return result;
        }

        /// <summary>
        /// Validate that every property listed under <see cref="IMcpTool.InputSchema"/>'s
        /// <c>required</c> array is present (and non-null) in the supplied args bag. Returns
        /// <c>true</c> when any required property is missing; <paramref name="missing"/> lists
        /// the offending names in declaration order.
        /// </summary>
        static bool TryValidateRequired(JsonNode? inputSchema, JsonObject? argsObj, out List<string> missing)
        {
            missing = new List<string>();
            if (inputSchema is not JsonObject schemaObj)
                return false;
            if (schemaObj["required"] is not JsonArray requiredArr || requiredArr.Count == 0)
                return false;

            foreach (var item in requiredArr)
            {
                var name = item?.GetValue<string>();
                if (string.IsNullOrEmpty(name))
                    continue;

                if (argsObj == null)
                {
                    missing.Add(name!);
                    continue;
                }

                if (!argsObj.TryGetPropertyValue(name!, out var node) || node is null)
                    missing.Add(name!);
            }

            return missing.Count > 0;
        }

        // ── Wiki helpers ─────────────────────────────────────────────────────

        // Returned on every wiki-mode response so callers prefer reading/grepping the on-disk wiki
        // tree over round-tripping the MCP for docs.
        const string WikiUsageHint =
            "Editor Automation docs are plain files in the 'WikiGenerated~' folder. Prefer filesystem tools " +
            "(Read/Grep/Glob) on it over MCP doc requests; use call(method, args) only to execute.";

        /// <summary>
        /// Build the root wiki-mode result. When the generated tree exists on disk, returns a
        /// structured reference (<c>wiki</c> = the WikiGenerated~ dir, <c>root</c> = the root page,
        /// plus a usage <c>hint</c>) so the caller reads / greps the tree instead of receiving the
        /// whole index inline. Falls back to the live inline render of the root index when the
        /// generated root page isn't on disk yet (cold start before the first domain reload).
        /// </summary>
        static JsonObject BuildWikiRootResult()
        {
            var dir = WikiDiskGenerator.OutputDirectory;
            var rootPath = WikiDiskGenerator.PagePath(WikiDiskGenerator.RootIndexSlug);

            if (dir != null && rootPath != null && File.Exists(rootPath))
            {
                var reference = new JsonObject
                {
                    ["wiki"] = dir,
                    ["root"] = rootPath,
                    ["hint"] = WikiUsageHint,
                };
                return WrapStructured(reference);
            }

            // Cold start: no generated tree yet — serve the live index inline so discovery works.
            return WrapMarkdown(RenderWikiMarkdown(string.Empty));
        }

        /// <summary>
        /// Build the per-page wiki-mode result for <paramref name="method"/> (a dotted method,
        /// namespace, or topic slug). When the generated page exists on disk, returns a structured
        /// <c>{ page: &lt;abs path&gt;, wiki: &lt;dir&gt;, hint }</c> reference. Falls back to the live inline render when the
        /// page isn't on disk yet (cold start) or has no generated file (e.g. an unknown method,
        /// which the live render answers with a not-found page).
        /// </summary>
        static JsonObject BuildWikiPageResult(string method)
        {
            var pagePath = WikiDiskGenerator.PagePath(method);
            if (pagePath != null && File.Exists(pagePath))
            {
                var reference = new JsonObject
                {
                    ["page"] = pagePath,
                    ["wiki"] = WikiDiskGenerator.OutputDirectory,
                    ["hint"] = WikiUsageHint,
                };
                return WrapStructured(reference);
            }

            // Cold start or unknown slug: serve the live render inline.
            return WrapMarkdown(RenderWikiMarkdown(method));
        }

        /// <summary>
        /// Wrap a structured wiki reference as a spec MCP <c>tools/call</c> result: a JSON text
        /// content block (so plain clients still see the paths) plus <c>structuredContent</c>
        /// carrying the same object for clients that parse it.
        /// </summary>
        static JsonObject WrapStructured(JsonObject reference)
        {
            var content = new JsonArray
            {
                new JsonObject
                {
                    ["type"] = "text",
                    ["text"] = reference.ToJsonString(),
                    ["mimeType"] = "application/json",
                }
            };
            return new JsonObject
            {
                ["content"] = content,
                ["structuredContent"] = reference.DeepClone(),
            };
        }

        /// <summary>
        /// Wrap rendered wiki markdown as a single text content block — the cold-start fallback
        /// shape, identical to the pre-path-reference wiki-mode response.
        /// </summary>
        static JsonObject WrapMarkdown(string markdown)
        {
            var content = new JsonArray
            {
                new JsonObject
                {
                    ["type"] = "text",
                    ["text"] = markdown ?? string.Empty,
                    ["mimeType"] = "text/markdown",
                }
            };
            return new JsonObject { ["content"] = content };
        }

        static string RenderWikiMarkdown(string method)
        {
            if (string.IsNullOrEmpty(method) || method == ".")
                return RenderRootIndex();

            // Topic pages (standalone walkthroughs under Docs~/WikiSource/<slug>.md whose
            // slug doesn't collide with any registered tool / namespace) are routed first so
            // a dotted slug like `assets.dump-quickstart` still reaches the topic renderer
            // without being misclassified as an unknown method.
            if (ToolRegistry.IsTopicPage(method))
                return _wikiEmitter.RenderTopicPage(method);

            if (method.IndexOf('.') < 0)
                return RenderNamespacePage(method);

            var tool = ToolRegistry.FindTool(method);
            if (tool == null)
                return BuildNotFoundPage(method);

            return _wikiEmitter.Render(tool);
        }

        static string RenderRootIndex()
        {
            var summary = ToolRegistry.NamespaceSummary();
            var namespaces = new List<NamespaceInfo>(summary.Count);
            foreach (var entry in summary)
            {
                var dottedNames = ToolRegistry.MethodsInNamespace(entry.Key);
                var tools = new List<IMcpTool>(dottedNames.Count);
                foreach (var dotted in dottedNames)
                {
                    var tool = ToolRegistry.FindTool(dotted);
                    if (tool != null)
                        tools.Add(tool);
                }
                namespaces.Add(new NamespaceInfo(entry.Key, tools));
            }
            return _wikiEmitter.RenderRootIndex(namespaces);
        }

        static string RenderNamespacePage(string ns)
        {
            var dottedNames = ToolRegistry.MethodsInNamespace(ns);
            if (dottedNames.Count == 0)
                return BuildNotFoundPage(ns);

            var tools = new List<IMcpTool>(dottedNames.Count);
            foreach (var dotted in dottedNames)
            {
                var tool = ToolRegistry.FindTool(dotted);
                if (tool != null)
                    tools.Add(tool);
            }
            return _wikiEmitter.RenderNamespacePage(ns, tools);
        }

        /// <summary>
        /// Render a markdown not-found page with the top fuzzy suggestions from the registry's
        /// suggestion pool. Wave 4's WikiOverlay merges topic-page slugs into the same pool.
        /// </summary>
        static string BuildNotFoundPage(string query)
        {
            var suggestions = RankSuggestions(query ?? string.Empty, 5);

            var sb = new StringBuilder();
            sb.AppendLine($"# Not found: `{query}`");
            sb.AppendLine();
            sb.AppendLine($"No registered method or namespace matches `{query}`.");
            sb.AppendLine();

            if (suggestions.Count == 0)
            {
                sb.AppendLine("No close matches. Call `tools/call` with `arguments = { }` to read the root index.");
            }
            else
            {
                sb.AppendLine("Did you mean:");
                sb.AppendLine();
                foreach (var s in suggestions)
                    sb.AppendLine($"- `{s}`");
                sb.AppendLine();
                sb.AppendLine("Read any of these by calling `tools/call` with `arguments = { method: \"<name>\" }` and no `args`.");
            }

            return sb.ToString();
        }

        /// <summary>
        /// Two-tier fuzzy ranking over the registry's suggestion pool:
        ///   Tier 1: pool entries containing the lowercase query as a substring (shorter wins).
        ///   Tier 2: Levenshtein-similarity (1 - dist/maxLen) >= 0.5, ranked by similarity desc.
        /// </summary>
        static IReadOnlyList<string> RankSuggestions(string query, int maxResults)
        {
            if (string.IsNullOrEmpty(query) || maxResults <= 0)
                return Array.Empty<string>();

            var pool = ToolRegistry.BuildSuggestionPool(includeTopicPages: true);
            var q = query.ToLowerInvariant();

            var substringHits = new List<(string name, int len)>();
            var fuzzyHits = new List<(string name, double score)>();

            foreach (var candidate in pool)
            {
                var cand = candidate.ToLowerInvariant();
                if (cand.Contains(q))
                {
                    substringHits.Add((candidate, cand.Length));
                    continue;
                }
                var score = LevenshteinSimilarity(q, cand);
                if (score >= 0.5)
                    fuzzyHits.Add((candidate, score));
            }

            substringHits.Sort((a, b) =>
            {
                var c = a.len.CompareTo(b.len);
                return c != 0 ? c : string.Compare(a.name, b.name, StringComparison.OrdinalIgnoreCase);
            });
            fuzzyHits.Sort((a, b) =>
            {
                var c = b.score.CompareTo(a.score);
                return c != 0 ? c : string.Compare(a.name, b.name, StringComparison.OrdinalIgnoreCase);
            });

            var results = new List<string>(maxResults);
            foreach (var hit in substringHits)
            {
                if (results.Count >= maxResults) break;
                results.Add(hit.name);
            }
            foreach (var hit in fuzzyHits)
            {
                if (results.Count >= maxResults) break;
                if (!results.Contains(hit.name))
                    results.Add(hit.name);
            }
            return results;
        }

        static double LevenshteinSimilarity(string a, string b)
        {
            if (a.Length == 0 && b.Length == 0) return 1.0;
            var dist = LevenshteinDistance(a, b);
            var max = Math.Max(a.Length, b.Length);
            return 1.0 - (double)dist / max;
        }

        static int LevenshteinDistance(string a, string b)
        {
            if (a.Length == 0) return b.Length;
            if (b.Length == 0) return a.Length;

            var prev = new int[b.Length + 1];
            var curr = new int[b.Length + 1];
            for (var j = 0; j <= b.Length; j++) prev[j] = j;

            for (var i = 1; i <= a.Length; i++)
            {
                curr[0] = i;
                for (var j = 1; j <= b.Length; j++)
                {
                    var cost = a[i - 1] == b[j - 1] ? 0 : 1;
                    curr[j] = Math.Min(Math.Min(curr[j - 1] + 1, prev[j] + 1), prev[j - 1] + cost);
                }
                (prev, curr) = (curr, prev);
            }
            return prev[b.Length];
        }

        static IReadOnlyDictionary<string, JsonElement> BuildNamedParameters(JsonObject? @params, List<JsonDocument> docs)
        {
            var dict = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
            if (@params == null)
                return dict;

            foreach (var kvp in @params)
            {
                var json = kvp.Value?.ToJsonString() ?? "null";
                var bytes = Encoding.UTF8.GetBytes(json);
                var doc = JsonDocument.Parse(bytes);
                docs.Add(doc);
                // Clone the element so it survives JsonDocument.Dispose() if Dispose ever happens before await.
                dict[kvp.Key] = doc.RootElement.Clone();
            }
            return dict;
        }

        // Pass-through stub for the deferred queue. See TODO_REENTRANCY above.
        internal static void EnqueueDeferred(Func<Task> work) => _ = work();

        // Returns a structured "editor_modal_blocked" McpResponse if any native Win32 modal dialog is
        // currently blocking the Editor's main thread; otherwise null. Enumeration is cheap (sub-ms)
        // and safe from any thread. Detects native dialogs only (e.g. EditorUtility.DisplayDialog) —
        // IMGUI-rendered modal EditorWindows have no Win32 identity and are invisible to this check.
        static McpResponse? TryBuildModalBlockedResponse()
        {
            var modals = EditorWin32.TryEnumModalDialogs();
            if (modals.Count == 0) return null;

            var modalSummary = new JsonArray();
            foreach (var m in modals)
            {
                var buttons = new JsonArray();
                foreach (var b in m.Buttons) buttons.Add(b.Text);
                modalSummary.Add(new JsonObject
                {
                    ["hwnd"] = m.Hwnd.ToInt64().ToString(),
                    ["title"] = m.Title,
                    ["buttons"] = buttons,
                });
            }
            return McpResponse.SuccessStructured(new JsonObject
            {
                ["status"] = "editor_modal_blocked",
                ["dialogs"] = modalSummary,
                ["hint"] = "A native Unity Editor modal dialog is blocking the main thread. Call system-editor-modal-respond to dismiss it, or wait for a user to click it manually.",
            });
        }

        // Best-effort watchdog: when a main-thread marshal stalls, wake the Editor window so the
        // EditorApplication.update loop resumes ticking and the marshal can complete. Cheap
        // worker-thread fire-and-forget; failure to wake is non-fatal (the request still times out
        // via perCallCts). Modes:
        //   Never  — no-op (legacy behavior).
        //   Always — wake immediately, in parallel with the marshal.
        //   OnStall — wake only if the marshal hasn't completed within AutoWakeStallMilliseconds.
        static void StartWakeWatchdog(Task marshal, HttpGatewaySettings settings, CancellationToken ct)
        {
            if (settings.AutoWakeMode == AutoWakeMode.Never) return;
            if (settings.AutoWakeMode == AutoWakeMode.Always)
            {
                EditorWin32.TryWakeEditor();
                return;
            }
            // OnStall: race the marshal against a delay; if the delay wins, the marshal is stuck.
            _ = Task.Run(async () =>
            {
                try
                {
                    var stallTask = Task.Delay(Math.Max(50, settings.AutoWakeStallMilliseconds), ct);
                    var completed = await Task.WhenAny(marshal, stallTask).ConfigureAwait(false);
                    if (completed == stallTask && !marshal.IsCompleted)
                        EditorWin32.TryWakeEditor();
                }
                catch (OperationCanceledException) { /* request cancelled — swallow */ }
            }, ct);
        }
    }
}
