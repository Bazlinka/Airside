using System.Linq;
using Airside.Domain;
using Airside.Simulation;
using NUnit.Framework;

namespace Airside.Tests
{
    /// <summary>
    /// A real airline to project HUD state from: the player's ATR on a bay at Adelaide, and
    /// helpers to park AI traffic or fly a contract to completion. Shared by the workspace
    /// tests so none of them has to fabricate simulation state (ADR 0057).
    /// </summary>
    internal static class HudTestAirline
    {
        /// <summary>The window sizes the HUD is expected to stay usable at, in virtual points.</summary>
        internal static readonly (float width, float height)[] Viewports =
        {
            (1440f, 900f), (1600f, 900f), (1280f, 800f), (1024f, 640f), (800f, 600f)
        };

        internal static Destination Code(string code)
        {
            Assert.That(DestinationCatalogue.TryFind(code, out var destination), Is.True, code);
            return destination;
        }

        internal static (ManualSimulationClock clock, AirlineOperations ops, FleetAircraft plane) Create(
            string name = "Soak Air")
        {
            var clock = new ManualSimulationClock(new SimulationTime(0));
            var ops = new AirlineOperations(clock, new SeededRandomSource(11), DestinationCatalogue.Adelaide,
                AirlineOperations.AdelaideRegionalBays);
            var player = Airline.Player(name, "#1F3A93");
            ops.AddAirline(player);
            var plane = ops.AddAircraft(player, "VH-PAX", AircraftType.Atr42,
                AirlineOperations.AdelaideRegionalBays[0]);
            return (clock, ops, plane);
        }

        /// <summary>Park an AI aircraft and cancel the departure <c>AddAircraft</c> books for it.</summary>
        internal static void Park(AirlineOperations ops, Airline airline, string registration, StableId stand)
        {
            var aircraft = ops.AddAircraft(airline, registration, AircraftType.Saab340, stand);
            ops.CancelDeparture(aircraft);
        }

        internal static void HoldEveryBay(AirlineOperations ops, Airline airline)
        {
            foreach (var aircraft in ops.FleetOf(airline))
                if (aircraft.State == FleetState.AtStand && aircraft.Scheduled.HasValue)
                    ops.CancelDeparture(aircraft);
        }

        internal static bool OwnsType(AirlineOperations ops, AircraftType type)
        {
            foreach (var owned in ops.PlayerOwnedTypes())
                if (owned.Id == type.Id)
                    return true;
            return false;
        }

        /// <summary>Fly the active contract's route until it fulfils, so nothing is faked.</summary>
        internal static void CompleteActiveContract(ManualSimulationClock clock, AirlineOperations ops,
            RouteContractDefinition definition)
        {
            var destination = Code(definition.DestinationCode);
            var aircraft = ops.FleetOf(ops.PlayerAirline).First(a => a.Type.Id == definition.EligibleType.Id);
            var guard = 0;
            while (ops.CareerState.ActiveContract != null && guard++ < 400)
            {
                if (aircraft.State == FleetState.AtStand && !aircraft.Scheduled.HasValue)
                    ops.ScheduleDeparture(aircraft, destination, ops.ProcessedTo.Advance(
                        DeparturePrep.LeadSeconds(aircraft.Type) + 60));
                clock.Set(clock.Now.Advance(300));
                ops.Update();
            }

            Assert.That(ops.CareerState.HasCompleted(definition.Id), Is.True,
                "the contract should have fulfilled by flying it");
        }
    }
}
