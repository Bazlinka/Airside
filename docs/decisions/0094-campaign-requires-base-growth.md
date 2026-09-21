# 0094 — Base growth is part of campaign progression

Date: 21 September 2026.

## Decision

The Adelaide player base is a core career requirement, not optional side progression.

- Chapter 2 (Eyre Peninsula) requires Expanded Regional.
- Chapter 4 (Interstate) requires Jet Gate.
- Chapter 5 (Going Global) requires International.

Chapter 1 remains focused on proving the first Saab operation. Chapter 3 remains focused on building
a reliable regional network and acquiring the Dash 8; adding another base checkbox there would
duplicate Chapter 2 without changing how the airline operates.

## Persistence

No new state. Campaign remains derived from AirlineCareerState and now reads the already-persisted
PlayerBaseLevel introduced in save v12.

## Rationale

Fleet class, stand access, maintenance capability and turnaround throughput now all depend on the
base. Letting the campaign ignore that system would allow the story to claim the airline has become
an interstate or international operator without establishing the physical Adelaide capability that
makes those operations possible.
