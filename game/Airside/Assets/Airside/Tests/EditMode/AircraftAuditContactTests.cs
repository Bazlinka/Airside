using System.Collections.Generic;
using Airside.Domain;
using Airside.Presentation;
using Airside.Simulation;
using NUnit.Framework;

namespace Airside.Tests
{
    public sealed class AircraftAuditContactTests
    {
        [Test]
        public void EveryFixedWingContactsOnlyAtItsOwnProfileTouchdown()
        {
            foreach (var spec in AircraftCatalogue.All)
            {
                var type = spec.Type;
                var contact = AircraftPerformance.For(type).TouchdownProgress;
                Assert.That(AircraftTouchdownContact.HasContact(type, contact - 0.00001f), Is.False, spec.Id);
                Assert.That(AircraftTouchdownContact.HasContact(type, contact), Is.True, spec.Id);
                Assert.That(AircraftTouchdownContact.HasContact(type, contact + 0.00001f), Is.True, spec.Id);
            }
        }

        [Test]
        public void WidebodyIsStillAirborneAfterAtrContact()
        {
            var t = (AircraftPerformance.For(AircraftType.Atr42).TouchdownProgress
                + AircraftPerformance.For(AircraftType.Boeing78710).TouchdownProgress) / 2f;
            Assert.That(AircraftTouchdownContact.HasContact(AircraftType.Atr42, t), Is.True);
            Assert.That(AircraftTouchdownContact.HasContact(AircraftType.Boeing78710, t), Is.False);
        }

        [Test]
        public void ContactRecordsOncePerLandingAndWaitsForVisibleView()
        {
            var fired = new HashSet<string>();
            var type = AircraftType.Boeing78710;
            var t = AircraftPerformance.For(type).TouchdownProgress;
            Assert.That(AircraftTouchdownContact.TryRecord(fired, "flight", AircraftPhase.Landing, type, t, false), Is.False);
            Assert.That(AircraftTouchdownContact.TryRecord(fired, "flight", AircraftPhase.Landing, type, t - 0.01f, true), Is.False);
            Assert.That(AircraftTouchdownContact.TryRecord(fired, "flight", AircraftPhase.Landing, type, t, true), Is.True);
            Assert.That(AircraftTouchdownContact.TryRecord(fired, "flight", AircraftPhase.Landing, type, 1f, true), Is.False);
            Assert.That(AircraftTouchdownContact.TryRecord(fired, "other", AircraftPhase.Landing, type, t, true), Is.True);
            Assert.That(AircraftTouchdownContact.TryRecord(fired, "flight", AircraftPhase.TaxiIn, type, 1f, true), Is.False);
            Assert.That(AircraftTouchdownContact.TryRecord(fired, "flight", AircraftPhase.Landing, type, t, true), Is.True);
        }

        [Test]
        public void DemoFallsBackToAtrAndRotorcraftNeverProduceTyreContact()
        {
            var t = AircraftPerformance.For(AircraftType.Atr42).TouchdownProgress;
            Assert.That(AircraftTouchdownContact.HasContact(null, t - 0.00001f), Is.False);
            Assert.That(AircraftTouchdownContact.HasContact(null, t), Is.True);
            var fired = new HashSet<string>();
            Assert.That(AircraftTouchdownContact.TryRecord(fired, "heli", AircraftPhase.Landing, AircraftType.Bell412, 1f, true), Is.False);
            Assert.That(fired, Is.Empty);
        }
    }
}
