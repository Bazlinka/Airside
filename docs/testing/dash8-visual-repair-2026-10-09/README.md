# Dash 8 visual repair — task #712

Symptom: the supplied airborne side-follow screenshots show a large rectangular gear panel and exposed main tyre behind each nacelle, plus a raised nacelle/wing join.
Expected: folded main gear enclosed by the nacelle; fitted bay doors close after the cycle; nacelle joins meet the underside of the wing.

## Reproduction and cause

Baseline: `74d4311a79091a3814e893513461008465bc6ca4`, pushed before native execution. Actual Unity runtime-builder gear-up side/under frames reproduce both protruding doors and tyres. Logs show all four tall main doors parented to `Gear L/R`. The generic classifier correctly recognises the authored thin vertical plates as leg-mounted doors, but those are the wrong geometry for this nacelle bay. The rear nacelle also fails to enclose the folded twin wheels. Its separate oval wing fillet exposes a raised shoulder/cap.

Baseline packaged Mac diagnostic `diag-a5881a7bf4ac40f2`: five follow/side/front/opposite/overview steps completed, matching clean build and private save, zero runtime errors. The baseline capture helper flipped PNGs vertically; the independently merged capture correction is included in the repair base. The baseline native editor frames are upright. Earlier remote request `diag-af32b4e8ad55423f` was cancelled by runner concurrency before execution; no remote result is claimed.

## Repair

AIR-006 only: curved closed bay leaves, independently hinged on opposite nacelle shoulders; expanded rear nacelle enclosing the actual existing aft-fold wheel envelope; smooth shared-vertex shell and under-wing fillets with buried shoulders; cowl paint refitted to the changed shell. Surgical `--nacelles-only` mode retains the finished fuselage, cockpit, cabin doors, wings, tail, generic fictional paint and all other parts. Runtime glTF/bin, editable FBX, thumbnail and packaged mirrors retain their paths/GUIDs. Simulation, identity, save format and phase timing are unchanged.

The generic tail marks intentionally remain the game's fictional paint. No copied Qantas artwork is introduced. Side-on rotating propellers may appear thin because their disc is viewed edge-on; a still does not establish an animation defect.

## Verification

- AIR-006 bounds, bay geometry, panel shape and full folded leg/tyre/rim/scissors enclosure pass (maximum radial envelope below 0.95).
- Nacelle and wing fillet indexed edges each have two incident triangles; positive volumes.
- Native repaired poses, focused articulation regressions and identical packaged scenario: pending on this pushed revision. No visual pass is claimed yet.

No broad test suite, long soak or performance certification.
