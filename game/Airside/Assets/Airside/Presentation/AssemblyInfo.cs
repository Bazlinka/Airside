using System.Runtime.CompilerServices;

// GltfJson is an implementation detail of ArtGltfLoader, not public API, but it
// carries the parsing rules the kit format depends on and is worth testing directly.
[assembly: InternalsVisibleTo("Airside.Tests.EditMode")]
