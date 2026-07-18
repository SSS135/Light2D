---
id: F-editor-state-last-compile-time
title: "editor-application-get-state cannot report last recompile time"
status: IN-REVIEW
severity: Low
category: feature
tags: [editor, compilation, diagnostics, recompile]
---

# editor-application-get-state cannot report last recompile time

`editor-application-get-state` (`Editor/Scripts/API/Tool/Editor.Application.GetState.cs:32-38` → `EditorStatsData.FromEditor()` at `Editor/Scripts/API/Tool/Data/EditorStatsData.cs:49-62`) exposes live editor flags (`IsPlaying`, `IsCompiling`, `IsUpdating`, …) but **no record of when scripts last finished recompiling**. The binary `IsCompiling` flag only answers "is a compile running right now"; it cannot confirm that a *specific* `.cs` edit was actually compiled in before a caller tests it through MCP — the core "editing C# is not testing C#" hazard the namespace already warns about (`Docs~/WikiSource/editor.md:22-33`). A caller that polls `IsCompiling == false` after an edit cannot distinguish "my edit compiled and settled" from "my edit never triggered a compile at all."

Unity exposes no built-in `EditorApplication.lastCompileTime`, so the value must be captured from `CompilationPipeline.compilationFinished` and persisted across the domain reload a recompile triggers (the reload wipes all static fields).

**Fix:** Add a small `[InitializeOnLoad]` tracker that records, from `CompilationPipeline.compilationFinished`, both the wall-clock UTC timestamp and the monotonic `EditorApplication.timeSinceStartup` reading into `SessionState` (survives domain reload, clears on editor restart — matching the precedent in `ScriptUtils.cs:98-117`). Surface two new read-only fields on `EditorStatsData`: `LastCompilationFinishedUtc` (ISO-8601 string) and `SecondsSinceLastCompilation` (computed editor-side as `timeSinceStartup - storedFinished`, so the consumer needs no wall-clock math). No change to the handler; it already returns `EditorStatsData.FromEditor()` and stays `ReadOnlyHint`/`IdempotentHint`.

## History
- `#1-initial-repro` `OPEN` reporter — `editor-application-get-state` returns no last-recompile timestamp; only the binary `IsCompiling`/`IsUpdating` flags exist (`EditorStatsData.cs:31-47`). Verified Unity has no built-in last-compile API, so the value must be captured from `CompilationPipeline.compilationFinished` and persisted across the recompile's domain reload. Requested by user this session.
- `#2-session-state-tracker` `IN-REVIEW` developer — Added `Editor/Scripts/Utils/CompileTimeTracker.cs` ([InitializeOnLoad], subscribes to `CompilationPipeline.compilationFinished`, persists UTC + `timeSinceStartup` to `SessionState`). Added `LastCompilationFinishedUtc` (string) and `SecondsSinceLastCompilation` (double?) to `EditorStatsData` with `[Description]` attributes, populated in `FromEditor()`. Updated `Docs~/WikiSource/editor.md` application narrative. Note: the compile that *builds* the tracker fires `compilationFinished` in the prior (subscriber-less) domain, so the first probe after introducing the feature reads empty/null by design; the next recompile populates it.
