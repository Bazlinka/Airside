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
            Assert.That(AdelaideHourProfile.Density(5), Is.GreaterThanOrEqualTo(0.45f),
                "05:00 first wave of night-stopped aircraft (ADR 0110)");
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

        [Test]
        public void SnapToBankLocal_CeilingsOntoFiveMinuteMarksDuringPeaks()
        {
            var almost = new DateTime(2026, 9, 19, 7, 2, 0);
            var snapped = AdelaideHourProfile.SnapToBankLocal(almost, 6, 22);
            Assert.That(snapped, Is.EqualTo(new DateTime(2026, 9, 19, 7, 5, 0)));
            var onMark = new DateTime(2026, 9, 19, 7, 0, 0);
            Assert.That(AdelaideHourProfile.SnapToBankLocal(onMark, 6, 22), Is.EqualTo(onMark));
            var quiet = new DateTime(2026, 9, 19, 14, 12, 0);
            Assert.That(AdelaideHourProfile.SnapToBankLocal(quiet, 6, 22).Hour, Is.EqualTo(16),
                "quiet hours still jump to the next bank");
        }
    }
}
