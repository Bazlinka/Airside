using System.Runtime.CompilerServices;

// The EditMode tests clear and probe Presentation caches (the tree-placement generators). The headless harness
// compiles Presentation and the tests into one assembly, so those internals were visible there but not to the
// Unity test assembly, and the tests failed to compile in the editor.
[assembly: InternalsVisibleTo("Airside.Tests.EditMode")]
