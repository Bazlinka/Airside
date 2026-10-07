# Validate player journeys with a local interactive dispatch study

Status: accepted for design-study work; runtime interface direction remains proposed

Date: 7 October 2026

## Decision

Bailey asked for the player perspective and complete flows to lead the interface, then authorised continuing that work. Build a connected local browser design study against `docs/plans/player_flows_and_interface_contract.md`, with separate sample state and explicit preview transitions. Explore horizontal navigation and a bottom flight tray as a fresh alternative to the current shell. Do not integrate it into Unity or promote the images into runtime assets.

## Reason

Earlier photographic concepts exceeded the current game's demonstrated visual fidelity. Later screenshot-based studies were more grounded but still illustrated only a subset of tasks. A connected prototype lets the owner review actual decisions, commitment consequences, blocked actions, context preservation and recovery before choosing the visual shell.

## Affected systems

New documentation-only `docs/art/player-flow-prototype-2026-10-07/`, browser verification script and retained evidence. Link the flow contract, prior studies, current status board and asset/source register. No Unity renderer, simulation, save schema or command semantics change. Original screen/maintenance ADRs are not superseded at runtime by a proposal.

## Migration impact

None for the game. Local browser study schema 1 and a separate localStorage key are not compatible with, and never access, Unity saves. All prices, timing and progression fixtures are labelled illustrative. Native Unity validation remains necessary for any later implementation.
