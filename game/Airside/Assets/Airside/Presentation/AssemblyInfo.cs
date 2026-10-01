using System.Runtime.CompilerServices;

// Native EditMode tests use internal cache-reset hooks; the headless harness
// compiles these tests into the same assembly and already has access.
[assembly: InternalsVisibleTo("Airside.Tests.EditMode")]
