#nullable enable
using System;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Json.Nodes;

namespace com.SSS135.EditorAutomation.Editor.Http
{
    public static class HttpResponseSpill
    {
        public const string OptOutParamKey = "__unityMcpHttpInternal_clientHandlesLargeResponses_skipHttpSpill_v1";
        public const int    MinThresholdCharacters = 1024;
        public const double RetentionHours = 24.0;

        const string DateTimeDirFormat = "yyyyMMddTHHmmssZ";
        const string FileStampFormat   = "yyyyMMddTHHmmss";

        // Unity Editor's CWD is always the project root, so Library/... is project-relative.
        public static string ResponsesRoot { get; } =
            Path.Combine(Directory.GetCurrentDirectory(), "Library", "EditorAutomationHttp", "responses");

        public static int ClampThresholdCharacters(int chars)
            => chars < MinThresholdCharacters ? MinThresholdCharacters : chars;

        public static bool ShouldSkipForParams(JsonObject? @params)
        {
            if (@params is null)
                return false;

            if (!@params.TryGetPropertyValue(OptOutParamKey, out var node) || node is null)
                return false;

            var skip = false;
            if (node is JsonValue value)
            {
                if (value.TryGetValue<bool>(out var b))
                    skip = b;
                else if (value.TryGetValue<string>(out var s)
                         && bool.TryParse(s, out var parsed))
                    skip = parsed;
            }

            // Strip the marker so downstream tool handlers never observe the transport-only flag.
            @params.Remove(OptOutParamKey);
            return skip;
        }

        public static JsonNode MaybeSpill(JsonNode payload, int threshold, bool skipSpill)
        {
            var clampedThreshold = ClampThresholdCharacters(threshold);
            if (skipSpill)
                return payload;

            var body = payload.ToJsonString();
            if (body.Length <= clampedThreshold)
                return payload;

            if (!TryWriteSpill(body, out var absPath))
                return payload; // Best effort: if disk write fails, send the oversized payload anyway.

            var file = new JsonObject
            {
                ["path"] = absPath,
                ["contentType"] = "application/json",
                ["characters"] = body.Length,
                ["threshold"] = clampedThreshold
            };

            return new JsonObject
            {
                ["outputTooLong"] = true,
                ["message"] = "Response was written to a file because it exceeded the direct HTTP response threshold.",
                ["file"] = file
            };
        }

        public static void PruneOld()
        {
            if (!Directory.Exists(ResponsesRoot))
                return;

            var now = DateTime.UtcNow;
            string[] dirs;
            try
            {
                dirs = Directory.GetDirectories(ResponsesRoot);
            }
            catch
            {
                return;
            }

            foreach (var dir in dirs)
            {
                try
                {
                    var name = Path.GetFileName(dir);
                    if (!DateTime.TryParseExact(name, DateTimeDirFormat,
                            CultureInfo.InvariantCulture,
                            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                            out var stamp))
                        continue;

                    if ((now - stamp).TotalHours <= RetentionHours)
                        continue;

                    Directory.Delete(dir, recursive: true);
                }
                catch
                {
                    // Per-directory failures must not stop the sweep.
                }
            }
        }

        static bool TryWriteSpill(string body, out string absPath)
        {
            absPath = string.Empty;
            try
            {
                var nowUtc = DateTime.UtcNow;
                var dirName = nowUtc.ToString(DateTimeDirFormat, CultureInfo.InvariantCulture);
                var dir = Path.Combine(ResponsesRoot, dirName);
                Directory.CreateDirectory(dir);

                var stamp = nowUtc.ToString(FileStampFormat, CultureInfo.InvariantCulture);
                var guid = Guid.NewGuid().ToString("N");
                var fileName = $"{stamp}_{guid}.json";
                var finalPath = Path.Combine(dir, fileName);
                var tmpPath = finalPath + ".tmp";

                File.WriteAllText(tmpPath, body, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
                // 2-arg File.Move is portable across Unity's .NET Standard 2.1; default behavior already fails on overwrite.
                File.Move(tmpPath, finalPath);

                absPath = Path.GetFullPath(finalPath);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
