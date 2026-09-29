using System.Collections.Generic;
using System.Linq;
using Airside.Domain;
using Airside.Simulation;
using NUnit.Framework;

namespace Airside.Tests
{
    /// <summary>
    /// Ramp crew and service vehicles were placed at the same fixed metres from every stand
    /// stop, whatever parked there. Turboprop art is rooted mid-airframe and jet art at the
    /// nose, so on a Saab the marshaller stood at the nose and a worker stood two metres from a
    /// turning propeller. Positions now come from each type's measured layout.
    /// </summary>
    public sealed class AircraftLayoutTests
    {
        private static IEnumerable<AircraftType> Types => AircraftCatalogue.All.Select(spec => spec.Type);

        private static readonly RampActivity[] Activities =
        {
            RampActivity.Arrival, RampActivity.Fuel, RampActivity.Catering, RampActivity.Baggage,
            RampActivity.Boarding, RampActivity.Pushback
        };

        [Test]
        public void EveryCrewMemberOnEveryTypeStandsOutsideTheAirframeAndPropellerArcs()
        {
            var crew = new List<RampCrewMember>();
            var footprint = new List<LayoutRect>();
            foreach (var type in Types)
            {
                var layout = AircraftLayout.For(type);
                layout.Footprint(footprint, forVehicles: false);
                foreach (var activity in Activities)
                {
                    RampCrew.ForActivity(activity, 0.5, layout, crew);
                    Assert.That(crew, Is.Not.Empty, $"{type.Id} {activity}");
                    foreach (var m in crew)
                    {
                        var where = $"{type.Id} {activity} {m.Task} at ({m.AcrossMetres:0.0}, {m.AlongMetres:0.0})";
                        Assert.That(footprint.Any(r => r.Contains(m.AcrossMetres, m.AlongMetres, 0.5f)), Is.False,
                            where + " is inside the airframe");
                        Assert.That(layout.InPropellerZone(m.AcrossMetres, m.AlongMetres, 0.69f), Is.False,
                            where + " is in a propeller arc");
                        Assert.That(m.AlongMetres, Is.InRange(layout.TailZ - 12f, layout.NoseZ + 15f), where);
                        Assert.That(System.Math.Abs(m.AcrossMetres), Is.LessThan(layout.HalfSpan + 4f), where);
                    }
                }
            }
        }

        [Test]
        public void TheMarshallerStandsAheadOfTheNoseOfEveryType()
        {
            var crew = new List<RampCrewMember>();
            foreach (var type in Types)
            {
                var layout = AircraftLayout.For(type);
                RampCrew.ForActivity(RampActivity.Arrival, 0.5, layout, crew);
                var marshal = crew.Single(m => m.Task == RampTask.MarshalArrival);
                Assert.That(marshal.AlongMetres, Is.GreaterThan(layout.NoseZ + 5f), type.Id);
                Assert.That(System.Math.Abs(marshal.AcrossMetres), Is.LessThan(1f), type.Id);
            }
        }

        [Test]
        public void VehicleStopsAreClearOfNacellesPropellersAndLowWings()
        {
            var footprint = new List<LayoutRect>();
            foreach (var type in Types)
            {
                var layout = AircraftLayout.For(type);
                layout.Footprint(footprint, forVehicles: true);
                var stops = new List<(string Name, (float X, float Z) At)>
                {
                    ("fuel truck", layout.FuelTruck), ("baggage train", layout.BaggageTrain)
                };
                if (layout.CateringTruck is { } hiLoader)
                    stops.Add(("catering truck", hiLoader));
                foreach (var (name, at) in stops)
                {
                    Assert.That(footprint.Any(r => r.Contains(at.X, at.Z, 2.0f)), Is.False,
                        $"{type.Id} {name} at ({at.X:0.0}, {at.Z:0.0}) is against the airframe");
                    Assert.That(layout.InPropellerZone(at.X, at.Z, 2.0f), Is.False, $"{type.Id} {name} is by a propeller");
                }
            }
        }

        [Test]
        public void TurbopropsAreCateredByHandAndJetsByHiLoader()
        {
            foreach (var type in Types)
            {
                var layout = AircraftLayout.For(type);
                Assert.That(layout.CateredByTruck, Is.EqualTo(!layout.IsTurboprop), type.Id);
            }
        }

        [Test]
        public void TheAtrBoardsAftLeftAndLoadsBagsForwardLeft()
        {
            var atr = AircraftLayout.For(AircraftType.Atr42);
            Assert.That(atr.PassengerDoor.X, Is.LessThan(0f));
            Assert.That(atr.PassengerDoor.Z, Is.LessThan(atr.WingBackZ), "behind the wing");
            Assert.That(atr.CargoDoor.X, Is.LessThan(0f));
            Assert.That(atr.CargoDoor.Z, Is.GreaterThan(atr.Engine.PropellerZ), "ahead of the propellers");
        }

        [Test]
        public void JetPassengerDoorsAreTheAerobridgeL1Doors()
        {
            foreach (var type in Types)
            {
                var layout = AircraftLayout.For(type);
                if (layout.IsTurboprop)
                    continue;
                var l1 = AircraftDoors.L1(type);
                Assert.That(layout.PassengerDoor.X, Is.EqualTo(l1.LocalX), type.Id);
                Assert.That(layout.PassengerDoor.Z, Is.EqualTo(l1.LocalZ), type.Id);
            }
        }

        [TestCase(0f)]
        [TestCase(37f)]
        [TestCase(-128f)]
        public void LayoutFrameRoundTripsThroughAnyStandHeading(float headingDegrees)
        {
            var h = headingDegrees * System.MathF.PI / 180f;
            var pose = new GroundPose(120f, -40f, System.MathF.Sin(h), System.MathF.Cos(h), 0f, false);
            var (wx, wz) = AircraftLayout.ToWorld(pose, 3f, -7f);
            var (x, z) = AircraftLayout.ToLocal(pose, wx, wz);
            Assert.That(x, Is.EqualTo(3f).Within(1e-3f));
            Assert.That(z, Is.EqualTo(-7f).Within(1e-3f));
            // +X is the aircraft's right: facing north (+Z), right is east (+X).
            if (headingDegrees == 0f)
                Assert.That(wx, Is.GreaterThan(pose.X));
        }
    }
}
