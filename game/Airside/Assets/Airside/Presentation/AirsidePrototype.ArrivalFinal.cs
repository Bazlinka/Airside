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
    /// it reaches the hold point as it is cleared. Speed flexes 55–160% to absorb estimate
    /// changes, and an early clearance blends onto the landing path over a few seconds.
    /// </summary>
    public sealed partial class AirsidePrototype
    {
        /// <summary>Beyond this an inbound is not drawn yet (it is still well out on the approach).</summary>
        private const float ArrivalFinalShowMetres = 18_000f;

        /// <summary>3,000 ft: the extended final levels off here rather than climbing forever.</summary>
        private const float ArrivalFinalCapMetres = 915f;

        private const float ArrivalMinSpeedFactor = 0.55f;
        private const float ArrivalMaxSpeedFactor = 1.6f;
        private const float ArrivalHandoffSeconds = 3f;

        private sealed class ArrivalFinalState
        {
            public float Metres;
            public int Frame = -1;
            public double LastTime;
            public bool Active;
            public bool HandoffPending;
            public float HandoffAt = -1f;
            public Vector3 HandoffOffset;
            public Vector3 World;
            public RunwayDirection Runway;
            public AircraftType Type;
        }

        private readonly Dictionary<string, ArrivalFinalState> _arrivalFinal = new();

        /// <summary>Updates (once a frame) and reports whether this aircraft is on the extended final.</summary>
        private bool TryArrivalFinal(FleetAircraft aircraft, out ArrivalFinalState state)
        {
            state = null;
            if (!FleetMode || aircraft == null)
                return false;
            _arrivalFinal.TryGetValue(aircraft.Registration, out state);
            if (state != null && state.Frame == Time.frameCount)
                return state.Active;

            var eta = _operations.ExpectedLandingClearance(aircraft, out var runway);
            if (!eta.HasValue)
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
            var target = speed * (float)System.Math.Max(0.0, eta.Value.ElapsedSeconds - _preciseTime);
            if (state == null)
            {
                if (target > ArrivalFinalShowMetres)
                    return false;
                state = new ArrivalFinalState { Metres = target, LastTime = _preciseTime };
                _arrivalFinal[aircraft.Registration] = state;
            }

            var step = (float)System.Math.Max(0.0, _preciseTime - state.LastTime);
            state.LastTime = _preciseTime;
            if (state.Active)
            {
                var slowest = state.Metres - speed * ArrivalMinSpeedFactor * step;
                var fastest = state.Metres - speed * ArrivalMaxSpeedFactor * step;
                state.Metres = Mathf.Max(0f, Mathf.Clamp(target, fastest, slowest));
            }
            else
            {
                state.Metres = Mathf.Min(target, ArrivalFinalShowMetres);
            }

            state.Frame = Time.frameCount;
            state.Active = true;
            state.Runway = runway;
            state.Type = aircraft.Type;
            state.World = ArrivalFinalWorld(state, 0f);
            return true;
        }

        /// <summary>World position on the extended final, <paramref name="lookAheadSeconds"/> further in.</summary>
        private static Vector3 ArrivalFinalWorld(ArrivalFinalState state, float lookAheadSeconds)
        {
            var hold = AirsideFlightPath.Approach((float)ApproachHold.HoldingFinalProgress(0), 0f, state.Type);
            var speed = CircuitProfile.Knots(AircraftPerformance.For(state.Type).ApproachKnots);
            var metres = Mathf.Max(0f, state.Metres - speed * lookAheadSeconds);
            var x = hold.x - metres;
            var y = AirsideFlightPath.GroundY + Mathf.Min(CircuitProfile.GlideslopeHeight(x), ArrivalFinalCapMetres);
            // A holder parked on the 80 % pin (~130 ft) looked frozen while the tower waited
            // for the previous landing to vacate. A small S-turn keeps them flying until cleared.
            var weave = metres < 40f ? Mathf.Sin((float)(state.LastTime + lookAheadSeconds) * 0.45f) * 16f : 0f;
            RunwayFrame.ToWorld(state.Runway, x, y, hold.z + weave, out var wx, out var wy, out var wz);
            return new Vector3(wx, wy, wz);
        }

        /// <summary>Extended-final position for an inbound or holding arrival; null otherwise.</summary>
        private Vector3? FleetArrivalFinalPosition(CommercialFlight flight, float lookAheadSeconds)
        {
            if (!FleetMode || !_fleetAircraftById.TryGetValue(flight.AircraftId, out var aircraft)
                || !TryArrivalFinal(aircraft, out var state))
                return null;
            return lookAheadSeconds == 0f ? state.World : ArrivalFinalWorld(state, lookAheadSeconds);
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

            var remaining = 1f - Mathf.SmoothStep(0f, 1f, (Time.time - state.HandoffAt) / ArrivalHandoffSeconds);
            if (remaining <= 0f)
            {
                _arrivalFinal.Remove(flight.AircraftId);
                return Vector3.zero;
            }

            return state.HandoffOffset * remaining;
        }
    }
}
