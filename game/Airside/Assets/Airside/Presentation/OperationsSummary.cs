using System.Collections.Generic;
using Airside.Domain;
using Airside.Simulation;

namespace Airside.Presentation
{
    /// <summary>
    /// The one-line "what's my airline doing, what needs me" objective (ADR 0053's persistent
    /// status/objective layer), shown once the first-flight guide is done. Reads fleet and
    /// career state only — it decides nothing and stores nothing.
    /// </summary>
    public static class OperationsSummary
    {
        /// <summary>
        /// The most urgent aircraft's situation; failing that, the active contract's progress;
        /// failing that, a quiet fleet-wide line. <paramref name="playerFleet"/> in any order —
        /// the worst severity wins.
        /// </summary>
        public static (string Text, StatusSeverity Severity) Line(
            IReadOnlyList<FleetAircraft> playerFleet, SimulationTime now, AirlineCareerState career = null)
        {
            if (playerFleet == null || playerFleet.Count == 0)
                return ("No aircraft yet.", StatusSeverity.Normal);

            FleetAircraft worst = null;
            var worstSeverity = StatusSeverity.Normal;
            foreach (var aircraft in playerFleet)
            {
                var severity = AircraftStatus.Severity(aircraft, now);
                if (severity > worstSeverity)
                {
                    worstSeverity = severity;
                    worst = aircraft;
                }
            }

            if (worst != null)
                return ($"{worst.Registration} needs you — {ActionHint(worst)}", worstSeverity);

            if (career?.ActiveContract != null
                && RouteContractCatalogue.TryFind(career.ActiveContract.DefinitionId, out var definition))
            {
                var remaining = definition.RequiredRotations - career.ActiveContract.CompletedRotations;
                return ($"{definition.Id}: {career.ActiveContract.CompletedRotations} of " +
                    $"{definition.RequiredRotations} rotations — {remaining} to go.", StatusSeverity.Normal);
            }

            var flying = 0;
            foreach (var aircraft in playerFleet)
                if (aircraft.State != FleetState.AtStand)
                    flying++;

            var text = flying > 0
                ? $"{flying} of {playerFleet.Count} aircraft flying — on schedule."
                : "All aircraft on stand — ready for a flight.";
            return (text, StatusSeverity.Normal);
        }

        private static string ActionHint(FleetAircraft aircraft) => aircraft.State switch
        {
            FleetState.AwaitingStand => "choose a stand.",
            FleetState.AtStand when !aircraft.Scheduled.HasValue => "plan a flight.",
            _ => "check its status."
        };
    }
}
