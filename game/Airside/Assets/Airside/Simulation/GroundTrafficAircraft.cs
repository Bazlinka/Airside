using System;
using System.Collections.Generic;
using Airside.Domain;

namespace Airside.Simulation
{
    public enum GroundTrafficRole
    {
        /// <summary>Taxis in, parks on a stand for a dwell, taxis out and departs.</summary>
        ArriveDepart,

        /// <summary>Taxis in, holds in a run-up bay, taxis back out — never uses a stand.</summary>
        Reposition
    }

    /// <summary>
    /// A scheduled aircraft that shares the airfield with the commercial flights and the
    /// rest of the ground-traffic fleet. It reserves every segment and stand it uses
    /// through the shared <see cref="ReservationTable"/>, including a single-file
    /// <see cref="AirportTaxiNetwork.Corridor"/> lock for the whole time it is on the
    /// A1/A2 taxiway, so fleet aircraft queue rather than meet head-on.
    ///
    /// The commercial flights has absolute priority: on every simulated second this
    /// aircraft releases any resource the flight needs, the flight takes its
    /// reservations, and this aircraft then moves into whatever is left. It holds
    /// its current position until it can reserve the next leg, and a hold beyond ten
    /// seconds is explained by the <see cref="TrafficWaitMonitor"/>.
    ///
    /// Motion is a pure function of the simulated seconds it has spent moving, of the
    /// reservation table, and of the commercial flights's stand assignment, so it is
    /// deterministic across frame rates and reconstructed exactly on load.
    /// </summary>
    public sealed class GroundTrafficAircraft
    {
        private readonly struct Leg
        {
            public Leg(string name, StableId[] resources, TaxiPoint from, TaxiPoint to, long seconds, bool parks = false)
            {
                Name = name;
                Resources = resources;
                From = from;
                To = to;
                Seconds = seconds;
                Parks = parks;
            }

            public string Name { get; }
            public StableId[] Resources { get; }
            public TaxiPoint From { get; }
            public TaxiPoint To { get; }
            public long Seconds { get; }
            public bool Parks { get; }
        }

        private static readonly StableId[] None = Array.Empty<StableId>();

        private readonly ReservationTable _reservations;
        private long _warmup;
        private Leg[] _circuit;
        private StableId _targetStand;
        private int _legIndex;
        private long _secondsOnLeg;
        private bool _onLeg;
        private StableId[] _held = None;

        public GroundTrafficAircraft(StableId id, ReservationTable reservations, GroundTrafficRole role, long startDelaySeconds)
        {
            if (startDelaySeconds < 0)
                throw new ArgumentOutOfRangeException(nameof(startDelaySeconds));

            Id = id;
            Role = role;
            _reservations = reservations ?? throw new ArgumentNullException(nameof(reservations));
            _warmup = startDelaySeconds;
            _targetStand = AirportSimulation.StandTwo;
            _circuit = BuildCircuit(role, _targetStand);
        }

        public StableId Id { get; }
        public GroundTrafficRole Role { get; }

        /// <summary>Human-readable phase, e.g. "Taxi in on A1" or "At Stand 1".</summary>
        public string CurrentPhase => _warmup > 0 ? "Waiting for a slot" : _circuit[_legIndex].Name;

        /// <summary>The stand this aircraft is arriving at this circuit (default for a repositioning aircraft).</summary>
        public StableId TargetStand => Role == GroundTrafficRole.ArriveDepart ? _targetStand : default;

        /// <summary>The taxi segment or stand it currently occupies, or <c>default</c> when it holds none.</summary>
        public StableId CurrentSegment => _held.Length > 0 ? _held[_held.Length - 1] : default;

        /// <summary>The segment or stand the current leg is heading for (ignoring the corridor lock).</summary>
        public StableId DesiredSegment => _warmup > 0 || _circuit[_legIndex].Resources.Length == 0
            ? default
            : _circuit[_legIndex].Resources[_circuit[_legIndex].Resources.Length - 1];

        /// <summary>True while it is blocked waiting for a resource another aircraft holds.</summary>
        public bool IsHolding { get; private set; }

        /// <summary>True while it is parked on its stand.</summary>
        public bool IsAtStand => _warmup == 0 && _onLeg && _circuit[_legIndex].Parks;

        /// <summary>True while it holds the shared A1/A2 corridor lock.</summary>
        public bool OnCorridor => Holds(AirportTaxiNetwork.Corridor);

        /// <summary>
        /// True when it is ready to move onto the corridor this tick but has not
        /// entered it yet — used by the fleet to hand the free corridor to whichever
        /// aircraft has waited longest.
        /// </summary>
        public bool WantsCorridorNow =>
            _warmup == 0 && !_onLeg && !OnCorridor && LegNeedsCorridor(_circuit[_legIndex]);

