#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using com.SSS135.EditorAutomation.Editor.API;
using com.SSS135.EditorAutomation.Recorder;

namespace com.SSS135.EditorAutomation.Editor.QueryEngine
{
    // The runtime namespace ends in 'Recorder', same as the static API class. From this
    // editor code the bare name would bind to the sibling namespace (a member of the
    // enclosing '...MCP'), so this alias must live INSIDE the namespace block to win
    // name lookup and map 'Recorder' back to the class.
    using Recorder = com.SSS135.EditorAutomation.Recorder.Recorder;

    /// <summary>
    /// Resolves a query's <c>session</c> argument to a <see cref="RecorderSession"/>:
    /// the literal <c>"live"</c> maps to the running in-memory session; anything
    /// else is treated as a file path or bare session id under the recordings
    /// directory and loaded read-only. Also enumerates discoverable sessions.
    /// </summary>
    public static class SessionResolver
    {
        /// <summary>Sentinel for the running in-memory session.</summary>
        public const string LiveToken = "live";

        /// <summary>Directory (relative to project root) holding session files; matches the runtime recorder.</summary>
        public const string RecordingsDir = "Temp/EditorAutomationRecordings";

        /// <summary>
        /// Resolve a session argument. <c>"live"</c> returns the active recorder
        /// session (throws if none is recording). A path or id is loaded from disk.
        /// </summary>
        public static RecorderSession Resolve(string session)
        {
            if (string.IsNullOrEmpty(session))
                throw new ArgumentException("'session' is required: pass \"live\" or a recording file path/id.");

            if (string.Equals(session, LiveToken, StringComparison.OrdinalIgnoreCase))
            {
                var live = Recorder.ActiveSession;
                if (live == null)
                    throw new InvalidOperationException(
                        "No live recording session is active. Enter Play mode (recording enabled) or query a file instead.");
                return live;
            }

            var path = ResolvePath(session);
            if (path == null)
                throw new FileNotFoundException($"Recording session not found: '{session}'.");

            return NdjsonSessionLoader.Load(path);
        }

        /// <summary>
        /// List discoverable sessions: every file in the recordings directory, plus
        /// the live session prepended when one is recording.
        /// </summary>
        public static IEnumerable<SessionInfo> ListSessions()
        {
            var live = Recorder.ActiveSession;
            if (live != null)
            {
                yield return new SessionInfo
                {
                    Id = live.SessionId ?? LiveToken,
                    Path = LiveToken,
                    Label = live.Label,
                    MinFrame = live.MinFrame == int.MaxValue ? 0 : live.MinFrame,
                    MaxFrame = live.MaxFrame == int.MinValue ? 0 : live.MaxFrame,
                    IsLive = true
                };
            }

            if (!Directory.Exists(RecordingsDir))
                yield break;

            foreach (var file in Directory.GetFiles(RecordingsDir, "session-*.ndjson")
                         .OrderByDescending(File.GetCreationTimeUtc))
            {
                yield return new SessionInfo
                {
                    Id = Path.GetFileNameWithoutExtension(file),
                    Path = Path.GetFullPath(file),
                    Label = null,
                    MinFrame = 0,
                    MaxFrame = 0,
                    IsLive = false
                };
            }
        }

        /// <summary>
        /// Map a session argument to an on-disk path: an existing absolute/relative
        /// path is used directly; otherwise it's treated as a bare id under the
        /// recordings directory (with or without the <c>.ndjson</c> extension).
        /// </summary>
        static string? ResolvePath(string session)
        {
            if (File.Exists(session))
                return session;

            var direct = Path.Combine(RecordingsDir, session);
            if (File.Exists(direct))
                return direct;

            var withExt = direct.EndsWith(".ndjson", StringComparison.OrdinalIgnoreCase)
                ? direct
                : direct + ".ndjson";
            return File.Exists(withExt) ? withExt : null;
        }
    }
}
