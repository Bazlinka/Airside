# Issue-driven real-game diagnosis and Mac bridge

- Date: 2026-10-09
- Status: Implemented; real remote success/failure evidence verified on 9 October 2026.
- Decision: A reported issue triggers reproduce → inspect evidence → identify cause
  → scoped fix → rerun the same scenario. Fixed smoke checks alone cannot close a
  diagnosis. Add custom action scenarios and related issue probes, real-frame/state/
  error traces and a private Mac self-hosted Actions runner for Linux agents.
- Reason: Agent gameplay tooling existed but had neither a cloud-to-Mac execution
  path nor an issue-driven investigation contract. Mac build requirements caused
  remote agents to stop with source-only conclusions.
- Affected systems: AgentGameplay, camera QA pose/time hooks, diagnostic CLI,
  manually dispatched Mac workflow and installed user-level Actions runner.
- Migration: None for gameplay/saves/settings. Existing protocol-1 fields remain;
  QA-only optional subject/expected-state fields and observation/error data added.
- Isolation: Exact committed revision; validated action/parameter JSON, no arbitrary
  command field; private authenticated workflow_dispatch only, no PR/push execution
  on the Mac. Dedicated runner checkout, serial job queue, separate QA saves.
- Operation: Mac must be awake, logged in and connected. User launch service lives
  outside protected Documents under ~/Developer/Airside-DiagnosticsRunner. No incoming
  network port. Only private-repository workflow-authorized collaborators can dispatch.
- Limits: Keyword probes suggest a starting reproduction; the agent refines custom
  scenarios and inspects images/logs. Captured evidence is not automatic visual
  approval or a proven root cause. Frame timings include diagnostic/capture overhead
  and host contention. Offline/licence/rendering blockers remain explicit blockers.

- Evidence: `docs/testing/agent-gameplay/DIAGNOSTIC_EVIDENCE_2026-10-09.md`. Ten weather
  steps/seven upright frames and an intentional failed-state screenshot were inspected.
