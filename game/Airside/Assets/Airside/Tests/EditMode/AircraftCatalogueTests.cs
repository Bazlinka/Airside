using System.Collections.Generic;
using System.IO;
using System.Linq;
using Airside.Domain;
using Airside.Presentation;
using Airside.Simulation;
using NUnit.Framework;

namespace Airside.Tests
{
    /// <summary>ADR 0048: one aircraft catalogue, true-scale genuine models, honest placeholders, per-type planning.</summary>
    public sealed class AircraftCatalogueTests
    {
        [Test]
        public void Catalogue_IsTheSingleSourceOfNamedTypes()
        {
            Assert.That(AircraftCatalogue.All.Select(s => s.Id).Distinct().Count(), Is.EqualTo(AircraftCatalogue.All.Count));
            Assert.That(AircraftType.Atr42, Is.SameAs(AircraftCatalogue.Atr42.Type));
            Assert.That(AircraftType.Saab340, Is.SameAs(AircraftCatalogue.Saab340.Type));
            Assert.That(AircraftType.Dash8Q400, Is.SameAs(AircraftCatalogue.Dash8Q400.Type));
            Assert.That(AircraftType.EmbraerE190, Is.SameAs(AircraftCatalogue.EmbraerE190.Type));
            Assert.That(AircraftType.AirbusA220300, Is.SameAs(AircraftCatalogue.AirbusA220300.Type));
            Assert.That(AircraftType.AirbusA320200, Is.SameAs(AircraftCatalogue.AirbusA320200.Type));
            Assert.That(AircraftType.Boeing737800, Is.SameAs(AircraftCatalogue.Boeing737800.Type));
            Assert.That(AircraftType.Boeing7378, Is.SameAs(AircraftCatalogue.Boeing7378.Type));
            Assert.That(AircraftType.AirbusA321Neo, Is.SameAs(AircraftCatalogue.AirbusA321Neo.Type));
            Assert.That(AircraftType.AirbusA350900, Is.SameAs(AircraftCatalogue.AirbusA350900.Type));
            Assert.That(AircraftType.Boeing78710, Is.SameAs(AircraftCatalogue.Boeing78710.Type));
            Assert.That(AircraftType.AirbusA330900, Is.SameAs(AircraftCatalogue.AirbusA330900.Type));
            Assert.That(AircraftType.Boeing7879, Is.SameAs(AircraftCatalogue.Boeing7879.Type));
            foreach (var spec in AircraftCatalogue.All)
            {
                Assert.That(AircraftType.TryFromId(spec.Id, out var type), Is.True);
                Assert.That(AircraftCatalogue.For(type), Is.SameAs(spec));
                Assert.That(spec.Role, Is.Not.Empty);
                Assert.That(spec.SourceId, Does.StartWith("SPEC-"));
                Assert.That(spec.LengthMetres * spec.WingspanMetres * spec.HeightMetres, Is.GreaterThan(0));
            }
        }

        [Test]
        public void PlanningFigures_NeverExceedTheRecordedManufacturerFigures()
        {
            foreach (var spec in AircraftCatalogue.All)
            {
                if (spec.ManufacturerMaxCruiseKmh > 0)
                    Assert.That(spec.PlanningCruiseKmh, Is.LessThanOrEqualTo(spec.ManufacturerMaxCruiseKmh), spec.Name);
                if (spec.ManufacturerRangeKm > 0)
                    Assert.That(spec.PracticalRangeKm, Is.LessThanOrEqualTo(spec.ManufacturerRangeKm), spec.Name);
            }
        }

        [Test]
        public void GenuineModels_MatchRealDimensionsWithinFivePercent_AndHaveTheirOwnThumbnail()
        {
            var genuine = AircraftCatalogue.All.Where(s => s.ModelStatus == ModelStatus.Genuine).ToList();
            Assert.That(genuine.Select(s => s.Id), Is.EquivalentTo(new[]
            {
                "ATR42", "SF34", "DH8D", "E190", "A223", "A320", "B738",
                "B38M", "A21N", "A339", "A359", "B789", "B78X"
            }));
            var thumbnails = new HashSet<string>();
            foreach (var spec in genuine)
            {
                Assert.That(AircraftModelBounds.TryMeasure(spec, out var length, out var span, out var height), Is.True, spec.Name);
                Assert.That(AircraftModelBounds.WithinTolerance(length, spec.LengthMetres), Is.True, $"{spec.Name} length {length:0.00} m vs {spec.LengthMetres}");
                Assert.That(AircraftModelBounds.WithinTolerance(span, spec.WingspanMetres), Is.True, $"{spec.Name} span {span:0.00} m vs {spec.WingspanMetres}");
                Assert.That(AircraftModelBounds.WithinTolerance(height, spec.HeightMetres), Is.True, $"{spec.Name} height {height:0.00} m vs {spec.HeightMetres}");

                var thumb = ArtRuntimePaths.ResolveExisting(spec.ThumbnailPath);
                Assert.That(thumb, Is.Not.Null, $"{spec.Name} thumbnail");
                var header = File.ReadAllBytes(thumb).Take(24).ToArray();
                Assert.That(header[1] == 'P' && header[2] == 'N' && header[3] == 'G', Is.True);
                var width = (header[16] << 24) | (header[17] << 16) | (header[18] << 8) | header[19];
                var heightPx = (header[20] << 24) | (header[21] << 16) | (header[22] << 8) | header[23];
                Assert.That((width, heightPx), Is.EqualTo((480, 320)), "consistent thumbnail size");
                Assert.That(thumbnails.Add(spec.ThumbnailPath), Is.True, "no type borrows another's thumbnail");
            }
        }

