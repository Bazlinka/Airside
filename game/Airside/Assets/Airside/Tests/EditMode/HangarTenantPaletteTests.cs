using System.Linq;
using System.Text.RegularExpressions;
using Airside.Presentation;
using Airside.Simulation;
using NUnit.Framework;

namespace Airside.Tests
{
    /// <summary>ADR 0222 — named hangars resolve to distinct stylised cladding bands.</summary>
    public sealed class HangarTenantPaletteTests
    {
        private static readonly Regex Hex = new("^#[0-9A-Fa-f]{6}$", RegexOptions.CultureInvariant);

        [Test]
        public void Resolve_RegionalExpress_IsNotDefault_AndDoorDiffersFromShell()
        {
            var colours = HangarTenantPalette.Resolve("Regional Express");
            Assert.That(colours.Key, Is.EqualTo(HangarTenantPalette.RegionalExpress.Key));
            Assert.That(colours.ShellHex, Is.Not.EqualTo(HangarTenantPalette.Default.ShellHex));
            Assert.That(colours.DoorHex, Is.Not.EqualTo(colours.ShellHex));
        }

        [Test]
        public void KeyFor_CobhamVariants_ShareOneKey()
        {
            Assert.That(HangarTenantPalette.KeyFor("Cobham Hangar A"),
                Is.EqualTo(HangarTenantPalette.KeyFor("Cobham")));
            Assert.That(HangarTenantPalette.KeyFor("Cobham Hangar B"),
                Is.EqualTo(HangarTenantPalette.KeyFor("Cobham")));
        }

        [Test]
        public void KeyFor_BothPilatusSites_ShareOneKey()
        {
            Assert.That(HangarTenantPalette.KeyFor("Pilatus Australia"),
                Is.EqualTo(HangarTenantPalette.PilatusAustralia.Key));
        }

        [Test]
        public void Resolve_UnnamedSupportHangar_IsDefaultGreys()
        {
            var colours = HangarTenantPalette.Resolve("Airport support building 105581518");
            Assert.That(colours.Key, Is.EqualTo(HangarTenantPalette.Default.Key));
            Assert.That(colours.ShellHex, Is.EqualTo("#7A8080"));
            Assert.That(colours.DoorHex, Is.EqualTo("#B3B8B8"));
            Assert.That(colours.RoofHex, Is.EqualTo("#8F9494"));
        }

        [Test]
        public void Resolve_FlyingDoctorNames_AreRfds()
        {
            Assert.That(HangarTenantPalette.Resolve("Royal Flying Doctor Service").Key,
                Is.EqualTo(HangarTenantPalette.Rfds.Key));
            Assert.That(HangarTenantPalette.Resolve("Flying Doctor").Key,
                Is.EqualTo(HangarTenantPalette.Rfds.Key));
            Assert.That(HangarTenantPalette.Resolve("Royal Flying Doctor Service").Key,
                Is.Not.EqualTo(HangarTenantPalette.Default.Key));
        }

        [Test]
        public void ReservedKeys_ExistAndAreDistinct()
        {
            Assert.That(HangarTenantPalette.AeroClub.Key, Is.EqualTo("Aero Club"));
            Assert.That(HangarTenantPalette.AdelaideAeroClub.Key, Is.EqualTo("Adelaide Aero Club"));
            Assert.That(HangarTenantPalette.Qantas.Key, Is.EqualTo("Qantas"));
            Assert.That(HangarTenantPalette.AeroClub.ShellHex,
                Is.Not.EqualTo(HangarTenantPalette.AdelaideAeroClub.ShellHex));
            Assert.That(HangarTenantPalette.Qantas.DoorHex,
                Is.Not.EqualTo(HangarTenantPalette.Default.DoorHex));
        }

        [Test]
        public void EveryAdelaideHangar_ResolvesWithoutThrow_AndNamedOnesDifferFromDefault()
        {
            foreach (var building in AdelaideBuildings.All.Where(b => b.Kind == AdelaideBuildingKind.Hangar))
            {
                var colours = HangarTenantPalette.Resolve(building.Name);
                Assert.That(colours.Key, Is.Not.Null.And.Not.Empty, building.Name);
                if (building.Name.StartsWith("Airport support building"))
                    Assert.That(colours.Key, Is.EqualTo(HangarTenantPalette.Default.Key), building.Name);
                else
                    Assert.That(colours.Key, Is.Not.EqualTo(HangarTenantPalette.Default.Key), building.Name);
            }
        }

        [Test]
        public void AllKnown_HexesAreSixDigit_AndShellDoorRoofDistinct()
        {
            foreach (var entry in HangarTenantPalette.AllKnown)
            {
                Assert.That(Hex.IsMatch(entry.ShellHex), Is.True, entry.Key + " shell");
                Assert.That(Hex.IsMatch(entry.DoorHex), Is.True, entry.Key + " door");
                Assert.That(Hex.IsMatch(entry.RoofHex), Is.True, entry.Key + " roof");
                Assert.That(Hex.IsMatch(entry.AccentHex), Is.True, entry.Key + " accent");
                Assert.That(entry.ShellHex, Is.Not.EqualTo(entry.DoorHex), entry.Key);
                Assert.That(entry.ShellHex, Is.Not.EqualTo(entry.RoofHex), entry.Key);
                Assert.That(entry.DoorHex, Is.Not.EqualTo(entry.RoofHex), entry.Key);
            }
        }

        [Test]
        public void Resolve_IsDeterministic()
        {
            var a = HangarTenantPalette.Resolve("Sharp Airlines");
            var b = HangarTenantPalette.Resolve("Sharp Airlines");
            Assert.That(a.ShellHex, Is.EqualTo(b.ShellHex));
            Assert.That(a.DoorHex, Is.EqualTo(b.DoorHex));
            Assert.That(a.RoofHex, Is.EqualTo(b.RoofHex));
        }
    }
}
