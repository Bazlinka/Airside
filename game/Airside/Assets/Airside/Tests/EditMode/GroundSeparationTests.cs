using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Airside.Domain;
using Airside.Presentation;
using Airside.Simulation;
using NUnit.Framework;

namespace Airside.Tests
{
    /// <summary>
    /// A whole busy Adelaide day, sampled every 2 s: no two aircraft moving or queued on the
    /// ground may be drawn driving through each other. Before ground control there were ~36
    /// such episodes a day — taxi-in head-on into taxi-out, taxi-outs into the holding queue,
    /// vacates into waiting arrivals. Parked neighbours are a stand-allocation question and
    /// are not counted here.
    /// </summary>
    public sealed class GroundSeparationTests
    {
        private struct Placed
        {
            public FleetAircraft Aircraft;
            public string Leg;
            public double X, Z, Half;
        }

        [Test]
        public void BusyDay_NoAircraftDriveThroughEachOther()
        {
            var clock = new ManualSimulationClock(new SimulationTime(0));
            var ops = AirlineOperations.StartAtAdelaide(clock, new SeededRandomSource(2026),
                Airline.Player("Probe Air", "#1F3A93"));
            var counts = new Dictionary<string, int>();
            var examples = new Dictionary<string, string>();
            var episodes = new Dictionary<string, double>();
            var placed = new List<Placed>();
            const double dt = 2.0;
            for (var t = 0.0; t < 24 * 3600; t += dt)
            {
                clock.Set(new SimulationTime((long)t));
                ops.Update();
                var hour = ops.Clock.LocalAt(clock.Now).Hour;
                if (hour >= AirportCurfew.ClosedFromHour || hour < AirportCurfew.OpensAtHour + 1)
                    continue;
                placed.Clear();
                foreach (var a in ops.Fleet)
                    if (TryPlace(ops, a, t, out var p))
                        placed.Add(p);
                for (var i = 0; i < placed.Count; i++)
                for (var j = i + 1; j < placed.Count; j++)
                {
                    var a = placed[i];
                    var b = placed[j];
                    var d = Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Z - b.Z) * (a.Z - b.Z));
                    if (a.Leg == "Parked" && b.Leg == "Parked")
                        continue;
                    // Next to its own stand, beside a parked neighbour: gate spacing, not traffic.
                    if (a.Leg == "Parked" && NearOwnStand(b) || b.Leg == "Parked" && NearOwnStand(a))
                        continue;
                    if (!GroundTraffic.TooClose(new GroundPose((float)a.X, (float)a.Z, 1, 0, 0, false), a.Half,
                            new GroundPose((float)b.X, (float)b.Z, 1, 0, 0, false), b.Half))
                        continue;
                    var key = string.CompareOrdinal(a.Leg, b.Leg) <= 0 ? $"{a.Leg} x {b.Leg}" : $"{b.Leg} x {a.Leg}";
                    var pair = string.CompareOrdinal(a.Aircraft.Registration, b.Aircraft.Registration) < 0
                        ? a.Aircraft.Registration + "|" + b.Aircraft.Registration
                        : b.Aircraft.Registration + "|" + a.Aircraft.Registration;
                    // Count distinct episodes (a pair clashing again after 60 s apart counts again).
                    if (episodes.TryGetValue(pair, out var last) && t - last < 60)
                    {
                        episodes[pair] = t;
                        continue;
                    }
                    episodes[pair] = t;
                    counts[key] = counts.TryGetValue(key, out var n) ? n + 1 : 1;
                    if (!examples.ContainsKey(key))
                        examples[key] = $"t={t:0} {a.Aircraft.Registration}({a.Aircraft.Type.Id} {a.Aircraft.State} {a.Aircraft.AssignedRunway}) @({a.X:0},{a.Z:0}) vs {b.Aircraft.Registration}({b.Aircraft.Type.Id} {b.Aircraft.State} {b.Aircraft.AssignedRunway}) @({b.X:0},{b.Z:0}) d={d:0}";
                }
            }

            var sb = new StringBuilder();
            foreach (var pair in counts.OrderByDescending(p => p.Value))
                sb.AppendLine($"{pair.Value,4}  {pair.Key}   e.g. {examples[pair.Key]}");
            Assert.That(counts, Is.Empty, sb.ToString());
        }

        private static bool NearOwnStand(Placed moving)
        {
            var stand = moving.Aircraft.State == FleetState.TaxiOut ? moving.Aircraft.DepartureStand : moving.Aircraft.Stand;
            if (string.IsNullOrEmpty(stand.Value))
                return false;
            var pose = AdelaideGround.StandPose(stand);
            var dx = pose.X - moving.X;
            var dz = pose.Z - moving.Z;
            return dx * dx + dz * dz < 60.0 * 60.0;
        }

        private static bool TryPlace(AirlineOperations ops, FleetAircraft aircraft, double now, out Placed placed)
        {
            placed = default;
            var visual = FleetVisual.For(aircraft, new SimulationTime((long)now));
            if (!visual.Visible)
                return false;
            var half = GroundTraffic.HalfSpan(aircraft.Type);
            if (visual.Leg == FleetGroundLeg.None)
            {
                var phase = visual.Phase;
                if (phase is not (AircraftPhase.Landing or AircraftPhase.Takeoff))
                    return false;
                var duration = AirsideFlightPath.PhaseSeconds(phase, aircraft.Type);
                var progress = (float)Math.Max(0, Math.Min(1, (now - visual.PhaseStartedAt.ElapsedSeconds) / duration));
                var v = phase == AircraftPhase.Landing
                    ? AirsideFlightPath.Landing(progress, 0f, aircraft.Type)
                    : AirsideFlightPath.Takeoff(progress, 0f, aircraft.Type);
                if (v.y - AirsideFlightPath.GroundY > 3f)
                    return false;
                RunwayFrame.ToWorld(aircraft.AssignedRunway, v.x, 0f, v.z, out var wx, out _, out var wz);
                placed = new Placed { Aircraft = aircraft, Leg = phase == AircraftPhase.Landing ? "Rollout" : "TakeoffRoll", X = wx, Z = wz, Half = half };
                return true;
            }

            GroundPose pose;
            string leg;
            if (visual.Leg == FleetGroundLeg.Parked)
            {
                pose = AdelaideGround.StandPose(aircraft.Stand);
                leg = "Parked";
            }
            else if (!GroundTraffic.TryPose(ops.Fleet, aircraft, now, out pose, out var groundLeg))
                return false;
            else
                leg = groundLeg.ToString();
            placed = new Placed { Aircraft = aircraft, Leg = leg, X = pose.X, Z = pose.Z, Half = half };
            return true;
        }
    }
}
