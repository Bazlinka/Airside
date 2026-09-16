using System.Runtime.CompilerServices;

// Lets the EditMode test assembly exercise the internal "apply a settlement exactly once"
// guard directly (AirlineCareerState.TryApplySettlement) rather than only through the public
// command surface, where the state machine happens to make a genuine double-call unreachable.
[assembly: InternalsVisibleTo("Airside.Tests.EditMode")]