        [Test]
        public void Placeholders_HaveNoBorrowedModelOrThumbnail()
        {
            Assert.That(
                AircraftCatalogue.All.Where(s => s.ModelStatus == ModelStatus.Placeholder).Select(s => s.Id),
                Is.Empty,
                "every live Adelaide type now has its own genuine model");
        }

        [Test]
        public void StandClasses_DriveStandCompatibility()
        {
            foreach (var spec in AircraftCatalogue.All)
            {
                var gate = spec.StandClass == StandClass.TerminalGate;
                Assert.That(AirlineOperations.NeedsTerminalGate(spec.Type), Is.EqualTo(gate), spec.Name);
                Assert.That(AirlineOperations.StandFits(spec.Type, new StableId("GATE-13")), Is.EqualTo(gate), spec.Name);
                Assert.That(AirlineOperations.StandFits(spec.Type, new StableId("BAY-1")), Is.EqualTo(!gate), spec.Name);
            }
        }

        [Test]
        public void FlightPlanning_UsesEachTypesOwnCruiseAndPracticalRange()
        {
            var clock = new ManualSimulationClock(new SimulationTime(0));
            var ops = new AirlineOperations(clock, new SeededRandomSource(5), DestinationCatalogue.Adelaide, AirlineOperations.AdelaideStands);
            // Range checks must not be gated by the player's opening float: a 787 to Perth
            // costs more than a Provisional airline starts with, and this test is about
            // CanReach, not career funds. A non-player airline is not charged.
            var airline = Airline.Rex();
            ops.AddAirline(airline);
            var bays = new Queue<StableId>(AirlineOperations.AdelaideRegionalBays);
            var terminalGates = new Queue<StableId>(AirlineOperations.AdelaideTerminalGates);
            foreach (var spec in AircraftCatalogue.All)
            {
                var stand = spec.StandClass == StandClass.TerminalGate ? terminalGates.Dequeue() : bays.Dequeue();
                var aircraft = ops.AddAircraft(airline, "VH-C" + spec.Id.Substring(0, 2), spec.Type, stand);
                foreach (var row in FlightPlanner.DestinationsFor(ops, aircraft))
                {
                    var km = ops.DistanceKm(row.Destination);
                    Assert.That(row.Reachable, Is.EqualTo(km <= spec.PracticalRangeKm), $"{spec.Name} to {row.Destination.Code}");
                    Assert.That(row.AirborneSeconds, Is.EqualTo(LegTiming.AirborneSeconds(km, spec.Type)));
                    var result = ops.ScheduleDeparture(aircraft, row.Destination, new SimulationTime(600));
                    Assert.That(result.Accepted, Is.EqualTo(row.Reachable), $"{spec.Name} to {row.Destination.Code}: offered only when reachable");
                }

                ops.CancelDeparture(aircraft);
            }

            Assert.That(LegTiming.AirborneSeconds(642, AircraftType.Boeing7378),
                Is.LessThan(LegTiming.AirborneSeconds(642, AircraftType.Saab340)), "a jet's own cruise speed makes the same leg shorter");
        }

        [Test]
        public void Dash8RangeCorrection_KeepsQantasLinkNetworkReachable()
        {
            foreach (var (code, _) in AirlineOperations.QantasLinkNetwork)
            {
                DestinationCatalogue.TryFind(code, out var destination);
                Assert.That(AircraftType.Dash8Q400.CanReach(DestinationCatalogue.Adelaide.DistanceKmTo(destination)), Is.True, code);
            }
        }

        [Test]
        public void Ownership_OneRuleForPlayerAndOtherOperators()
        {
            var player = Airline.Player("Mine Air", "#C8102E");
            var other = Airline.Rex();
            Assert.That(Ownership.SectionFor(player), Is.EqualTo("YOUR AIRLINE"));
            Assert.That(Ownership.SectionFor(other), Is.EqualTo("OTHER OPERATORS"));
            Assert.That(Ownership.BadgeFor(player), Is.EqualTo("YOURS"));
            Assert.That(Ownership.BadgeFor(other), Is.Null);
            Assert.That(Ownership.AlphaFor(player), Is.EqualTo(1f));
            Assert.That(Ownership.AlphaFor(other), Is.LessThan(1f));

            var clock = new ManualSimulationClock(new SimulationTime(0));
            var ops = AirlineOperations.StartAtAdelaide(clock, new SeededRandomSource(3), Airline.Player("Board Air", "#2E7D32"));
            var rows = ops.Fleet.ToList();
            FlightBoard.Sort(rows);
            var others = rows.Where(a => !a.Airline.IsPlayer).ToList();
            FlightBoard.GroupByOwnership(rows);
            Assert.That(rows.TakeWhile(a => a.Airline.IsPlayer).Count(), Is.EqualTo(ops.Fleet.Count(a => a.Airline.IsPlayer)));
            Assert.That(rows.SkipWhile(a => a.Airline.IsPlayer), Is.EqualTo(others), "time order kept within each section");
        }
    }
}
