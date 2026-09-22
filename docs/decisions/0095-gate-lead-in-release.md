# 0095 — Gate lead-in releases at Holding Short

Date: 21 September 2026.

## Decision

A terminal gate's lead-in is reserved while an aircraft taxis in to the gate or pushes
back from it. The reservation releases when the departing aircraft reaches Holding Short;
the gate stand remains reserved through TaxiOut as before.

## Rationale

At Holding Short the aircraft has cleared its gate lead-in and is waiting at the runway,
so retaining the lead-in reservation would make a runway queue block an otherwise clear
gate movement. Releasing it keeps gate circulation responsive without allowing a second
aircraft to occupy the still-reserved stand.

## Affected systems

Ground resource lookup and terminal-gate assignment use the same derived rule, so saved
and caught-up simulations retain deterministic reservation behaviour. No save migration is
required.
