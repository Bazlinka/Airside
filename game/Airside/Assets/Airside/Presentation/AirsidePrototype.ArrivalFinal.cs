using System.Collections.Generic;
using Airside.Domain;
using Airside.Simulation;
using UnityEngine;

namespace Airside.Presentation
{
    /// <summary>
    /// Arrivals waiting for the runway fly in down an extended final instead of hanging still.
    /// The simulation clears a landing from a fixed point on short final; an aircraft that got
    /// there while a departure was still rolling (or the wake gap after a jet was running) was
    /// drawn frozen in mid-air at that point for up to three minutes, then carried on. Every
    /// arrival also appeared out of nowhere at that point, 750 m out. Now the tower's expected
    /// clearance time places the arrival that far back along the final at its approach speed, so
    /// it reaches the hold point as it is cleared. Speed eases within 55–160% to absorb estimate
    /// changes, and an early clearance blends onto the landing path over a few seconds.
    /// </summary>
    public sealed partial class AirsidePrototype
    {
        /// <summary>Beyond this an inbound is not drawn yet (ADR 0142: 32 km, was 18).</summary>
        private const float ArrivalFinalShowMetres = ArrivalApproach.ShowMetres;

        private const float ArrivalHandoffSeconds = 6f;

        private sealed class ArrivalFinalState
        {
            public float Metres;
            /// <summary>Ground speed along the final, eased toward the estimate rather than snapped to it.</summary>
            public float Speed;
            public int Frame = -1;
            public double LastTime;
            public bool Active;
            public bool HandoffPending;
            public float HandoffAt = -1f;
            public Vector3 HandoffOffset;
            public Vector3 World;
            /// <summary>Where the drawn pose is against the flown path: it may lag a jump, never leap.</summary>
            public Vector3 Offset;
            public bool HasShown;
            public RunwayDirection Runway;
            public AircraftType Type;
            /// <summary>Which side the arrival joins the final from (ADR 0142), fixed when first drawn.</summary>
            public float Lateral;
        }

        private readonly Dictionary<string, ArrivalFinalState> _arrivalFinal = new();

        /// <summary>
        /// ADR 0173: the tower's landing estimate for one arrival, kept between frames. The queue
        /// part is cheap; the ground check (up to 144 whole-fleet path tests, about 3 ms a call and
        /// over 100 ms at worst) runs a few steps a frame. It was recomputed several times a frame
        /// for every inbound aircraft, which is what slowed the game whenever arrivals were due.
        /// </summary>
        private sealed class LandingEtaSearch
        {
            public FleetState State;
            public long StateStarted;
            public long RefreshedAt = long.MinValue;
            public bool Searching;
            public SimulationTime Cursor;
            public int Steps;
            public bool HasEta;
            public SimulationTime Eta;
            public RunwayDirection Runway;
        }

        private const long LandingEtaRefreshSeconds = 5;
        private const int LandingEtaStepsPerFrame = 4;

        private readonly Dictionary<string, LandingEtaSearch> _landingEta = new();
        private int _landingEtaBudgetFrame = -1;
        private int _landingEtaBudget;

