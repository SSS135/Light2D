# Runtime Diagnostics

Light2D exposes stable profiler markers through `Light2DProfiling`. Their Profiler window names are:

| Stage | Marker |
| --- | --- |
| Settings and frame validation | `Light2D.Validation` |
| Resource allocation and reconciliation | `Light2D.ResourceReconciliation` |
| Light and renderer filtering | `Light2D.Culling` |
| Obstacle rendering | `Light2D.Obstacles` |
| Direct-light rendering | `Light2D.Sources` |
| Normal-buffer rendering and application | `Light2D.Normals` |
| Direct or ambient blur | `Light2D.Blur` |
| Ambient emission and propagation | `Light2D.Ambient` |
| Final camera-color composition | `Light2D.Composition` |

`Light2DDiagnostics.LastFrame` reports the most recent texture dimensions, estimated owned texture bytes, visible-light count, ambient cycles, reallocations, and active contexts. `OwnedTextureBytes` and `ContextCount` report current package-owned totals. Reading these values and using the markers does not allocate managed memory after initialization.
