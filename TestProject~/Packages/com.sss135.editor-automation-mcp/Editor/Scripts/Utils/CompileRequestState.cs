#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using com.SSS135.EditorAutomation.Editor.Framework;
using com.SSS135.EditorAutomation.Editor.Framework.Jobs;
using UnityEditor;
using UnityEditor.Compilation;

namespace com.SSS135.EditorAutomation.Editor.Utils
{
    [InitializeOnLoad]
    public static class CompileRequestState
    {
        const string DirRel = "Library/EditorAutomationHttp/compile";
        const string StateFileRel = "Library/EditorAutomationHttp/compile/request.json";

        static readonly object _lock = new();
        static bool _tickScheduled;

        static string DirectoryPath { get; } =
            Path.Combine(Directory.GetCurrentDirectory(), DirRel.Replace('/', Path.DirectorySeparatorChar));

        static string StateFilePath { get; } =
            Path.Combine(Directory.GetCurrentDirectory(), StateFileRel.Replace('/', Path.DirectorySeparatorChar));

        static CompileRequestState()
        {
            CompilationPipeline.compilationStarted += OnCompilationStarted;
            CompilationPipeline.assemblyCompilationFinished += OnAssemblyCompilationFinished;
            CompilationPipeline.compilationFinished += OnCompilationFinished;

            if (TryLoad(out var state) && state != null && !state.Completed)
                ScheduleTick();
        }

        public static bool TryGetPendingTicket(out string ticketId)
        {
            ticketId = string.Empty;
            if (!TryLoad(out var state) || state == null || state.Completed)
                return false;

            ticketId = state.TicketId ?? string.Empty;
            return !string.IsNullOrEmpty(ticketId);
        }

        public static void Start(string ticketId)
        {
            var state = new StateData
            {
                TicketId = ticketId,
                RequestedAtUtc = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"),
                StatusReason = "pending_refresh",
                Diagnostics = new List<DiagnosticData>(),
            };
            Save(state);
            ScheduleTick();
        }

        public static void RunNormalRefresh(string ticketId)
        {
            if (!TryLoad(out var state) || state == null || state.Completed || state.TicketId != ticketId)
                return;

            state.RefreshStarted = true;
            state.StatusReason = "refreshing_asset_database";
            Save(state);

            try
            {
                AssetDatabase.Refresh();
            }
            catch (Exception ex)
            {
                state.StatusReason = "refresh_failed";
                state.Completed = true;
                Save(state);
                JobRegistry.Complete(ticketId, McpResponse.Error($"AssetDatabase.Refresh failed: {ex.Message}"));
                ClearIfSameTicket(ticketId);
                return;
            }

            if (!TryLoad(out state) || state == null || state.Completed || state.TicketId != ticketId)
                return;

            state.RefreshCompleted = true;
            state.StatusReason = "refresh_completed";
            Save(state);

            if (EditorApplication.isCompiling || EditorApplication.isUpdating || state.CompileObserved)
            {
                ScheduleTick();
                return;
            }

            CompleteFromState(state, completionReason: EditorUtility.scriptCompilationFailed
                ? "pre_existing_compile_errors_after_refresh"
                : "no_compile_needed");
        }

        static void OnCompilationStarted(object context)
        {
            if (!TryLoad(out var state) || state == null || state.Completed)
                return;

            state.CompileObserved = true;
            state.StatusReason = "compilation_started";
            Save(state);
            JobRegistry.Progress(state.TicketId ?? string.Empty, new JsonObject
            {
                ["phase"] = "compilation_started",
            });
        }

        static void OnAssemblyCompilationFinished(string assemblyPath, CompilerMessage[] messages)
        {
            if (!TryLoad(out var state) || state == null || state.Completed)
                return;

            state.CompileObserved = true;
            state.StatusReason = "assembly_compilation_finished";
            state.Diagnostics ??= new List<DiagnosticData>();

            foreach (var message in messages ?? Array.Empty<CompilerMessage>())
            {
                state.Diagnostics.Add(new DiagnosticData
                {
                    AssemblyPath = assemblyPath ?? string.Empty,
                    File = message.file ?? string.Empty,
                    Line = message.line,
                    Column = message.column,
                    Type = message.type.ToString(),
                    Message = message.message ?? string.Empty,
                });
            }

            Save(state);
            JobRegistry.Progress(state.TicketId ?? string.Empty, new JsonObject
            {
                ["phase"] = "assembly_compilation_finished",
                ["assemblyPath"] = assemblyPath ?? string.Empty,
                ["messageCount"] = messages?.Length ?? 0,
            });
        }

        static void OnCompilationFinished(object context)
        {
            if (!TryLoad(out var state) || state == null || state.Completed)
                return;

            CompleteFromState(state, completionReason: "compilation_finished");
        }

        static void ScheduleTick()
        {
            if (_tickScheduled)
                return;

            _tickScheduled = true;
            EditorApplication.update += TickPendingRequest;
        }

        static void TickPendingRequest()
        {
            EditorApplication.update -= TickPendingRequest;
            _tickScheduled = false;

            if (!TryLoad(out var state) || state == null || state.Completed)
                return;

            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                ScheduleTick();
                return;
            }

