---
type: guide
summary: "Editor Automation plugin issue board — one file per issue under this folder. Tracks Editor Automation tool/RPC bugs, missing methods, and ergonomic gaps. Workflow, frontmatter schema, and status rules live here."
tags: [editor-automation, mcp, tooling, issue-board]
---

# Editor Automation Issue Board

Living tracker for bugs, missing features, and ergonomic improvements in the
**Editor Automation plugin** (`com.sss135.editor-automation-mcp`) — the RPC surface reached
through the single `mcp__editor-automation__call` tool. **One file per issue** in this
folder, filename = `{id}.md`. New issues are created as new files; status
changes edit the existing file's frontmatter and append to its History section.

Scope: Editor Automation tool issues only — RPC method bugs, JSON shape problems,
property-resolution failures, missing tool methods, wiki/doc gaps, and
ergonomic friction in the `mcp__editor-automation__call` surface. **NOT** ColorWars
gameplay/engine issues — those go to `c:/Unity/ColorWars/docs/board/`.

Plugin source for verifying findings lives next to this board at
`../../Editor/` (i.e. `Packages/com.sss135.editor-automation-mcp/Editor/`).

## Workflow

```
OPEN → IN-REVIEW → DONE
            ↓
          OPEN (returned with failure reason)
```

Three roles:
- **Reporter agent** — files issues with `OPEN` status.
- **Developer agent** — implements fix, moves to `IN-REVIEW` with
  implementation notes. **Cannot set `DONE` unilaterally** — verification by a
  separate tester is mandatory.
- **Tester agent** — verifies fix, moves to `DONE` or returns to `OPEN` with
  failure details. The user is the default tester; an AI agent acts as tester
  only when the user explicitly asks it to verify.

## Status Rules

| Status | Who sets it | Required comment |
|--------|-------------|------------------|
| `OPEN` | Reporter or Tester | Problem description. If returned: why it failed and repro steps |
| `IN-REVIEW` | Developer | How it was fixed: "Changed X in Y.cs to do Z" |
| `DONE` | Tester | Test performed and result |
| `WONTFIX` | Developer | Why it won't be fixed |

Rules:
- **OPEN → DONE is NEVER allowed.** IN-REVIEW is mandatory.
- Every status change MUST update `status:` in frontmatter AND append a
  History entry with new status, agent role, and comment.
- Every history entry is prefixed with `` `#N-slug` `` where `N` is a
  monotonic, file-local counter (1-based, never resets) and `slug` is a short
  kebab-case descriptor (2–5 words). The `#N` prefix guarantees each bullet is
  textually unique so Edit-tool appends land at the true end of History.
- Never delete history entries — append only.
- No dates in history entries — git blame provides timestamps.

## ID Format

Kebab-case slug IDs prefixed by category:
- `B-descriptive-slug` — bug (RPC method misbehaves / wrong result / crash)
- `F-descriptive-slug` — feature request (missing method or capability)
- `E-descriptive-slug` — ergonomic improvement (friction, UX, error clarity)

Keep slugs short (2-4 words). Before creating a new file, check that
`{id}.md` doesn't already exist — IDs must be unique across all statuses.

## Frontmatter Schema (mandatory on every entry file)

```yaml
---
id: F-compilation-status
title: "No RPC to refresh assets and report compilation result"
status: OPEN               # OPEN | IN-REVIEW | DONE | WONTFIX
severity: High             # Critical | High | Medium | Low
category: feature          # bug | feature | ergonomic
tags: []                   # optional; list of short keywords
---
```

Rules:
- `id` matches the filename stem exactly.
- `category` is derived from the ID prefix (`B`→bug, `F`→feature,
  `E`→ergonomic).
- `status` is the single source of truth — skills filter on this field.
- `tags` is optional. May be empty.

## Body Template

```markdown
# {Title}

{Description — what the tool does wrong, what it should do instead, repro,
impact. Cite verified handler paths + line numbers under ../../Editor/.}

**Workaround:** {optional}
**Fix:** {optional proposed approach, ideally pointing at the helper/handler
that would change}

## History
- `#1-initial-repro` `OPEN` reporter — what happened this session, with RPC
  path, args, and verbatim error/response
```

## Creating a New Entry

1. Choose an ID: `{B|F|E}-{2-4-word-slug}`.
2. Verify `{id}.md` does not exist.
3. Write the file with full frontmatter and at least one History entry
   (`#1-{slug}` `OPEN` reporter).

## Changing an Entry's Status

1. Edit `{id}.md`.
2. Update the `status:` field in frontmatter.
3. Append one History entry prefixed with `` `#{N+1}-{slug}` `` where `N` is
   the current highest number in the section. Don't modify existing entries.
