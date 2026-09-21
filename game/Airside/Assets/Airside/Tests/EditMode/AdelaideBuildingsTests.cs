using System.Linq;
using Airside.Simulation;
using NUnit.Framework;

namespace Airside.Tests.EditMode
{
    public sealed class AdelaideBuildingsTests
    {
        [Test]
        public void Snapshot_ContainsRecognisableOperationalLandmarks()
        {
            Assert.That(AdelaideBuildings.All.Length, Is.GreaterThanOrEqualTo(50));
            Assert.That(AdelaideBuildings.All.Any(b =>
                b.Kind == AdelaideBuildingKind.ControlTower && b.Name.Contains("Control Tower")), Is.True);
            Assert.That(AdelaideBuildings.All.Any(b =>
                b.Kind == AdelaideBuildingKind.FireStation && b.Name.Contains("Fire Station")), Is.True);
            Assert.That(AdelaideBuildings.All.Count(b => b.Kind == AdelaideBuildingKind.Hangar),
                Is.GreaterThanOrEqualTo(10));
        }

        [Test]
        public void Snapshot_DoesNotDuplicateExistingDetailedShells()
        {
            Assert.That(AdelaideBuildings.All.Any(b =>
                b.Name.IndexOf("Domestic & International Terminal", System.StringComparison.OrdinalIgnoreCase) >= 0), Is.False);
            Assert.That(AdelaideBuildings.All.Any(b =>
                b.Name.IndexOf("Flying Doctor", System.StringComparison.OrdinalIgnoreCase) >= 0), Is.False);
        }

        [Test]
        public void Snapshot_FootprintsAndHeightsAreRenderSafe()
        {
            foreach (var building in AdelaideBuildings.All)
            {
                Assert.That(building.Id, Is.Not.Empty);
                Assert.That(building.Xz.Length, Is.GreaterThanOrEqualTo(6), building.Name);
                Assert.That(building.Xz.Length % 2, Is.Zero, building.Name);
                Assert.That(building.HeightMetres, Is.InRange(3f, 60f), building.Name);
                for (var i = 0; i + 1 < building.Xz.Length; i += 2)
                {
                    Assert.That(building.Xz[i], Is.InRange(-1100f, 1950f), building.Name);
                    Assert.That(building.Xz[i + 1], Is.InRange(100f, 1600f), building.Name);
                }
            }
        }
    }
}
