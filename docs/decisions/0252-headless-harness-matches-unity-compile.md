# 0252 — The headless harness must fail where Unity's compile fails

Status: accepted
Date: 2026-10-07

- **Decision:**
  1. `scripts/dotnet-harness/Harness.csproj` sets `ImplicitUsings` to **disable**. .NET implicitly imports `System`, `System.Linq`, `System.IO`
     and others; Unity does not, so a file missing `using System;` compiled headlessly and broke Unity.
  2. A **compile-only build against NUnit 3.5.0** (`-p:UnityNUnit=true`, wrapped by `scripts/check-unity-nunit.sh`) runs in CI before the
     tests and at the start of `scripts/test-domain.sh`. Unity Test Framework 1.6.0 (our `manifest.json`) bundles NUnit 3.5, so constraints
     Unity lacks (`Is.AnyOf` broke `main`) now fail the pull request. The tests still *run* on NUnit 3.14, because 3.5 has no .NET 8 assets.
- **Reason:** on 2026-10-07 `main` did not compile in Unity (Mac build blocked) while every headless check was green: `Math` without
  `using System;` and `Is.AnyOf` were both accepted by the harness. The fix was manual (PR #551); this closes the same hole for the future.
- **Affected systems:** the dotnet harness project, `scripts/test-domain.sh`, `scripts/check-unity-nunit.sh`, `.github/workflows/headless.yml`.
  No game code, assets, saves or tests changed; the existing tests already comply.
- **Evidence:** a probe test using `Is.AnyOf` and an unqualified `Math` was rejected by the compat build (CS0117 and CS0103) and, for `Math`,
  by the plain harness (CS0103); with the probe removed the compat compile passes and the full suite runs 1842 passed / 0 failed.
- **Still not covered:** UnityEngine-dependent tests and Presentation files do not run or compile headlessly (the harness only takes files
  that build without UnityEngine), and Unity-only APIs on NUnit 3.5 are assumed to match the bundled version. A real Unity run
  (`scripts/test-unity.sh`) remains the source of truth, and wiring it into CI needs a Unity licence secret from Bailey.
- **Revert:** remove the CI step and the `UnityNUnit` conditions; set `ImplicitUsings` back to `enable`.
