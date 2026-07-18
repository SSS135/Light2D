#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;

namespace com.SSS135.EditorAutomation.Editor.Framework
{
    /// <summary>
    /// One content block returned by a tool: text / image / audio / resource.
    /// Mirrors the legacy <c>ContentBlock</c>; the dispatcher serializes these into
    /// the spec MCP <c>content[]</c> array.
    /// </summary>
    public sealed class McpContent
    {
        /// <summary>
        /// Content type — one of <c>"text"</c>, <c>"image"</c>, <c>"audio"</c>, <c>"resource"</c>.
        /// </summary>
        public string Type { get; set; } = "text";

        /// <summary>Text payload for <c>"text"</c> blocks.</summary>
        public string? Text { get; set; }

        /// <summary>Base64-encoded payload for <c>"image"</c> and <c>"audio"</c> blocks.</summary>
        public string? Data { get; set; }

        /// <summary>MIME type for the payload (e.g. <c>"text/plain"</c>, <c>"image/png"</c>).</summary>
        public string? MimeType { get; set; }

        public static McpContent CreateText(string? text, string mimeType = "text/plain")
            => new McpContent { Type = "text", Text = text, MimeType = mimeType };

        public static McpContent CreateImage(byte[] data, string mimeType)
            => new McpContent { Type = "image", Data = Convert.ToBase64String(data), MimeType = mimeType };

        public static McpContent CreateImageBase64(string base64Data, string mimeType)
            => new McpContent { Type = "image", Data = base64Data, MimeType = mimeType };

        public static McpContent CreateAudio(byte[] data, string mimeType)
            => new McpContent { Type = "audio", Data = Convert.ToBase64String(data), MimeType = mimeType };

        public static McpContent CreateAudioBase64(string base64Data, string mimeType)
            => new McpContent { Type = "audio", Data = base64Data, MimeType = mimeType };
    }

    /// <summary>
    /// The result envelope returned by every <see cref="IMcpTool.InvokeAsync"/> call.
    /// Mirrors the legacy <c>ResponseCallTool</c> + <c>ResponseStatus</c> shape with an
    /// added <see cref="McpStatus.Cancelled"/> state for the job-ticket layer.
    /// </summary>
    /// <remarks>
    /// Field semantics:
    /// <list type="bullet">
    ///   <item><see cref="Status"/> — lifecycle of this call.</item>
    ///   <item><see cref="StructuredResult"/> — optional structured JSON payload.
    ///   The dispatcher mirrors this under MCP's <c>structuredContent</c> and also stamps the
    ///   serialized JSON as the leading text content block for back-compat.</item>
    ///   <item><see cref="Content"/> — optional text / image / audio content blocks.</item>
    ///   <item><see cref="Metadata"/> — optional bag of out-of-band fields (e.g. job ticket id).</item>
    ///   <item><see cref="ErrorCode"/> — optional in-band error code on <see cref="McpStatus.Error"/>
    ///   responses; surfaced via the dispatcher's JSON-RPC error path when set.</item>
    /// </list>
    /// </remarks>
    public sealed class McpResponse
    {
        public McpStatus Status { get; set; } = McpStatus.Error;

        public JsonNode? StructuredResult { get; set; }

        public List<McpContent> Content { get; set; } = new List<McpContent>();

        public JsonObject? Metadata { get; set; }

        public int? ErrorCode { get; set; }

        public McpResponse() { }

        public McpResponse(McpStatus status, List<McpContent>? content = null, JsonNode? structuredResult = null)
        {
            Status = status;
            Content = content ?? new List<McpContent>();
            StructuredResult = structuredResult;
        }

        /// <summary>
        /// First text content block's message if present, otherwise null.
        /// </summary>
        public string? GetMessage() => Content
            ?.FirstOrDefault(b => b.Type == "text" && !string.IsNullOrEmpty(b.Text))
            ?.Text;

        // ---------- Static factories (mirror ResponseCallTool.Static.cs) ----------

        public static McpResponse Success(string? message = null)
            => new McpResponse(
                status: McpStatus.Success,
                content: message == null
                    ? new List<McpContent>()
                    : new List<McpContent> { McpContent.CreateText(message) });

        /// <summary>
        /// Success response carrying a structured JSON result plus optional text content blocks.
        /// The structured payload is also stamped as a JSON text block for spec back-compat
        /// (MCP 2025-06-18 server/tools spec — <c>structuredContent</c>).
        /// </summary>
        public static McpResponse Success(IEnumerable<McpContent>? messages, JsonNode? jsonResult)
        {
            var content = messages == null ? new List<McpContent>() : messages.ToList();
            if (jsonResult != null)
            {
                content.Add(new McpContent
                {
                    Type = "text",
                    Text = jsonResult.ToJsonString(),
                    MimeType = "text/json",
                });
            }
            return new McpResponse(
                status: McpStatus.Success,
                content: content,
                structuredResult: jsonResult);
        }

        public static McpResponse SuccessStructured(JsonNode? structuredResult)
            => Success(messages: null, jsonResult: structuredResult);

        /// <summary>
        /// Processing response — the call has been accepted and will complete later via a job ticket.
        /// The <paramref name="immediateTicketBody"/> is whatever payload (typically the ticket id)
        /// the caller hands back synchronously.
        /// </summary>
        public static McpResponse Processing(string? immediateTicketBody = null)
            => new McpResponse(
                status: McpStatus.Processing,
                content: immediateTicketBody == null
                    ? new List<McpContent>()
                    : new List<McpContent> { McpContent.CreateText(immediateTicketBody) });

        public static McpResponse Processing(JsonNode? structuredTicket)
            => new McpResponse(
                status: McpStatus.Processing,
                structuredResult: structuredTicket);

        public static McpResponse Error(string? message = null, int? code = null)
            => new McpResponse(
                status: McpStatus.Error,
                content: new List<McpContent> { McpContent.CreateText(message) })
            {
                ErrorCode = code,
            };

        public static McpResponse Error(Exception exception, int? code = null)
            => Error($"[Error] {exception?.Message}\n{exception?.StackTrace}", code);

        public static McpResponse Cancelled(string? message = null)
            => new McpResponse(
                status: McpStatus.Cancelled,
                content: message == null
                    ? new List<McpContent>()
                    : new List<McpContent> { McpContent.CreateText(message) });
    }
}
