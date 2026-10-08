# Save recovery copy

Status: Accepted for implementation. Date: 2026-10-08. Task: #599.

## Decision

Keep one previous readable airline JSON beside the primary as `.bak`. Existing
atomic temporary-file replacement remains. If the primary is missing or cannot
be parsed as an airline save, Continue reads the backup and labels the title
summary “Recovery copy”; after Continue, an amber toast warns that recent changes
may be missing. A readable primary always wins.

A write over an unreadable primary must preserve any existing recovery copy.
The backup rotates only when the previous primary can be read. This recovery
works for the normal save and the isolated soak save through their own paths.

## Reason and scope

Atomic writes protect against partial writes, but previously discarded the old
file without retaining a recovery option. One backup offers bounded disk use
and recovery without a new save-management screen or a simulation change.
Affected files: AirlineSaveFile, the prototype's save probe/summary/Continue,
and native AirlineSaveTests. No save-schema change or migration is needed.

## Limits

Recovery covers missing files, invalid JSON and absent airline records. A
parseable save that fails semantic restore or uses an unsupported version keeps
the existing refusal; it does not silently revert to an older career. This is
one previous save, not a history or a backup against disk loss. Native temporary
file fixtures verify file behaviour; no real player save is used for testing.
