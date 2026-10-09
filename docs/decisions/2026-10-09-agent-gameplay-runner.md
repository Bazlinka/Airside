# Hidden agent gameplay scenarios

- Date: 2026-10-09
- Status: Implemented; native gameplay execution unverified.
- Decision: Provide one opt-in agent QA runner that reuses a matching clean build,
  batches selected production command/controller checks with finished-frame evidence,
  and optionally invokes existing accelerated journey hooks. No player UI or network
  control endpoint. Each scenario owns a unique QA save and bounded child process.
- Reason: Existing review hooks require separate launches and manual evidence
  assembly. Batching reduces startup cost while retaining explicit coverage/failures.
- Affected systems: Soak startup/dispatch gating and save path, AgentGameplay partial,
  scripts/agent-gameplay.py, existing ReviewFrameCapture and journey review.
- Migration impact: None. No player save/settings schema or gameplay rule changes.
  Protocol 1 applies only to disposable QA plans/reports. Existing QA tools remain.
- Limits: CLI opt-in is a workflow restriction, not AI authentication. Checks exercise
  production commands, not physical pointer input. Screenshots require inspection;
  acceleration does not establish performance or real-time pacing. Full profile covers
  named feature scenarios and one round trip, not every game feature.
