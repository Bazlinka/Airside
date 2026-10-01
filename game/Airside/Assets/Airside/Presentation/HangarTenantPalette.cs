using System;
using System.Collections.Generic;

namespace Airside.Presentation
{
    /// <summary>
    /// Stylised hangar cladding / door / roof colours keyed by tenant name (ADR 0222).
    /// Pure hex strings — no UnityEngine — so headless tests lock the same values the
    /// pavement builder paints. Muted cladding bands, not photoreal brand livery.
    /// </summary>
    public readonly struct HangarTenantColours
    {
        public HangarTenantColours(string key, string shellHex, string doorHex, string roofHex, string accentHex)
        {
            Key = key ?? throw new ArgumentNullException(nameof(key));
            ShellHex = shellHex ?? throw new ArgumentNullException(nameof(shellHex));
            DoorHex = doorHex ?? throw new ArgumentNullException(nameof(doorHex));
            RoofHex = roofHex ?? throw new ArgumentNullException(nameof(roofHex));
            AccentHex = accentHex ?? throw new ArgumentNullException(nameof(accentHex));
        }

        public string Key { get; }
        public string ShellHex { get; }
        public string DoorHex { get; }
        public string RoofHex { get; }
        public string AccentHex { get; }
    }

    public static class HangarTenantPalette
    {
        public static readonly HangarTenantColours Default = new(
            "Default", "#7A8080", "#B3B8B8", "#8F9494", "#383B3D");

        public static readonly HangarTenantColours Rfds = new(
            "RFDS", "#E4DFD6", "#A83A3C", "#9A9590", "#F2EEE6");

        public static readonly HangarTenantColours RegionalExpress = new(
            "Regional Express", "#B8BDC2", "#C45A32", "#8E9398", "#E8E4DC");

        public static readonly HangarTenantColours Cobham = new(
            "Cobham", "#5A6B7A", "#3D4A56", "#6A7A88", "#A8B8C4");

        public static readonly HangarTenantColours SharpAirlines = new(
            "Sharp Airlines", "#D4CFC2", "#3A3C40", "#8A8680", "#2F6A6C");

        public static readonly HangarTenantColours PilatusAustralia = new(
            "Pilatus Australia", "#E6E8EA", "#9A4545", "#9AA0A4", "#4A4E52");

        public static readonly HangarTenantColours PulseAviation = new(
            "Pulse Aviation", "#9AA0A4", "#C9A12A", "#7E8488", "#3A3C40");

        public static readonly HangarTenantColours Aerobond = new(
            "Aerobond", "#A8A098", "#6B5648", "#8A847C", "#D4C8B8");

        public static readonly HangarTenantColours SapolAviationUnit = new(
            "SAPOL Aviation Unit", "#B0B8C0", "#2C3E5A", "#8A9098", "#C4A35A");

        /// <summary>Reserved until an OSM hangar carries this name.</summary>
        public static readonly HangarTenantColours AeroClub = new(
            "Aero Club", "#9AA892", "#4F6F60", "#7E8878", "#E8E4DC");

        /// <summary>Reserved until an OSM hangar carries this name.</summary>
        public static readonly HangarTenantColours AdelaideAeroClub = new(
            "Adelaide Aero Club", "#8FA090", "#3E5A48", "#748070", "#C8B286");

        /// <summary>Reserved until an OSM hangar carries this name.</summary>
        public static readonly HangarTenantColours Qantas = new(
            "Qantas", "#B8A8A8", "#B83A3A", "#8A8080", "#E8E4DC");

        private static readonly HangarTenantColours[] Known =
        {
            Default, Rfds, RegionalExpress, Cobham, SharpAirlines, PilatusAustralia,
            PulseAviation, Aerobond, SapolAviationUnit, AeroClub, AdelaideAeroClub, Qantas
        };

        public static IReadOnlyList<HangarTenantColours> AllKnown => Known;

        /// <summary>Stable tenant key for mesh batching (Cobham Hangar A → Cobham).</summary>
        public static string KeyFor(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return Default.Key;

            if (Contains(name, "Flying Doctor") || Contains(name, "RFDS"))
                return Rfds.Key;
            if (Contains(name, "Regional Express") || EqualsOrdinal(name, "Rex"))
                return RegionalExpress.Key;
            if (Contains(name, "Cobham"))
                return Cobham.Key;
            if (Contains(name, "Sharp"))
                return SharpAirlines.Key;
            if (Contains(name, "Pilatus"))
                return PilatusAustralia.Key;
            if (Contains(name, "Pulse"))
                return PulseAviation.Key;
            if (Contains(name, "Aerobond"))
                return Aerobond.Key;
            if (Contains(name, "SAPOL"))
                return SapolAviationUnit.Key;
            if (Contains(name, "Adelaide Aero Club"))
                return AdelaideAeroClub.Key;
            if (Contains(name, "Aero Club"))
                return AeroClub.Key;
            if (Contains(name, "Qantas"))
                return Qantas.Key;
            return Default.Key;
        }

        public static HangarTenantColours Resolve(string name)
        {
            var key = KeyFor(name);
            foreach (var entry in Known)
            {
                if (entry.Key == key)
                    return entry;
            }

            return Default;
        }

        private static bool Contains(string name, string needle) =>
            name.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0;

        private static bool EqualsOrdinal(string name, string needle) =>
            string.Equals(name.Trim(), needle, StringComparison.OrdinalIgnoreCase);
    }
}
