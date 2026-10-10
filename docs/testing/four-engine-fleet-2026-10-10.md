# Four-engine fleet validation (#778)

Boeing 747-8 Intercontinental (B748, Bailey's selection) and Airbus A380-800 (A388). Scoped source and asset integration; native verification in progress.

Geometry generator validates dimensions, tyre ground/nose datums, finite index data, four engines and 18/22 wheels. The actual runtime thumbnails were opened: distinct upper hump/full double deck and four nacelles visible. Shared fitted glazing opens window apertures for native interior review. Existing aircraft files are preserved.

Focused regressions cover type-aware profiles, F-only stands and taxi availability in both main runway directions, wake classes, live feed identity, save restore, outer-engine obstacles and relevant existing acquisition/audio/profile tests. Record final counts and native evidence below before completion. No blanket suite or performance claim.

Limits: exact variant cockpit instrumentation, upper-deck traversal, exhaustive listening, long-haul destination ground compatibility, real airport dispatch approval and performance are not established.

## Focused results

112 initial headless checks pass, followed by eight purchase/save/code F clearance cases. Unity NUnit 3.5 compile check passes with no errors. 86 native EditMode checks pass: actual model builders, four fan rigs, five struts/four trucks, wheel counts/parentage, gear cycle/cache/pause, model dimensions, catalogue/stand compatibility, taxi wheelbases/performance, purchase/save/clearance and cabin profiles (`work/native-fleet.xml`, `work/native-fleet.log`).

Final connectivity audit passes for both models within 5 cm after fixing the outer A380 pylons and root landing lights. Metadata/mirror audit passes: 2,168 GUIDs, 453 mirrors. Python syntax checks pass. The earlier interrupted native setup and intermediate connectivity failure are not final passes.

Packaged review and integration with newly merged economy v2 are in progress; the counts above precede that merge.

After economy integration: 84 selected headless checks pass; clean universal `4232e064` Mac build succeeds. Exact generator --check passes for glTF/bin/FBX. First B748 packaged follow/day/rear/dusk/night frames inspected; camera attempt correctly refused a cold engine-off cockpit (recorded failed scenario, not a product error). The fixture now grants its injected aircraft the required base and full journeys honor the selected type; nine focused purchase/overflow/save/wake cases and 12 runner regressions pass. Clear shared F overflow avoids a seasonal operator indefinitely blocking leased 18R. Final source/build/frame results pending below.
