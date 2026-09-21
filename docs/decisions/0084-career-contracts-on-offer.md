# 0084 — Authored career contracts on offer

Date: 21 September 2026. Bailey: more contracts and variety per tier.

## Decision

- Twelve authored career contracts added, a stepping stone for each campaign chapter
  (ADR 0083): Mount Gambier, Ceduna, Coober Pedy, Mildura, Broken Hill (Regional);
  Canberra, Sydney, Brisbane, Perth (Domestic); Auckland, Singapore, Hong Kong
  (International). Each names the type it needs, so they also pull the player up the
  fleet ladder.
- `AirlineOperations.MarketOffers()` now leads with the next two authored contracts the
  tier allows and the airline has not fulfilled, then the rotating market. Before this the
  authored contracts appeared only as the objective card's fallback, never on the
  Contracts page.
- A test checks every authored contract is reachable and route-band-legal for its type.

Pay figures are first guesses to tune by play. No save change: accepted definitions are
already remembered by the career state.