        private bool Holds(StableId resource)
        {
            foreach (var held in _held)
            {
                if (held.Equals(resource))
                    return true;
            }
            return false;
        }

        private static bool LegNeedsCorridor(Leg leg)
        {
            foreach (var resource in leg.Resources)
            {
                if (resource.Equals(AirportTaxiNetwork.Corridor))
                    return true;
            }
            return false;
        }

        /// <summary>0..1 progress along the current leg (preserved across Yield holds).</summary>
        public double Progress
        {
            get
            {
                if (_warmup > 0)
                    return 0d;

                var leg = _circuit[_legIndex];
                if (leg.Seconds <= 0)
                    return 0d;

                // Yield clears _onLeg but keeps _secondsOnLeg — report mid-leg progress so
                // Position does not snap back to the leg start (ADR 0008 cosmetic gap).
                if (!_onLeg && _secondsOnLeg <= 0)
                    return 0d;

                return Math.Max(0d, Math.Min(1d, _secondsOnLeg / (double)leg.Seconds));
            }
        }

        /// <summary>World position along the taxiway for the presentation layer.</summary>
        public TaxiPoint Position
        {
            get
            {
                if (_warmup > 0)
                    return new TaxiPoint(-60f, -30f);

                var leg = _circuit[_legIndex];
                var t = (float)Progress;
                return new TaxiPoint(
                    leg.From.X + (leg.To.X - leg.From.X) * t,
                    leg.From.Z + (leg.To.Z - leg.From.Z) * t);
            }
        }

        /// <summary>
        /// Release any resource the commercial flights needs this tick. Called before the
        /// flight synchronises its own reservations so it never has to wait.
        /// Drops held resources and pauses the leg, but keeps <c>_secondsOnLeg</c> so
        /// progress resumes when the resources are free again.
        /// </summary>
        public void Yield(IEnumerable<StableId> primaryResources)
        {
            if (_held.Length == 0)
                return;

            foreach (var required in primaryResources)
            {
                foreach (var owned in _held)
                {
                    if (required.Equals(owned))
                    {
                        _reservations.Release(Id);
                        _held = None;
                        _onLeg = false;
                        // Keep _secondsOnLeg — Reposition re-acquires without resetting it.
                        IsHolding = true;
                        return;
                    }
                }
            }
        }

        /// <summary>Overload for a single occupied commercial stand on the baseline airfield.</summary>
        public void Reposition(SimulationTime now, TrafficWaitMonitor monitor, StableId primaryStand, bool mayEnterCorridor)
        {
            Reposition(now, monitor, new[] { primaryStand }, AirportCapacity.BaselineStands, mayEnterCorridor);
        }

        /// <summary>Overload for a single occupied commercial stand on an airfield of <paramref name="standCount"/> stands.</summary>
        public void Reposition(SimulationTime now, TrafficWaitMonitor monitor, StableId primaryStand, int standCount, bool mayEnterCorridor)
        {
            Reposition(now, monitor, new[] { primaryStand }, standCount, mayEnterCorridor);
        }

        /// <summary>Overload that assumes the baseline two-stand airfield.</summary>
        public void Reposition(SimulationTime now, TrafficWaitMonitor monitor,
            System.Collections.Generic.IReadOnlyList<StableId> commercialStands, bool mayEnterCorridor)
        {
            Reposition(now, monitor, commercialStands, AirportCapacity.BaselineStands, mayEnterCorridor);
        }

        /// <summary>
        /// Advance one simulated second. Called after the commercial flights have taken
        /// their reservations for this tick. <paramref name="commercialStands"/> is every
        /// stand a commercial currently occupies and <paramref name="standCount"/> is how
        /// many stands the airport has actually built; a fresh arrival parks on the lowest
        /// built stand that is free, and holds off-field when none is.
        /// </summary>
        public void Reposition(SimulationTime now, TrafficWaitMonitor monitor,
            System.Collections.Generic.IReadOnlyList<StableId> commercialStands, int standCount, bool mayEnterCorridor)
        {
            if (_warmup > 0)
            {
                _warmup--;
                return;
            }

            if (_legIndex == 0 && !_onLeg && Role == GroundTrafficRole.ArriveDepart)
            {
                var away = AlternateStand(commercialStands, standCount);
                if (away.Equals(default(StableId)))
                {
                    // Every stand the airport has actually built is taken by a
                    // commercial. Hold off-field rather than taxiing to a stand that
                    // does not exist yet — and leave the corridor free while waiting.
                    IsHolding = true;
                    monitor.SetWaiting(Id, _targetStand, now);
                    return;
                }

                if (!away.Equals(_targetStand))
                {
                    _targetStand = away;
                    _circuit = BuildCircuit(Role, away);
                }
            }

            var leg = _circuit[_legIndex];

            if (!_onLeg)
            {
                if (LegNeedsCorridor(leg) && !OnCorridor && !mayEnterCorridor)
                {
                    // Not this aircraft's turn for the single-file corridor.
                    IsHolding = true;
                    monitor.SetWaiting(Id, AirportTaxiNetwork.Corridor, now);
                    return;
                }

                if (leg.Resources.Length == 0)
                {
                    _reservations.Release(Id);
                    _held = None;
                }
                else if (_reservations.TryReplace(Id, leg.Resources, out var blocked))
                {
                    _held = leg.Resources;
                }
                else
                {
                    IsHolding = true;
                    monitor.SetWaiting(Id, blocked, now);
                    return;
                }

                _onLeg = true;
                // Do not zero _secondsOnLeg here — Yield pauses mid-leg and we resume.
                // Fresh legs reset seconds when the previous leg completes below.
                IsHolding = false;
                monitor.Clear(Id);
            }

            _secondsOnLeg++;
            if (_secondsOnLeg >= leg.Seconds)
            {
                _legIndex = (_legIndex + 1) % _circuit.Length;
                _onLeg = false;
                _secondsOnLeg = 0;
            }
        }


