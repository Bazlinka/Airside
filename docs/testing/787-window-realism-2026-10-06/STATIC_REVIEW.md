# Independent 787 window geometry regression

6 October 2026. Static source/mesh review only; no Unity, builds or renders.

`python3 scripts/test-787-window-fit.py --report docs/testing/787-window-realism-2026-10-06/measured-fit.json`

The default regression uses only Python's standard library and committed fixtures;
it works in a shallow checkout. `pre-upgrade-geometry.json` captures immutable
pre-change node/geometry hashes and individual connected panes from `e60a9efa`.
`uncut-source-hulls.json` snapshots the original project generator lofts, with
source SHA256 checks. Their compressed payloads contain JSON, not executable code.
Only the opt-in source snapshot command imports generator NumPy dependencies.

Each type retains 186 individual panes across 96 window nodes; six nodes have a
single pane beside a door. Tests compare connected pane counts/topology, normalized
rounded silhouettes, projected Y/Z centres and pitch against the original geometry.
Outer glass is 0.28723404255 × 0.500 m. Its 787-specific 0.94 inner seal gives the
representative 0.270 × 0.470 m clear-aperture profile. These are authored projected
mesh dimensions, not manufacturer-certified measurements. The source hull fit and
finished hull/gasket clearance are separate checks: holes in the finished hull
must not be mistaken for missing source skin.

The preservation allowlist contains only passenger panes, cabin gasket/interior
nodes, and the coordinated fuselage/fuselage_port cutout meshes. Every other node's
position/attribute/index/material data remains compared against the immutable
fixture. Added hull partitioning is limited to `fuselage_port`. The regression also
checks UInt16 mesh limits, valid indices, FBX/glTF positions and topology, finite
unit FBX normals, FBX Geometry/Model connections, and byte-identical packaged mirrors.

Clearance includes nine named profile probes and a 21 × 21 grid restricted to the
achieved clear polygon, reaching 98% of its projected bounds. These are bounded
geometric samples, not a native rendering or all-angle visibility proof.

Initial strict review found a residual white hull triangle near the B789 clear
edge: X −2.751616955, Y 7.166942988, Z −24.364926915. The gasket did not mask that
probe; the finished hull did, at 9.48 mm behind the ray datum. The coarse centroid
cut left the boundary fragment inside the declared clear aperture. This regression
retains the failing boundary probe; the full strict run must pass after correction.

Native compilation, materials/transparency, actual view switching, clipping,
framerate and appearance remain unverified under the user's no-Unity restriction.
