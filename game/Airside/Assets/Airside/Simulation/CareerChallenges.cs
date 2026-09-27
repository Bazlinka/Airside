using System;
using System.Collections.Generic;
using System.Linq;
using Airside.Domain;

namespace Airside.Simulation
{
    /// <summary>What the challenges are judged on, gathered once per check.</summary>
    public readonly struct ChallengeFacts
    {
        public ChallengeFacts(int onTimeStreak, bool profitableDay, int charters, int medicalFlights, bool finaleReached,
            int longHaulServed, int fleetCount, int reliability, int rotations)
        {
            OnTimeStreak = onTimeStreak;
            ProfitableDay = profitableDay;
            Charters = charters;
            MedicalFlights = medicalFlights;
            FinaleReached = finaleReached;
            LongHaulServed = longHaulServed;
            FleetCount = fleetCount;
            Reliability = reliability;
            Rotations = rotations;
        }

        public int OnTimeStreak { get; }
        public bool ProfitableDay { get; }
        public int Charters { get; }
        public int MedicalFlights { get; }
        public bool FinaleReached { get; }
        public int LongHaulServed { get; }
        public int FleetCount { get; }
        public int Reliability { get; }
        public int Rotations { get; }
    }

    /// <summary>One optional challenge: a cash reward for playing well, beside the career goals.</summary>
    public sealed class CareerChallenge
    {
        internal CareerChallenge(string id, string title, long reward, bool prestige, Func<ChallengeFacts, (int, int)> progress)
        {
            Id = id;
            Title = title;
            Reward = reward;
            Prestige = prestige;
            Progress = progress;
        }

        public string Id { get; }
        public string Title { get; }
        public long Reward { get; }

        /// <summary>Only open once the established-airline finale is reached — the sandbox's goals.</summary>
        public bool Prestige { get; }

        /// <summary>(progress, target) for these facts.</summary>
        public Func<ChallengeFacts, (int Progress, int Target)> Progress { get; }

        public string Key => CareerChallenges.KeyPrefix + Id;
    }

    public readonly struct CareerChallengeStatus
    {
        public CareerChallengeStatus(CareerChallenge challenge, int progress, int target, bool complete, bool open)
        {
            Challenge = challenge;
            Progress = progress;
            Target = target;
            Complete = complete;
            Open = open;
        }

        public CareerChallenge Challenge { get; }
        public int Progress { get; }
        public int Target { get; }
        public bool Complete { get; }

        /// <summary>False for a prestige challenge before the finale.</summary>
        public bool Open { get; }
    }

    /// <summary>
    /// ADR 0127 — optional challenges. They give punctuality and the new contract kinds a purpose
    /// (the career goals only ever counted), pay cash once, and after the finale become the
    /// sandbox's prestige goals. Completion is recorded as an award key in the career state, which is
    /// already saved, so it is paid exactly once across reloads.
    /// </summary>
    public static class CareerChallenges
    {
        public const string KeyPrefix = "challenge:";

        public static readonly string[] LongHaulCities = { "DPS", "SIN", "HKG", "KUL", "NAN", "DOH", "DXB" };

        public static readonly IReadOnlyList<CareerChallenge> All = new[]
        {
            new CareerChallenge("on-time-10", "Push back on time 10 times in a row", 1_500, false,
                f => (Math.Min(f.OnTimeStreak, 10), 10)),
            new CareerChallenge("profitable-day", "Make a profit on a day of 4 or more flights", 1_000, false,
                f => (f.ProfitableDay ? 1 : 0, 1)),
            new CareerChallenge("medical-2", "Fly 2 medical calls", 2_000, false,
                f => (Math.Min(f.MedicalFlights, 2), 2)),
            new CareerChallenge("charter-3", "Fly 3 charters", 2_500, false,
                f => (Math.Min(f.Charters, 3), 3)),
            new CareerChallenge("on-time-30", "Push back on time 30 times in a row", 5_000, false,
                f => (Math.Min(f.OnTimeStreak, 30), 30)),
            new CareerChallenge("long-haul-all", "Serve every long-haul city", 25_000, true,
                f => (f.LongHaulServed, LongHaulCities.Length)),
            new CareerChallenge("fleet-25", "Fly 25 aircraft", 20_000, true,
                f => (Math.Min(f.FleetCount, AircraftAcquisition.MaxPlayerAircraft), AircraftAcquisition.MaxPlayerAircraft)),
            new CareerChallenge("reliability-95", "Keep 95% reliability to 300 flights", 15_000, true,
                f => (f.Reliability >= 95 ? Math.Min(f.Rotations, 300) : 0, 300))
        };

        public static ChallengeFacts FactsFor(AirlineCareerState career, int fleetCount, bool profitableDay)
        {
            var charters = 0;
            var medical = 0;
            foreach (var id in career.CompletedContractIds)
            {
                if (id.EndsWith("-CH", StringComparison.Ordinal))
                    charters++;
                else if (id.EndsWith("-MED", StringComparison.Ordinal))
                    medical++;
            }

            var longHaul = LongHaulCities.Count(code => career.ServedDestinations.Contains(code));
            return new ChallengeFacts(career.OnTimeStreak, profitableDay, charters, medical, career.FinaleReached, longHaul,
                fleetCount, career.Reliability, career.CompletedPlayerRotations);
        }

        public static IReadOnlyList<CareerChallengeStatus> Status(AirlineCareerState career, ChallengeFacts facts)
        {
            var list = new List<CareerChallengeStatus>(All.Count);
            foreach (var challenge in All)
            {
                var (progress, target) = challenge.Progress(facts);
                var done = career.HasAward(challenge.Key);
                list.Add(new CareerChallengeStatus(challenge, done ? target : progress, target, done,
                    !challenge.Prestige || facts.FinaleReached));
            }

            return list;
        }
    }
}