        /// <summary>The landing estimate for an arrival that could be on the drawn final; false otherwise.</summary>
        private bool TryLandingEta(FleetAircraft aircraft, out SimulationTime eta, out RunwayDirection runway)
        {
            eta = default;
            runway = default;
            var registration = aircraft.Registration;
            if (aircraft.State is not (FleetState.Inbound or FleetState.HoldingForLanding))
            {
                _landingEta.Remove(registration);
                return false;
            }

            // It cannot be cleared before it joins the queue, so an arrival that far out is not on
            // the drawn final yet: most inbound aircraft, and no estimate needed.
            if (aircraft.State == FleetState.Inbound && aircraft.StateEndsAt.HasValue)
            {
                var speed = CircuitProfile.Knots(AircraftPerformance.For(aircraft.Type).ApproachKnots);
                if (speed * (aircraft.StateEndsAt.Value.ElapsedSeconds - _preciseTime) > ArrivalFinalShowMetres)
                {
                    _landingEta.Remove(registration);
                    return false;
                }
            }

            if (!_landingEta.TryGetValue(registration, out var search))
                _landingEta[registration] = search = new LandingEtaSearch();
            var started = aircraft.StateStartedAt.ElapsedSeconds;
            if (search.State != aircraft.State || search.StateStarted != started)
            {
                // Joined the queue: estimate again at once, but keep flying on the last one meanwhile.
                search.State = aircraft.State;
                search.StateStarted = started;
                search.RefreshedAt = long.MinValue;
                search.Searching = false;
            }

            var now = _clock.Now.ElapsedSeconds;
            if (!search.Searching && now - search.RefreshedAt >= LandingEtaRefreshSeconds)
            {
                search.RefreshedAt = now;
                var queued = _operations.ExpectedLandingQueueTime(aircraft, out var queuedRunway);
                if (queued.HasValue)
                {
                    search.Runway = queuedRunway;
                    search.Cursor = queued.Value;
                    search.Steps = 0;
                    search.Searching = true;
                }
                else
                {
                    search.HasEta = false;
                }
            }

            if (search.Searching)
            {
                if (_landingEtaBudgetFrame != Time.frameCount)
                {
                    _landingEtaBudgetFrame = Time.frameCount;
                    _landingEtaBudget = LandingEtaStepsPerFrame;
                }

                while (search.Searching && _landingEtaBudget > 0)
                {
                    _landingEtaBudget--;
                    if (search.Steps >= AirlineOperations.LandingGroundCheckSteps
                        || _operations.LandingGroundClear(aircraft, search.Cursor))
                    {
                        search.Eta = search.Cursor;
                        search.HasEta = true;
                        search.Searching = false;
                        break;
                    }

                    search.Cursor = GroundTraffic.NextGrid(search.Cursor);
                    search.Steps++;
                }
            }

            if (!search.HasEta)
                return false;
            eta = search.Eta;
            runway = search.Runway;
            return true;
        }

        /// <summary>Updates (once a frame) and reports whether this aircraft is on the extended final.</summary>
        private bool TryArrivalFinal(FleetAircraft aircraft, out ArrivalFinalState state)
        {
            state = null;
            if (!FleetMode || aircraft == null)
                return false;
            _arrivalFinal.TryGetValue(aircraft.Registration, out state);
            if (state != null && state.Frame == Time.frameCount)
                return state.Active;

            var hasEta = TryLandingEta(aircraft, out var eta, out var runway);
            if (!hasEta)
            {
                if (state == null)
                    return false;
                state.Frame = Time.frameCount;
                if (state.Active)
                {
                    // Cleared (or sent round): hand over to the landing path smoothly.
                    state.Active = false;
                    state.HandoffPending = state.Metres > 1f;
                }

                if (!state.HandoffPending && state.HandoffAt < 0f)
                    _arrivalFinal.Remove(aircraft.Registration);
                return false;
            }

            var speed = CircuitProfile.Knots(AircraftPerformance.For(aircraft.Type).ApproachKnots);
            var target = speed * (float)System.Math.Max(0.0, eta.ElapsedSeconds - _preciseTime);
            if (state == null)
            {
                if (target > ArrivalFinalShowMetres)
                    return false;
                state = new ArrivalFinalState
                {
                    Metres = target, LastTime = _preciseTime, Speed = speed,
                    Lateral = ArrivalApproach.LateralFactor(aircraft, runway)
                };
                _arrivalFinal[aircraft.Registration] = state;
            }

            var step = (float)System.Math.Max(0.0, _preciseTime - state.LastTime);
            state.LastTime = _preciseTime;
            if (state.Active)
                state.Metres = ArrivalApproach.FlyFinal(state.Metres, ref state.Speed, target, speed, step);
            else
            {
                state.Metres = Mathf.Min(target, ArrivalFinalShowMetres);
                state.Speed = speed;
            }

            state.Frame = Time.frameCount;
            state.Active = true;
            state.Runway = runway;
            state.Type = aircraft.Type;
            // ADR 0179: whatever moves the path (a runway change, a new estimate, a fallback), the
            // drawn aircraft covers at most a few times its approach speed, so it never leaps.
            var flown = ArrivalFinalWorld(state, 0f);
            var shown = state.HasShown
                ? Vector3.MoveTowards(state.World, flown,
                    speed * ArrivalApproach.PoseSlewFactor * step + 1f)
                : flown;
            state.HasShown = true;
            state.Offset = shown - flown;
            state.World = shown;
            return true;
        }

