# Contributing to Light2D

Thanks for your interest in improving Light2D. Bug reports, feature requests, and pull requests are all welcome.

## Development setup

Light2D is developed as an embedded UPM package inside this repository: the repository root is a Unity project, and the package lives in [`Packages/com.sss13594.light2d/`](Packages/com.sss13594.light2d/).

1. Open the **repository root** in Unity **6000.4.7f1** — not a subfolder. The package is auto-discovered under `Packages/`.
2. The package's own EditMode/PlayMode suites run through the Unity Test Runner (the project manifest lists it under `testables`).
3. See [`Packages/com.sss13594.light2d/Documentation~/TESTING.md`](Packages/com.sss13594.light2d/Documentation~/TESTING.md) for the full test and host protocol.

## Before you open a pull request

- Run the **EditMode** suite and any relevant **PlayMode** tests, and make sure they pass. If a test is knowingly blocked, say so — for example, the URP behavioral tests are blocked by an external Unity 6000.4.7f1 / ShaderGraph 17.4 defect and skip on affected hosts by design.
- **Preserve `.meta` files and asset GUIDs.** Serialized scenes, prefabs, and materials depend on them.
- **Don't break the compatibility contracts** — legacy shader names, pass tags, properties, keywords, and serialized field names — unless your change includes a tested migration. The protected list is in [`CLAUDE.md`](CLAUDE.md).
- **Keep samples mirrored:** `Packages/com.sss13594.light2d/Samples~/Examples/` and the imported copy under `Assets/Samples/Light2D/2.0.0/Examples/` must stay byte-identical; mirror any change to both.
- Write a clear PR description and link any related issue (e.g. `Closes #123`).

## AI-assisted contributions

AI-assisted and AI-agent-authored pull requests are **welcome** — but they **must be clearly disclosed** so we can review and merge them faster. If any part of your PR (code, tests, or docs) was produced with an AI agent or assistant:

1. **Tick the AI-assistance checkbox** in the pull request template.
2. **Name the tool(s)** used (e.g. Claude Code, Copilot) in that section.
3. **Prefix the PR title with `[AI]`** so it is visible at a glance in the PR list.
4. Keep a machine-readable **`Co-Authored-By:`** trailer naming the tool in the relevant commits, e.g. `Co-Authored-By: Claude <noreply@anthropic.com>`.

Disclosure is about triage speed, not gatekeeping: a clearly-marked AI PR gets a faster, appropriately-scoped review. **The human submitter remains responsible** for the correctness, testing, and licensing of everything in the PR — review AI output before submitting it.

## License

By contributing, you agree that your contributions are licensed under the repository's [MIT License](LICENSE.md).
