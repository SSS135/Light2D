#nullable enable
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace com.SSS135.EditorAutomation.Editor.Http
{
    /// <summary>
    /// A parsed JSON-RPC 2.0 request. <see cref="Method"/> is the protocol method
    /// (e.g. <c>tools/call</c>); <see cref="Params"/> is the <c>params</c> object
    /// (always either a JsonObject or null per spec; arrays / scalars are rejected
    /// at the parse stage). <see cref="IsNotification"/> reflects the absence of an
    /// <c>id</c> member per JSON-RPC 2.0 §4.1.
    /// </summary>
    public sealed class JsonRpcRequest
    {
        public string Method = "";
        public JsonObject? Params;
        public JsonNode? Id;
        public bool HasId;
        public bool IsNotification => !HasId;
    }

    public sealed class JsonRpcParsed
    {
        public List<JsonRpcRequest> Items = new();
        public bool IsBatch;
        public int? ParseErrorCode;
        public string? ParseErrorMessage;
        public string? ParseErrorIdentifier;
    }

    /// <summary>
    /// JSON-RPC 2.0 envelope parsing and response builders, matching the spec MCP
    /// wire shape used by the in-Editor HTTP gateway. Replaces the legacy bare
    /// <c>{method, params}</c> envelope on <c>/rpc</c>.
    /// </summary>
    /// <remarks>
    /// Error codes are spec integers (<see cref="ErrParseError"/> = -32700, etc.).
    /// The original string identifier (e.g. <c>"PARSE_ERROR"</c>) is preserved as
    /// <c>error.data.identifier</c> so downstream consumers can branch on a stable
    /// human-readable label without losing the integer wire code.
    /// <para/>
    /// <see cref="JsonRpcRequest.Id"/> is type-preserving — a string id round-trips as
    /// a string, a number id as a number, <c>null</c> stays <c>null</c>. The parser
    /// distinguishes "id absent" (notification) from "id explicitly null" (request
    /// with null id) via <see cref="JsonRpcRequest.HasId"/>.
    /// </remarks>
    public static class JsonRpc
    {
        public const string JsonRpcVersion = "2.0";

        // Spec error codes (JSON-RPC 2.0 §5.1).
        public const int ErrParseError       = -32700;
        public const int ErrInvalidRequest   = -32600;
        public const int ErrMethodNotFound   = -32601;
        public const int ErrInvalidParams    = -32602;
        public const int ErrInternalError    = -32603;
        // Custom server-error codes (in the -32000 .. -32099 reserved range).
        public const int ErrTimeout          = -32001;

        // Stable identifiers carried in `error.data.identifier` for callers that want to
        // branch on a human label instead of the integer wire code.
        public const string IdParseError       = "PARSE_ERROR";
        public const string IdInvalidRequest   = "INVALID_REQUEST";
        public const string IdMethodNotFound   = "METHOD_NOT_FOUND";
        public const string IdInvalidParams    = "INVALID_PARAMS";
        public const string IdInternalError    = "INTERNAL_ERROR";
        public const string IdTimeout          = "TIMEOUT";

        public static JsonRpcParsed ParseBody(string body)
        {
            var parsed = new JsonRpcParsed();
            var trimmed = (body ?? string.Empty).TrimStart();

            if (trimmed.Length == 0)
            {
                parsed.ParseErrorCode = ErrParseError;
                parsed.ParseErrorIdentifier = IdParseError;
                parsed.ParseErrorMessage = "Empty request body";
                return parsed;
            }

            JsonNode? root;
            try
            {
                // JsonNode.Parse copies into managed nodes so no JsonDocument lifetime leak.
                root = JsonNode.Parse(trimmed);
            }
            catch (JsonException ex)
            {
                parsed.ParseErrorCode = ErrParseError;
                parsed.ParseErrorIdentifier = IdParseError;
                parsed.ParseErrorMessage = ex.Message;
                return parsed;
            }

            if (root is JsonArray array)
            {
                // JSON-RPC 2.0 batch: parse every element into a JsonRpcRequest. The dispatcher
                // handles empty-array rejection and per-element fail-fast semantics; we only
                // flag the shape here.
                parsed.IsBatch = true;
                foreach (var element in array)
                    parsed.Items.Add(ParseOne(element));
                return parsed;
            }

            parsed.Items.Add(ParseOne(root));
            return parsed;
        }

        static JsonRpcRequest ParseOne(JsonNode? node)
        {
            var req = new JsonRpcRequest();

            if (node is not JsonObject obj)
                return req; // Method stays empty → dispatcher returns INVALID_REQUEST.

            if (obj.TryGetPropertyValue("method", out var methodNode) && methodNode is JsonValue mv
                && mv.TryGetValue<string>(out var methodStr) && !string.IsNullOrEmpty(methodStr))
            {
                req.Method = methodStr;
            }

            // Distinguish "id absent" (notification) from "id: null" (request with null id).
            if (obj.ContainsKey("id"))
            {
                req.HasId = true;
                if (obj.TryGetPropertyValue("id", out var idNode))
                    req.Id = idNode?.DeepClone();
            }

            // `params` is always a JsonObject when present (arrays / scalars become empty bag);
            // omitted entirely means null.
            if (obj.ContainsKey("params"))
            {
                if (obj.TryGetPropertyValue("params", out var pn) && pn is JsonObject po)
                    req.Params = (JsonObject)po.DeepClone();
                else
                    req.Params = new JsonObject();
            }

            return req;
        }

        /// <summary>
        /// Build a JSON-RPC 2.0 success envelope. The <paramref name="id"/> is echoed
        /// type-preservingly; null is allowed (matches "id: null" requests).
        /// </summary>
        public static JsonObject BuildSuccess(JsonNode? id, JsonNode? result)
        {
            var resp = new JsonObject
            {
                ["jsonrpc"] = JsonRpcVersion,
                ["id"] = id?.DeepClone(),
                ["result"] = result?.DeepClone() ?? new JsonObject()
            };
            return resp;
        }

        /// <summary>
        /// Build a JSON-RPC 2.0 error envelope with an integer wire code and an optional
        /// string <paramref name="identifier"/> stamped into <c>error.data.identifier</c>.
        /// Additional caller-supplied <paramref name="data"/> fields are merged into
        /// <c>error.data</c>; the identifier takes precedence on key collision.
        /// </summary>
        public static JsonObject BuildError(JsonNode? id, int code, string message, string? identifier = null, JsonNode? data = null)
        {
            var err = new JsonObject
            {
                ["code"] = code,
                ["message"] = message
            };

            JsonObject? dataObj = null;
            if (data is JsonObject existingObj)
                dataObj = (JsonObject)existingObj.DeepClone();
            else if (data is not null)
                dataObj = new JsonObject { ["value"] = data.DeepClone() };

            if (!string.IsNullOrEmpty(identifier))
            {
                dataObj ??= new JsonObject();
                dataObj["identifier"] = identifier;
            }

            if (dataObj is not null)
                err["data"] = dataObj;

            var resp = new JsonObject
            {
                ["jsonrpc"] = JsonRpcVersion,
                ["id"] = id?.DeepClone(),
                ["error"] = err
            };
            return resp;
        }

        public static string Serialize(JsonNode node) => node.ToJsonString();
    }
}
