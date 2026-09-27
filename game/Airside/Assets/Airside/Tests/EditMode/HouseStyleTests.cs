using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Airside.Domain;
using Airside.Presentation;
using Airside.Simulation;
using NUnit.Framework;

namespace Airside.Tests
{
    /// <summary>
    /// ADR 0129 — the game speaks in one plain voice (docs/product/WRITING.md). This gathers the text a
    /// player actually reads and fails on the habits the wording pass removed: em dashes inside
    /// sentences, semicolons, and the words the player never sees (rotation, service, dispatch,
    /// delegation).
    /// </summary>
    public sealed class HouseStyleTests
    {
        private static readonly Regex OldWords =
            new(@"\b(rotations?|services?|dispatch\w*|delegat\w*)\b", RegexOptions.IgnoreCase);

        private static void AssertHouseStyle(IEnumerable<(string Where, string Text)> texts)
        {
            var problems = new List<string>();
            foreach (var (where, text) in texts)
            {
                if (string.IsNullOrEmpty(text) || text == "—")
                    continue;
                if (text.Contains(" — ") || text.Contains("— ") || text.Contains(" —"))
                    problems.Add($"{where}: em dash in \"{text}\"");
                if (text.Contains("; "))
                    problems.Add($"{where}: semicolon in \"{text}\"");
                var old = OldWords.Match(text);
                if (old.Success)
                    problems.Add($"{where}: \"{old.Value}\" in \"{text}\"");
            }

            Assert.That(problems, Is.Empty, string.Join("\n", problems));
        }

        [Test]
        public void TheManualGoalsAndNewsReadInTheHouseStyle()
        {
            var texts = new List<(string, string)>();
            foreach (var page in FlightManual.Pages)
            {
                texts.Add(($"manual {page.Id} lead", page.Lead));
                foreach (var (heading, body) in page.Sections)
                {
                    texts.Add(($"manual {page.Id} heading", heading));
                    // The controls page lists key bindings; its bodies are "key    action" rows.
                    texts.Add(($"manual {page.Id} '{heading}'", body));
                }
            }

            var career = new AirlineCareerState();
            foreach (var goal in CareerRoadmap.Evaluate(career, new[] { AircraftType.Saab340 }))
                texts.Add(($"goal {goal.Id}", goal.Title));
            foreach (var milestone in CareerMilestones.Reached(career, 1, new[] { AircraftType.Saab340 }))
                texts.Add(("milestone", milestone.Title));
            foreach (var challenge in CareerChallenges.All)
                texts.Add(($"challenge {challenge.Id}", challenge.Title));
            foreach (var card in new[]
                     {
                         CelebrationCard.ForTier(OperatingTier.Regional, "Soak Air"),
                         CelebrationCard.ForTier(OperatingTier.Domestic, "Soak Air"),
                         CelebrationCard.ForTier(OperatingTier.International, "Soak Air"),
                         CelebrationCard.ForContract("Kingscote charter", 4_200, 4, null),
                         CelebrationCard.ForFinale("Soak Air", 18, 12, 140)
                     })
            {
                texts.Add(("celebration title", card.Title));
                texts.Add(("celebration subtitle", card.Subtitle));
                foreach (var line in card.Lines)
                    texts.Add(("celebration line", line));
            }

            for (var day = 0L; day < 400; day++)
                if (DemandEvents.OnDay(day) is { } news)
                    texts.Add(("demand event", news.Headline));

            AssertHouseStyle(texts);
        }

        [Test]
        public void HoldLinesAndPayMessagesReadInTheHouseStyle()
        {
            var (clock, ops, plane) = HudTestAirline.Create();
            var blocker = ops.Fleet.FirstOrDefault(a => !a.Airline.IsPlayer) ?? plane;
            var texts = new List<(string, string)>();
            foreach (HoldKind kind in Enum.GetValues(typeof(HoldKind)))
            {
                var reason = new HoldReason(kind, blocker, RunwayDirection.Runway23, clock.Now.Advance(90), 2,
                    "fuel 40%", new[] { blocker });
                texts.Add(($"hold {kind}", HoldReasonText.Long(plane, reason, clock.Now)));
                texts.Add(($"hold tag {kind}", HoldReasonText.Short(reason)));
                var anonymous = new HoldReason(kind, null, null, null, 0, "12/30");
                texts.Add(($"hold {kind} (no blocker)", HoldReasonText.Long(plane, anonymous, clock.Now)));
            }

            var id = new SettlementId(plane.Registration, 1);
            foreach (DelayCause cause in Enum.GetValues(typeof(DelayCause)))
            {
                var delay = DelayBreakdown.Parse(600, $"{cause}:600");
                texts.Add(($"pay toast {cause}", DelayText.SettlementToast(new FlightSettlement(id, "C-1", 900, 0, 2, false, delay), 0)));
                texts.Add(($"pay toast {cause} done", DelayText.SettlementToast(new FlightSettlement(id, "C-1", 900, 0, 3, true, delay), 0)));
            }
            texts.Add(("pay toast on time", DelayText.SettlementToast(
                new FlightSettlement(id, null, 900, 1, 1, false, DelayBreakdown.Parse(0, null)), 5)));

            AssertHouseStyle(texts);
        }

        [Test]
        public void ContractsAndRefusalsReadInTheHouseStyle()
        {
            var (clock, ops, plane) = HudTestAirline.Create();
            var texts = new List<(string, string)>();

            var contracts = new ContractsWorkspaceModel();
            contracts.Rebuild(ops, clock.Now);
            foreach (var offer in contracts.Offers)
            {
                texts.Add(("offer title", offer.Title));
                texts.Add(("offer terms", offer.Terms));
            }
            texts.Add(("contracts empty", contracts.EmptyOffersLine));

            Assert.That(DestinationCatalogue.TryFind("PER", out var perth), Is.True);
            texts.Add(("refuse range", ops.ScheduleDeparture(plane, perth, clock.Now.Advance(3600)).Reason));
            texts.Add(("refuse buy", ops.BuyAircraft(AircraftType.Boeing7378).Reason));
            Assert.That(ops.AcceptContract(RouteContractCatalogue.RegionalKingscoteIntro).Accepted, Is.True);
            texts.Add(("refuse second contract", ops.AcceptContract(RouteContractCatalogue.RegionalKingscoteIntro).Reason));
            contracts.Rebuild(ops, clock.Now);
            texts.Add(("active title", contracts.ActiveTitle));
            texts.Add(("active progress", contracts.ActiveProgressText));
            foreach (var term in contracts.ActiveTerms)
                texts.Add(("active term", term));

            AssertHouseStyle(texts);
        }
    }
}
