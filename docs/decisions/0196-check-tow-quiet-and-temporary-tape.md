# 0196 — A checked aircraft is towed quiet; boarding tape is temporary

Status: accepted (Unity look not yet verified; the pure parts are tested)

- While an aircraft is in its check (ADR 0186) it is towed to the hangar with engines and beacon off and doors shut, and
  nobody deplanes, boards or waits for it: `BoardingFlow.InCheck` gates the passenger moves, doors, stair truck, bus and
  ramp crew, and `EngineStartSequence.For`. Before, a check started soon after landing let the engines wind down and the
  arrival passengers walk off while the aircraft was already being towed.
- The barrier tape (ADR 0187) is no longer baked into the road mesh. `AirsidePrototype.WalkwayTape` puts it up along the
  route passengers really walk (terminal end to stair foot, round the aircraft) while they are on it, and takes it down
  12 s after the last one, so the apron is clear otherwise. `AdelaideWalkwayGeometry.BuildAlong` draws it for any route.
