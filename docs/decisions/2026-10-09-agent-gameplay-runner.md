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

## Invocation policy update — 9 October 2026

Bailey authorizes agents to decide when gameplay checks are necessary and run the
smallest relevant scenario automatically, without waiting for a testing request.
Build once if needed for current runtime evidence. Full journeys are warranted for
cross-phase continuity risk or unresolved concrete regressions; broad suites,
performance and long soaks remain on request. AGENTS.md holds the standing rule.
No runtime/protocol or player/save change. CLI opt-in remains how an agent launches
the tool; it no longer implies that Bailey must request each run.
