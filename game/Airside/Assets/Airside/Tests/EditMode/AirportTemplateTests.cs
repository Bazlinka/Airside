using System;
using System.Collections.Generic;
using System.Linq;
using Airside.Domain;
using Airside.Simulation;
using NUnit.Framework;

namespace Airside.Tests
{
    public sealed class AirportTemplateTests
    {
        private static readonly SurfaceWind Calm = new SurfaceWind(0, 0);

        [Test]
        public void EveryAustralianDestination_HasATemplate()
        {
            foreach (var destination in DestinationCatalogue.Australia)
            {
                Assert.That(AirportTemplates.TryFor(destination.Code, out var template), Is.True, destination.Code);
                Assert.That(template.Name, Is.EqualTo(destination.Name), destination.Code);
                Assert.That(template.State, Is.EqualTo(destination.State), destination.Code);
            }
        }

        [Test]
        public void Templates_AreInternallyConsistent()
        {
            var icaos = new HashSet<string>();
            foreach (var airport in AirportTemplates.All)
            {
                Assert.That(icaos.Add(airport.Icao), Is.True, "duplicate ICAO " + airport.Icao);
                Assert.That(airport.Icao, Does.StartWith("Y"), airport.Iata);
                Assert.That(airport.Runways.Any(r => r.Sealed), Is.True, airport.Iata + " needs a sealed runway");
                Assert.That(airport.AllRunwayEnds.Any(e => e.Designator.StartsWith(airport.CalmWindRunwayEnd, StringComparison.Ordinal)),
                    Is.True, airport.Iata + " calm-wind end must exist");
                var gates = airport.AllGates.ToList();
                Assert.That(gates, Is.Not.Empty, airport.Iata);
                Assert.That(gates.Select(g => g.Id).Distinct().Count(), Is.EqualTo(gates.Count), airport.Iata + " gate ids unique");
                foreach (var end in airport.AllRunwayEnds)
                    Assert.That(end.HeadingDegrees, Is.InRange(10, 360), airport.Iata + " " + end);
                foreach (var runway in airport.Runways)
                {
                    var low = runway.End(airport.Iata, false).HeadingDegrees;
                    var high = runway.End(airport.Iata, true).HeadingDegrees;
                    var apart = Math.Abs(low - high);
                    Assert.That(apart, Is.EqualTo(180), airport.Iata + " " + runway.Id + " ends are opposite");
                }
            }
        }

        [Test]
        public void Figures_MatchTheDataSheet()
        {
            AirportTemplates.TryFor("MEL", out var melbourne);
            Assert.That(melbourne.Runways[0].Id, Is.EqualTo("16/34"));
            Assert.That(melbourne.Runways[0].LengthMetres, Is.EqualTo(3657));
            AirportTemplates.TryFor("SYD", out var sydney);
            Assert.That(sydney.Runways.Select(r => r.Id), Is.EquivalentTo(new[] { "16R/34L", "07/25", "16L/34R" }));
            AirportTemplates.TryFor("CNS", out var cairns);
            Assert.That(cairns.Terminals.Single(t => t.Id == "DOM").Gates.Count, Is.EqualTo(17));
            Assert.That(cairns.Terminals.Single(t => t.Id == "INT").Gates.Count, Is.EqualTo(10));
        }

        [Test]
        public void OpenStreetMapAirports_HaveRealGateNumbers()
        {
            AirportTemplates.TryFor("MEL", out var melbourne);
            Assert.That(melbourne.Terminals.Select(t => t.Id), Is.EquivalentTo(new[] { "T1", "T2", "T3", "T4" }));
            Assert.That(melbourne.Terminals.Single(t => t.Id == "T4").Gates.Select(g => g.Id), Does.Contain("T4-41"));
            AirportTemplates.TryFor("BNE", out var brisbane);
            Assert.That(brisbane.Terminals.Single(t => t.Id == "INT").Gates.All(g => g.Use == GateUse.International), Is.True);
            AirportTemplates.TryFor("OOL", out var goldCoast);
            Assert.That(goldCoast.AllGates.Any(g => g.Use == GateUse.Swing), Is.True, "D10/I31 style gates serve both flows");
        }