        /// <summary>World position on the extended final, <paramref name="lookAheadSeconds"/> further in.</summary>
        private static Vector3 ArrivalFinalWorld(ArrivalFinalState state, float lookAheadSeconds)
        {
            var hold = AirsideFlightPath.Approach((float)ApproachHold.HoldingFinalProgress(0), 0f, state.Type);
            var metres = Mathf.Max(0f, state.Metres - state.Speed * lookAheadSeconds);
            var x = hold.x - metres;
            var y = AirsideFlightPath.GroundY + ArrivalApproach.Height(CircuitProfile.GlideslopeHeight(x));
            // A holder parked on the 80 % pin (~130 ft) looked frozen while the tower waited
            // for the previous landing to vacate. A small S-turn keeps them flying until cleared.
            // ADR 0147: a gentle drift rather than a 16 m S-turn at 130 ft.
            var weave = metres < 40f ? Mathf.Sin((float)(state.LastTime + lookAheadSeconds) * 0.22f) * 5f : 0f;
            // ADR 0142: beyond 12 km the arrival curves in from its origin's side of the final.
            // The route map uses the same function, so the two agree on where it is.
            ArrivalMapTrack.FinalWorldXZ(state.Runway, state.Type, metres, state.Lateral, weave, out var wx, out var wz);
            return new Vector3(wx, y, wz);
        }

        /// <summary>Extended-final position for an inbound or holding arrival; null otherwise.</summary>
        private Vector3? FleetArrivalFinalPosition(CommercialFlight flight, float lookAheadSeconds)
        {
            if (!FleetMode || !_fleetAircraftById.TryGetValue(flight.AircraftId, out var aircraft)
                || !TryArrivalFinal(aircraft, out var state))
                return null;
            return lookAheadSeconds == 0f ? state.World : ArrivalFinalWorld(state, lookAheadSeconds) + state.Offset;
        }

        /// <summary>An inbound close enough to be on the drawn extended final.</summary>
        private bool IsArrivingOnFinal(FleetAircraft aircraft) =>
            aircraft != null && aircraft.State == FleetState.Inbound && TryArrivalFinal(aircraft, out _);

        /// <summary>
        /// Offset that eases a just-cleared arrival from where it was drawn onto the landing
        /// path, when the tower cleared it earlier than expected.
        /// </summary>
        private Vector3 ArrivalHandoffOffset(CommercialFlight flight, Vector3 position)
        {
            if (!_arrivalFinal.TryGetValue(flight.AircraftId, out var state) || state.Active)
                return Vector3.zero;
            if (state.HandoffPending)
            {
                state.HandoffPending = false;
                var offset = state.World - position;
                if (offset.magnitude > 1f && offset.magnitude < 4_000f)
                {
                    state.HandoffOffset = offset;
                    state.HandoffAt = Time.time;
                }
            }

            if (state.HandoffAt < 0f)
            {
                _arrivalFinal.Remove(flight.AircraftId);
                return Vector3.zero;
            }

            // Smootherstep: no kink in speed at either end of the handoff (ADR 0147).
            var u = Mathf.Clamp01((Time.time - state.HandoffAt) / ArrivalHandoffSeconds);
            var remaining = 1f - u * u * u * (u * (u * 6f - 15f) + 10f);
            if (remaining <= 0f)
            {
                _arrivalFinal.Remove(flight.AircraftId);
                return Vector3.zero;
            }

            return state.HandoffOffset * remaining;
        }
    }
}
