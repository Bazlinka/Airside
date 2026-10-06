using System;
using System.Collections.Generic;
using Airside.Domain;

namespace Airside.Simulation
{
    public static class AdelaideGroundPolicy
    {
        private static readonly HashSet<string> Unavailable = new();
        private static string Key(StableId stand, AircraftType type, RunwayDirection runway, bool outbound) =>
            stand.Value + "/" + type?.Id + "/" + runway + "/" + outbound;

        public static bool RouteAvailable(StableId stand, AircraftType type, RunwayDirection runway, bool outbound)
        {
            type ??= AdelaideGround.IsTerminalGate(stand) ? AircraftType.Boeing7378 : AircraftType.Atr42;
            if (outbound) AdelaideGround.TaxiOut(stand, type, runway);
            else AdelaideGround.TaxiIn(stand, type, runway);
            return !Unavailable.Contains(Key(stand, type, runway, outbound));
        }

        /// <summary>Preserve the painted gate lead-in, replace the taxiway section with a permitted route.</summary>
        internal static float[] Outbound(float[] taxi, StableId stand, AircraftType type, RunwayDirection runway)
        {
            if (!AdelaideGround.IsTerminalGate(stand)) return taxi;
            // The authored push can end in the apron lead-in, outside the OSM taxiway graph.
            // Preserve only that connection, then require a route on the permitted graph.
            for (var join = 0; join + 3 < taxi.Length; join += 2)
            {
                if (AdelaideTaxiRouter.DistanceToCentreline(taxi[join], taxi[join + 1]) > 3f) continue;
                if (AdelaideTaxiRouter.TryRoute(taxi[join], taxi[join + 1], taxi[taxi.Length - 2], taxi[taxi.Length - 1],
                        type, stand, out var route))
                {
                    var prefix = new float[join + 2];
                    Array.Copy(taxi, prefix, prefix.Length);
                    return join == 0 ? route : GroundPathSmoothing.Join(prefix, route);
                }
                break;
            }
            Unavailable.Add(Key(stand, type, runway, true));
            return taxi; // display-only plan; ground control cannot release this route.
        }

        internal static float[] Inbound(float[] taxi, StableId stand, AircraftType type, RunwayDirection runway)
        {
            if (!RunwayWeather.IsMainRunway(runway)) return taxi;
            var vacate = AdelaideGround.VacateFor(type, runway);
            var start = vacate.PoseAt(vacate.Seconds);
            // Keep the last 100 m painted stand approach; graph routing stops on the taxilane.
            var join = Math.Max(0, taxi.Length - 2);
            var remaining = 0f;
            while (join > 0 && remaining < 100f)
            {
                var dx = taxi[join] - taxi[join - 2];
                var dz = taxi[join + 1] - taxi[join - 1];
                remaining += (float)Math.Sqrt(dx * dx + dz * dz);
                join -= 2;
            }
            if (AdelaideTaxiRouter.TryRoute(start.X, start.Z, taxi[join], taxi[join + 1], type,
                    default, out var route))
            {
                var tail = new float[taxi.Length - join];
                Array.Copy(taxi, join, tail, 0, tail.Length);
                return GroundPathSmoothing.Join(route, tail);
            }
            Unavailable.Add(Key(stand, type, runway, false));
            return taxi;
        }

        /// <summary>Use the first compatible forward exit. No backtracking or 180° runway turn.</summary>
        internal static float[] Vacate(AircraftType type, RunwayDirection runway, float[] baked)
        {
            if (!RunwayWeather.IsMainRunway(runway)
                || AircraftCatalogue.TryFor(type, out var spec) && spec.CodeLetter <= 'C') return baked;
            // 05: L2 is the next compatible exit after E2. 23: F4 for Code D, F5 for E.
            var exit = runway == RunwayDirection.Runway05 ? "L2"
                : spec?.CodeLetter == 'D' ? "F4" : "F5";
            var chosen = default(AdelaideTaxiway);
            var best = float.MaxValue;
            foreach (var taxiway in AdelaideLayout.Taxiways)
            {
                if (taxiway.Reference != exit) continue;
                var last = taxiway.Xz.Length - 2;
                var endpoint = Math.Abs(taxiway.Xz[1]) < Math.Abs(taxiway.Xz[last + 1]) ? 0 : last;
                var distance = Math.Abs(taxiway.Xz[endpoint] - baked[0]);
                if (Math.Abs(taxiway.Xz[endpoint + 1]) > 8f || distance >= best) continue;
                best = distance;
                chosen = taxiway;
            }
            if (chosen.Xz == null) throw new InvalidOperationException("Compatible Adelaide exit missing: " + exit);
            var end = Math.Abs(chosen.Xz[1]) < Math.Abs(chosen.Xz[chosen.Xz.Length - 1]) ? chosen.Xz.Length - 2 : 0;
            if (!AdelaideTaxiRouter.TryRoute(baked[0], baked[1], chosen.Xz[end], chosen.Xz[end + 1],
                    type, default, out var route, runway))
                throw new InvalidOperationException("No compatible Adelaide runway exit: " + exit);
            return route;
        }
    }
}
