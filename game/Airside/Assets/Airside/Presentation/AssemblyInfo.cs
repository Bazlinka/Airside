using System.Runtime.CompilerServices;

// Keep placement cache reset hooks internal while allowing the separate Unity test assembly.
[assembly: InternalsVisibleTo("Airside.Tests.EditMode")]
