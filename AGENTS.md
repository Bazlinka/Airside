# Airside working rules

- Protect the real-time, persistent airport-management concept defined in `docs/product/Airside Project Plan.docx`.
- Keep domain and simulation code independent of Unity scenes and presentation code.
- Use an injected clock and seeded random source for simulation rules.
- Every state-changing command must be identifiable and safe to apply once.
- Add systems in the order set by the project plan; do not begin broad content production before the first aircraft loop is stable.
- Preserve save compatibility. Any persisted schema change needs an explicit version and migration path.
- Do not add paid data or asset dependencies without recording licence, attribution, cost and fallback information.
- Test behaviour at variable frame rates and compare live simulation with offline catch-up.
- Keep changes narrow, reviewable and tied to an acceptance criterion.
