using System.Collections.Generic;
using System.Linq;
using Airside.Domain;
using Airside.Simulation;
using NUnit.Framework;

namespace Airside.Tests
{
    public sealed class FlightCrewWorkTests
    {
        [Test]
        public void InspectionPathsStayOutsideEveryFixedWingFootprintAndFinishBeforeFuelReady()
        {
            var footprint = new List<LayoutRect>();
            foreach (var spec in AircraftCatalogue.All.Where(s => !s.Type.IsRotorcraft))
            {
                var type = spec.Type; var layout = AircraftLayout.For(type);
                layout.Footprint(footprint, false);
                var duration = FlightCrewWork.InspectionSeconds(type);
                for (double t=0;t<duration;t+=.5)
                {
                    var a = FlightCrewWork.Inspection(type,t);
                    Assert.That(footprint.Any(r => r.Contains(a.X,a.Z,.25f)), Is.False, type.Id + " at " + t);
                }
                foreach (PlayerBaseLevel level in new[] { PlayerBaseLevel.Starter, PlayerBaseLevel.International })
                    Assert.That(DeparturePrep.StageSecondsFor(type,DeparturePrepStage.Fuel,level), Is.GreaterThanOrEqualTo(duration));
            }
        }
        [Test]
        public void InspectorPositionDoesNotDependOnPreviouslyRenderedFrames()
        {
            var a=FlightCrewWork.Inspection(AircraftType.Saab340,30);
            FlightCrewWork.Inspection(AircraftType.Saab340,200);
            var b=FlightCrewWork.Inspection(AircraftType.Saab340,30);
            Assert.That((a.X,a.Z,a.Walking), Is.EqualTo((b.X,b.Z,b.Walking)));
        }
    }
}
