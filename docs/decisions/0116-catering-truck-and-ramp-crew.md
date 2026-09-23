# 0116 — A catering truck that exists, and people on the apron

Date: 23 September 2026. Bailey asked for more detail on the fuel truck, catering truck and
boarding — and asked where the passengers are.

## What was actually wrong

**The catering truck had no model.** `BuildServiceVehicle("Catering truck", …)` was called
without an art path, the one turnaround vehicle that was. It fell back to a flat-shaded box
while the fuel truck, baggage tug and apron bus all loaded authored v06 kits. There was no
catering model anywhere on disk.

**Passengers were drawn for stairs boarding only.** `UpdatePassengers` skips any aircraft
whose `BoardingMode` is not `IntegralAirstair` or `StairTruck`. That is correct as far as it
goes — you cannot see people inside an aerobridge — but every Terminal 1 jet gate uses a
bridge (17 of them, ADR 0113), and that is where the player's jets park. So the answer to
"where are the passengers" is that at a terminal gate there were none, and there was nobody
else on the apron either.

**Two hi-vis ramp workers were imported and never placed.** ADR 0114 brought in
`chr_ramp_m_worker` and `chr_ramp_f_worker` and said so explicitly — "imported but not yet
placed". Nothing referenced them.

## Decision

### VEH-004 catering hi-loader

`scripts/generate-veh-fleet-v06.py` gains `catering_truck_v01()`, built from scratch rather
than from a v05 base, and writes `mdl_catering_truck_v01`. What identifies the type is the
scissor lift, so that is what it models: a chassis and cab, crossed scissor legs, a box body
carried well clear of the chassis, and a bridge platform with handrails out front at cabin
door height. 44 meshes.

Part names follow the existing kit conventions (`cab`, `wheel_fl`, `glass_pane_1`, `beacon`
and so on), so the loader's colour rules — dark wheels, tinted glass, orange beacon — apply
with no C# change beyond passing the art path.

### Ramp crew (Simulation/RampCrew.cs, UnityEngine-free)

Crew are placed for whichever vehicle is working, so the apron is populated through
fuelling, catering and baggage rather than only during a stairs boarding:

| stage | crew |
|---|---|
| Fuel | one at the truck's panel, one under the wing — aircraft's right |
| Catering | one steadying the hi-loader, one at the forward galley door — left |
| Baggage | one at the hold, one on the cart — left |
| Boarding | one marshaller, ahead and clear of the boarding path |
| Idle / Ready | none — the aircraft is not being worked |

Positions are stand-local metres and are rotated onto the stand in presentation. The two
imported characters alternate, so two workers beside one aircraft are never the same person.

`EnsureCharacters` was refactored into `LoadCharacterKinds(ids, into)` and called twice, so
passengers and crew cannot drift apart in how they are imported or scaled.

**Presentation only.** No simulation timing, reservation or save behaviour changes.

## Evidence

- `scripts/test-domain.sh` — **776 passed**, 10 new in `RampCrewTests`.
- Tests assert that every serviced stage puts crew out, that idle and ready put none out,
  that boarding keeps its single marshaller clear of the door, that nobody stands on the
  centreline (inside the fuselage) or off the stand, that crew work the same side as their
  vehicle, and that both imported characters get used.
- The catering model was rendered and inspected before being wired in.

Not verified: the Unity compile and the in-engine look. `scripts/test-unity.sh` cannot run on
this Mac (batchmode licensing loop, `docs/build-mac-batchmode-dead-end`) and the harness does
not compile `AirsidePrototype.Boarding.cs`, where the crew are drawn.

## Still open

- Passengers still never appear at an **aerobridge** gate. They are genuinely inside the
  tunnel, so the honest options are a few figures in the gate lounge behind the glazing, or
  leaving it. Not decided here.
- Passengers do not appear for **bus** boarding either; `BoardingMode` has no bus value, so
  a bus stand shows the vehicle and the new crew but no queue.
- The catering truck's scissor lift is static. It does not extend to the door.
