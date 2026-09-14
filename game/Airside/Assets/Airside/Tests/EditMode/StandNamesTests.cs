using System.Linq;
using Airside.Domain;
using Airside.Presentation;
using Airside.Simulation;
using NUnit.Framework;

namespace Airside.Tests
{
    public sealed class StandNamesTests
    {
        [Test]
        public void Bays_ReadAsTheirRealAdelaideNumbers()
        {
            Assert.That(StandNames.Short(new StableId("BAY-1")), Is.EqualTo("50D"));
            Assert.That(StandNames.Display(new StableId("BAY-4")), Is.EqualTo("Bay 50A"));
            Assert.That(StandNames.Short(new StableId("GATE-9")), Is.EqualTo("GATE-9"));
            Assert.That(StandNames.Display(default), Is.EqualTo("—"));
        }

        [Test]
        public void Quickest_PicksTheShortestTaxiInAmongFreeStands()
        {
            var stands = AirlineOperations.AdelaideRegionalBays.ToList();
            var quickest = StandNames.QuickestToTaxiIn(stands);
            Assert.That(quickest.HasValue, Is.True);
            foreach (var stand in stands)
                Assert.That(AirlineOperations.TaxiInSecondsTo(quickest.Value),
                    Is.LessThanOrEqualTo(AirlineOperations.TaxiInSecondsTo(stand)));
            Assert.That(StandNames.QuickestToTaxiIn(new StableId[0]).HasValue, Is.False);
        }
    }
}
