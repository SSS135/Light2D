# Light2D Benchmarking Protocol

The profiler markers and the `Light2DDiagnostics` surface these fixtures read are documented in [`Packages/com.sss13594.light2d/Documentation~/BENCHMARKING.md`](../Packages/com.sss13594.light2d/Documentation~/BENCHMARKING.md).

## Benchmark Fixtures

Use the following fixtures as the minimum raster baseline. Keep camera coverage constant when changing resolution, and distribute lights uniformly across the extended light-camera bounds. Report the exact graphics API, resolution, render pipeline, renderer data type, quality level, and player build configuration with every result.

| Fixture | Resolution | Lights / coverage | Features | Cameras | Change under test |
| --- | --- | --- | --- | --- | --- |
| `submission-low` | 1280x720 | 16 / 25% | LDR, no AA, no blur, no ambient, no normals | 1 | Shader tier: 1, 20, 60, 90, Auto |
| `submission-high` | 1920x1080 | 128 / 25% | LDR, no AA, no blur, no ambient, no normals | 1 | Light count and draw submission |
| `fragment-coverage` | 1920x1080 | 32 / 25%, 50%, 100% | HDR, no AA, no blur, no ambient, no normals | 1 | Screen coverage |
| `normals` | 1920x1080 | 8, 32, 64 / 50% | HDR and normal mapping | 1 | Per-light normal cost |
| `ambient` | 1920x1080 | 32 / 50% | HDR, ambient 1, 2, 4, 8 cycles; blur off/on | 1 | Propagation and bandwidth |
| `obstacle-aa` | 1920x1080 | 32 / 50% | LDR/HDR, obstacle AA off/on | 1 | Obstacle resolution and resolve |
| `blur` | 1920x1080 | 32 / 50% | LDR/HDR, direct and ambient blur off/on | 1 | Blur passes and bandwidth |
| `multi-camera` | 1920x1080 | 32 / 50% per camera | HDR, ambient 2 cycles, blur | 1, 2, 4 | Independent contexts |
| `quality-change` | 1920x1080 | 32 / 50% | Toggle HDR, AA, blur, normals, and resolution | 1 | Reallocation and warmup |

Run every fixture at a fixed player resolution on these target classes:

- Desktop discrete GPU using the primary supported desktop graphics API.
- Desktop integrated GPU using the same content and quality settings.
- Representative mobile tile GPU using a supported mobile graphics API and a non-development player.

Record the device, GPU, driver or OS version, graphics API, thermal state, and power mode. Do not compare Editor captures with player baselines.

## Capture Protocol

1. Build a player with script debugging and deep profiling disabled. Enable profiler connection only for the capture.
2. Load the fixture, wait for shader compilation and asset loading, then discard at least 120 warmup frames.
3. Capture warmup separately, including first-frame allocations, shader creation, texture allocation, and ambient-history initialization.
4. Capture at least 600 steady-state frames without changing scene content or quality settings.
5. Record median, 95th percentile, and maximum main-thread, render-thread, and GPU frame time.
6. Record managed allocation per frame, reserved and used managed heap, graphics-driver memory when available, `OwnedTextureBytes`, context count, and reallocation count.
7. Repeat each fixture at least three times after restarting the player and report all runs rather than only the fastest.

Use Unity Profiler CPU Timeline data for submission and render-thread cost, GPU Profiler or platform GPU tooling for stage cost, Profile Analyzer for frame distributions, and Memory Profiler snapshots immediately after warmup and after the steady-state capture. A valid steady-state capture has zero Light2D-authored managed bytes per frame and no resource-count growth.

## Interpreting Results

| Limit | Evidence |
| --- | --- |
| CPU submission | Main or render thread rises with light count while GPU time and coverage remain stable. |
| Fragment cost | GPU time rises with light coverage or shader tier while draw count remains stable. |
| Bandwidth | HDR, AA, blur, or ambient cycles increase GPU time disproportionately to shader work; tile-GPU counters show external-memory traffic. |
| Memory | `OwnedTextureBytes` and platform graphics memory match expected feature allocations and return after owners are destroyed. |

Do not retain compute ambient propagation unless raster baselines exist for all three target classes, unsupported hardware selects the raster path, output stays within the approved image threshold, and compute produces a meaningful repeatable GPU-frame-time improvement without regressing CPU time or memory. Evaluate conventional instancing only when the baseline identifies CPU or render-thread draw submission as the limiting factor.