        [Test]
        public void Airlines_GoToTheirOwnTerminal()
        {
            AirportTemplates.TryFor("MEL", out var melbourne);
            for (var i = 0; i < 20; i++)
            {
                var virgin = AirportArrivalPlanner.Plan(melbourne, AircraftType.Boeing737800, false, Calm, "v" + i, null, "VOZ");
                Assert.That(virgin.Gate.TerminalId, Is.EqualTo("T3"));
                var qantas = AirportArrivalPlanner.Plan(melbourne, AircraftType.Boeing737800, false, Calm, "q" + i, null, "QFA");
                Assert.That(qantas.Gate.TerminalId, Is.EqualTo("T1"));
            }

            AirportTemplates.TryFor("SYD", out var sydney);
            Assert.That(AirportArrivalPlanner.Plan(sydney, AircraftType.Boeing737800, false, Calm, "x", null, "JST").Gate.TerminalId, Is.EqualTo("T2"));
        }

        [Test]
        public void Adelaide_UsesTheRealStands()
        {
            AirportTemplates.TryFor("ADL", out var adelaide);
            Assert.That(adelaide.AllGates.Count(), Is.EqualTo(AdelaideLayout.Bays.Length + AdelaideLayout.TerminalGates.Length));
            Assert.That(adelaide.AllGates.Select(g => g.Id), Is.SupersetOf(AdelaideLayout.TerminalGates.Select(g => g.Id)));
            foreach (var id in AirlineOperations.CodeEGates)
                Assert.That(adelaide.AllGates.Single(g => g.Id == id.Value).MaxCodeLetter, Is.EqualTo('E'));
        }

        [Test]
        public void EveryFleetType_CanLandAndParkAtEveryAirport()
        {
            foreach (var airport in AirportTemplates.All)
            foreach (var spec in AircraftCatalogue.All)
            foreach (var international in new[] { false, true })
            {
                var arrival = AirportArrivalPlanner.Plan(airport, spec.Type, international, new SurfaceWind(200, 12), "VH-TST");
                if (spec.Type.IsRotorcraft)
                {
                    Assert.That(arrival.Gate, Is.Null, airport.Iata);
                    continue;
                }

                Assert.That(arrival.Runway.Sealed, Is.True, airport.Iata + " " + spec.Id);
                Assert.That(arrival.Gate, Is.Not.Null, airport.Iata + " " + spec.Id);
                Assert.That(arrival.Gate.TerminalId, Is.Not.Null);
            }
        }

        [Test]
        public void Runway_FollowsTheWind()
        {
            AirportTemplates.TryFor("MEL", out var melbourne);
            Assert.That(AirportArrivalPlanner.ChooseRunway(melbourne, AircraftType.AirbusA320200, new SurfaceWind(350, 15)).Designator,
                Is.EqualTo("34"));
            Assert.That(AirportArrivalPlanner.ChooseRunway(melbourne, AircraftType.AirbusA320200, new SurfaceWind(170, 15)).Designator,
                Is.EqualTo("16"));
            Assert.That(AirportArrivalPlanner.ChooseRunway(melbourne, AircraftType.AirbusA320200, new SurfaceWind(95, 20)).Designator,
                Is.EqualTo("09"));
        }

        [Test]
        public void LightWind_UsesTheCalmRunway()
        {
            AirportTemplates.TryFor("BNE", out var brisbane);
            var end = AirportArrivalPlanner.ChooseRunway(brisbane, AircraftType.Boeing737800, new SurfaceWind(190, 3));
            Assert.That(end.Designator, Does.StartWith("01"));
        }

        [Test]
        public void Sydney_KeepsWidebodiesOffTheShortParallel()
        {
            AirportTemplates.TryFor("SYD", out var sydney);
            var wind = new SurfaceWind(340, 12);
            Assert.That(AirportArrivalPlanner.ChooseRunway(sydney, AircraftType.AirbusA350900, wind).RunwayId, Is.EqualTo("16R/34L"));
            Assert.That(AirportArrivalPlanner.ChooseRunway(sydney, AircraftType.Atr42, wind).RunwayId, Is.Not.Null);
        }

