using System;
using Airside.Simulation;
using NUnit.Framework;

namespace Airside.Tests
{
    public sealed class AdelaideHourProfileTests
    {
        [Test]
        public void Density_PeaksInTheMorningAndEveningBanks()
        {
            Assert.That(AdelaideHourProfile.Density(5), Is.LessThan(0.1f),
                "curfew until 06:00");
            Assert.That(AdelaideHourProfile.Density(7), Is.EqualTo(1f));
            Assert.That(AdelaideHourProfile.Density(17), Is.EqualTo(1f));
            Assert.That(AdelaideHourProfile.Density(14), Is.LessThan(0.5f));
            Assert.That(AdelaideHourProfile.Density(22), Is.LessThan(0.45f),
                "regionals skip the late-international hole");
            Assert.That(AdelaideHourProfile.Density(23), Is.LessThan(0.1f), "curfew from 23:00");
            Assert.That(AdelaideHourProfile.Density(3), Is.LessThan(0.1f));
        }

        [Test]
        public void BankMinutes_ClusterOnTheBusyHours()
        {
            var banks = AdelaideHourProfile.BankMinutes(20, 6, 21);
            Assert.That(banks.Length, Is.EqualTo(20));
            var peak = 0;
            var quiet = 0;
            foreach (var minute in banks)
            {
                var hour = minute / 60;
                if (hour is >= 6 and <= 8 or >= 16 and <= 18)
                    peak++;
                if (hour is >= 13 and <= 15)
                    quiet++;
            }

            Assert.That(peak, Is.GreaterThan(quiet * 2), "the published day bunches on the banks");
        }

        [Test]
        public void NextUsefulLocal_SkipsTheAfternoonHole()
        {
            var afternoon = new DateTime(2026, 9, 19, 14, 20, 0);
            var next = AdelaideHourProfile.NextUsefulLocal(afternoon, 6, 21);
            Assert.That(next.Hour, Is.EqualTo(16));
            var morning = new DateTime(2026, 9, 19, 7, 40, 0);
            Assert.That(AdelaideHourProfile.NextUsefulLocal(morning, 6, 21), Is.EqualTo(morning));
        }
    }
}
