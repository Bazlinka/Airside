# ADR 0242 — Refined interface and maintenance design study

Date: 6 October 2026. Status: design proposal refined under Bailey's chat approval; runtime implementation pending.

Create a concrete, locally interactive design study and scoped task packet for the accepted maintenance/interface direction. Keep the ADR 0231 palette, but simplify overview composition to a slim status surface, narrow navigation, quiet objective and optional aircraft inspector. Management workspaces replace competing panels; identity and aircraft actions remain visible at compact heights. The HTML prototype is a review tool and does not introduce a shipping web framework.

Propose a separate deterministic maintenance job sharing aircraft-specific startup, excluding boarding/loading and coordinating actual ground reservations. Taxi under power to the shed apron, shut down, then use a short continuous tug positioning movement into a fitting bay. Repair starts when positioned; wear resets once on completion; return requires an available stand. Define save migration and legacy check handling before implementation.

Affected systems: future shared HUD/workspaces, startup/service gating, maintenance, hangar capacity/geometry, ground reservations and save/catch-up. No runtime changes or save migration in this design commit. Existing product plans and current gameplay remain intact.

Evidence: `docs/art/interface-refinement-2026-10-06/`; implementation packet: `docs/plans/refined-interface-and-maintenance.md`. Original schematic background and sample figures are explicitly labelled. No Unity execution, compile, native appearance or performance claim. First recommended implementation slice is overview/inspector plus a complete Saab maintenance journey, followed by jet pushback and larger-aircraft clearance.

Implementation follow-through: ADR 0243, 7 October 2026. The study is retained as design history; shared runtime painters now implement its overview/inspector direction.