        /// <summary>
        /// The lowest-numbered stand the airport has built that no commercial flight
        /// occupies, or <c>default</c> when every built stand is taken. Stands beyond
        /// <paramref name="standCount"/> are not on the airfield yet and are never chosen.
        /// </summary>
        private static StableId AlternateStand(
            System.Collections.Generic.IReadOnlyList<StableId> commercialStands, int standCount)
        {
            var built = new[] { AirportSimulation.StandOne, AirportSimulation.StandTwo, AirportSimulation.StandThree };
            var usable = Math.Max(0, Math.Min(built.Length, standCount));
            for (var index = 0; index < usable; index++)
            {
                var candidate = built[index];
                var taken = false;
                if (commercialStands != null)
                {
                    for (var i = 0; i < commercialStands.Count; i++)
                    {
                        if (commercialStands[i].Equals(candidate))
                        {
                            taken = true;
                            break;
                        }
                    }
                }

                if (!taken)
                    return candidate;
            }

            return default;
        }

        private static Leg[] BuildCircuit(GroundTrafficRole role, StableId stand)
        {
            var corridorA1 = new[] { AirportTaxiNetwork.Corridor, AirportTaxiNetwork.AlphaOne };
            var corridorA2 = new[] { AirportTaxiNetwork.Corridor, AirportTaxiNetwork.AlphaTwo };
            var runwayEnd = new TaxiPoint(-24f, 0f);
            var junction = new TaxiPoint(-12f, 9f);
            var alphaEnd = new TaxiPoint(8f, 9f);
            var away = new TaxiPoint(-60f, -30f);
            var offField = new TaxiPoint(-36f, -4f);

            if (role == GroundTrafficRole.Reposition)
            {
                var bay = new TaxiPoint(5f, 9f);
                return new[]
                {
                    new Leg("Taxi in on A1", corridorA1, runwayEnd, junction, 14),
                    new Leg("Taxi to run-up bay", corridorA2, junction, bay, 12),
                    new Leg("Run-up hold", corridorA2, bay, bay, 18),
                    new Leg("Taxi out on A2", corridorA2, bay, junction, 12),
                    new Leg("Taxi out on A1", corridorA1, junction, runwayEnd, 14),
                    new Leg("Departing", None, runwayEnd, offField, 8),
                    new Leg("Away", None, away, away, 45)
                };
            }

            var leadIn = AirportTaxiNetwork.LeadInFor(stand);
            var standPoint = new TaxiPoint(17f, AirportTaxiNetwork.StandZ(stand));
            var label = stand.Equals(AirportSimulation.StandOne) ? "Stand 1"
                : stand.Equals(AirportSimulation.StandTwo) ? "Stand 2"
                : "Stand 3";

            return new[]
            {
                new Leg("Taxi in on A1", corridorA1, runwayEnd, junction, 14),
                new Leg("Taxi in on A2", corridorA2, junction, alphaEnd, 14),
                new Leg($"Taxi to {label}", new[] { leadIn, stand }, alphaEnd, standPoint, 10),
                new Leg($"At {label}", new[] { stand }, standPoint, standPoint, 40, parks: true),
                // First outbound leg must still hold the stand lead-in until clear of the bay.
                new Leg("Taxi out on A2", new[] { leadIn, AirportTaxiNetwork.Corridor, AirportTaxiNetwork.AlphaTwo }, standPoint, junction, 16),
                new Leg("Taxi out on A1", corridorA1, junction, runwayEnd, 14),
                new Leg("Departing", None, runwayEnd, offField, 8),
                new Leg("Away", None, away, away, 30)
            };
        }
    }
}