        [Test]
        public void ShortRunways_AreNotUsedByLongRollTypes()
        {
            AirportTemplates.TryFor("PER", out var perth);
            // Wind straight down 06 would favour the short runway; a heavy twin still takes the long one.
            var end = AirportArrivalPlanner.ChooseRunway(perth, AircraftType.AirbusA350900, new SurfaceWind(60, 12));
            Assert.That(end.LengthMetres, Is.GreaterThanOrEqualTo(AircraftPerformance.For(AircraftType.AirbusA350900).TakeoffRollMetres));
        }

        [Test]
        public void InternationalFlights_GetInternationalOrSwingGates()
        {
            AirportTemplates.TryFor("MEL", out var melbourne);
            var gate = AirportArrivalPlanner.ChooseGate(melbourne, AircraftType.AirbusA350900, true, "VH-A", null, out _);
            Assert.That(gate.Use, Is.Not.EqualTo(GateUse.Domestic));
            Assert.That(gate.TerminalId, Is.EqualTo("T2"));
        }

        [Test]
        public void DomesticFlights_NeverGetInternationalOnlyGates()
        {
            AirportTemplates.TryFor("CNS", out var cairns);
            for (var i = 0; i < 40; i++)
            {
                var gate = AirportArrivalPlanner.ChooseGate(cairns, AircraftType.Boeing737800, false, "VH-" + i, null, out _);
                Assert.That(gate.Use, Is.Not.EqualTo(GateUse.International));
            }
        }

        [Test]
        public void SmallTypes_DoNotTakeWidebodyGates()
        {
            AirportTemplates.TryFor("SYD", out var sydney);
            var gate = AirportArrivalPlanner.ChooseGate(sydney, AircraftType.Dash8Q400, false, "VH-Q", null, out _);
            Assert.That(gate.MaxCodeLetter, Is.LessThanOrEqualTo('C'));
        }

        [Test]
        public void OccupiedGates_AreAvoided_WhileOthersAreFree()
        {
            AirportTemplates.TryFor("PER", out var perth);
            var occupied = new List<string>();
            for (var i = 0; i < 6; i++)
            {
                var gate = AirportArrivalPlanner.ChooseGate(perth, AircraftType.AirbusA320200, false, "seed" + i, occupied, out var free);
                Assert.That(free, Is.True);
                Assert.That(occupied, Does.Not.Contain(gate.Id));
                occupied.Add(gate.Id);
            }
        }

        [Test]
        public void FullApron_StillReturnsAGate_AndSaysItIsNotFree()
        {
            AirportTemplates.TryFor("KGC", out var kingscote);
            var occupied = kingscote.AllGates.Select(g => g.Id).ToList();
            var gate = AirportArrivalPlanner.ChooseGate(kingscote, AircraftType.Atr42, false, "VH-X", occupied, out var free);
            Assert.That(gate, Is.Not.Null);
            Assert.That(free, Is.False);
        }

        [Test]
        public void Planning_IsDeterministic()
        {
            AirportTemplates.TryFor("SYD", out var sydney);
            var wind = new SurfaceWind(250, 9);
            var a = AirportArrivalPlanner.Plan(sydney, AircraftType.Boeing737800, false, wind, "VH-ABC@3600");
            var b = AirportArrivalPlanner.Plan(sydney, AircraftType.Boeing737800, false, wind, "VH-ABC@3600");
            Assert.That(a.Gate.Id, Is.EqualTo(b.Gate.Id));
            Assert.That(a.Runway.Designator, Is.EqualTo(b.Runway.Designator));
        }

        [Test]
        public void Helicopter_UsesTheApron()
        {
            AirportTemplates.TryFor("MEL", out var melbourne);
            var arrival = AirportArrivalPlanner.Plan(melbourne, AircraftType.Bell412, false, Calm, "VH-HEL");
            Assert.That(arrival.Gate, Is.Null);
            Assert.That(arrival.Text, Does.Contain("apron"));
        }

        [Test]
        public void TryPlan_RefusesAnUnknownAirport()
        {
            Assert.That(AirportArrivalPlanner.TryPlan("XXX", AircraftType.Atr42, false, Calm, "s", null, out _), Is.False);
            Assert.That(AirportArrivalPlanner.TryPlan("MEL", AircraftType.Atr42, false, Calm, "s", null, out var arrival), Is.True);
            Assert.That(arrival.Text, Does.Contain("Gate"));
        }
    }
}