            if (!state.RefreshStarted)
                return;

            if (state.CompileObserved || state.RefreshCompleted)
            {
                CompleteFromState(state, completionReason: state.CompileObserved
                    ? "compilation_settled_after_reload"
                    : (EditorUtility.scriptCompilationFailed
                        ? "pre_existing_compile_errors_after_refresh"
                        : "no_compile_needed"));
            }
        }

        static void CompleteFromState(StateData state, string completionReason)
        {
            var ticketId = state.TicketId ?? string.Empty;
            if (state.Completed || string.IsNullOrEmpty(ticketId))
                return;

            state.Completed = true;
            state.CompletedAtUtc = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ");
            state.StatusReason = completionReason;
            Save(state);

            var result = BuildResult(state, completionReason);
            JobRegistry.Complete(ticketId, McpResponse.SuccessStructured(result));
            ClearIfSameTicket(ticketId);
        }

        static JsonObject BuildResult(StateData state, string completionReason)
        {
            var diagnostics = state.Diagnostics ?? new List<DiagnosticData>();
            var errors = diagnostics.Count(d => IsError(d.Type));
            var warnings = diagnostics.Count(d => IsWarning(d.Type));
            var scriptCompilationFailed = EditorUtility.scriptCompilationFailed;
            var preExistingErrors = !state.CompileObserved && scriptCompilationFailed;
            var success = errors == 0 && !scriptCompilationFailed;

            var arr = new JsonArray();
            foreach (var diagnostic in diagnostics)
            {
                arr.Add(new JsonObject
                {
                    ["type"] = diagnostic.Type ?? string.Empty,
                    ["assemblyPath"] = diagnostic.AssemblyPath ?? string.Empty,
                    ["file"] = diagnostic.File ?? string.Empty,
                    ["line"] = diagnostic.Line,
                    ["column"] = diagnostic.Column,
                    ["message"] = diagnostic.Message ?? string.Empty,
                });
            }

            if (preExistingErrors && arr.Count == 0)
            {
                arr.Add(new JsonObject
                {
                    ["type"] = "Error",
                    ["assemblyPath"] = string.Empty,
                    ["file"] = string.Empty,
                    ["line"] = 0,
                    ["column"] = 0,
                    ["message"] = ScriptUtils.GetCompilationErrorDetails(),
                });
                errors = 1;
            }

            return new JsonObject
            {
                ["compiled"] = state.CompileObserved,
                ["compileObserved"] = state.CompileObserved,
                ["preExistingErrors"] = preExistingErrors,
                ["success"] = success,
                ["statusReason"] = completionReason,
                ["errorCount"] = errors,
                ["warningCount"] = warnings,
                ["diagnostics"] = arr,
                ["requestedAt"] = state.RequestedAtUtc ?? string.Empty,
                ["completedAt"] = state.CompletedAtUtc ?? string.Empty,
            };
        }

        static bool IsError(string? type)
            => string.Equals(type, CompilerMessageType.Error.ToString(), StringComparison.OrdinalIgnoreCase);

        static bool IsWarning(string? type)
            => string.Equals(type, CompilerMessageType.Warning.ToString(), StringComparison.OrdinalIgnoreCase);

        static bool TryLoad(out StateData? state)
        {
            lock (_lock)
            {
                state = null;
                try
                {
                    if (!File.Exists(StateFilePath))
                        return false;

                    var json = File.ReadAllText(StateFilePath);
                    state = JsonSerializer.Deserialize<StateData>(json);
                    return state != null;
                }
                catch
                {
                    state = null;
                    return false;
                }
            }
        }

        static void Save(StateData state)
        {
            lock (_lock)
            {
                Directory.CreateDirectory(DirectoryPath);
                File.WriteAllText(StateFilePath, JsonSerializer.Serialize(state, new JsonSerializerOptions
                {
                    WriteIndented = true,
                }));
            }
        }

        static void ClearIfSameTicket(string ticketId)
        {
            lock (_lock)
            {
                try
                {
                    if (!File.Exists(StateFilePath))
                        return;

                    var json = File.ReadAllText(StateFilePath);
                    var state = JsonSerializer.Deserialize<StateData>(json);
                    if (state?.TicketId == ticketId)
                        File.Delete(StateFilePath);
                }
                catch
                {
                    // Stale state is recoverable on the next request.
                }
            }
        }

        class StateData
        {
            public string? TicketId { get; set; }
            public string? RequestedAtUtc { get; set; }
            public string? CompletedAtUtc { get; set; }
            public string? StatusReason { get; set; }
            public bool RefreshStarted { get; set; }
            public bool RefreshCompleted { get; set; }
            public bool CompileObserved { get; set; }
            public bool Completed { get; set; }
            public List<DiagnosticData>? Diagnostics { get; set; }
        }

        class DiagnosticData
        {
            public string? AssemblyPath { get; set; }
            public string? File { get; set; }
            public int Line { get; set; }
            public int Column { get; set; }
            public string? Type { get; set; }
            public string? Message { get; set; }
        }
    }
}
