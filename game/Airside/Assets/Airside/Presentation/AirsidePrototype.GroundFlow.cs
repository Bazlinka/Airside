using System;
using System.Collections.Generic;
using Airside.Simulation;
using UnityEngine;

namespace Airside.Presentation
{
    /// <summary>
    /// ADR 0144 — ground movement that never jumps. A taxiing aircraft used to be drawn at the
    /// simulation's leg time. While it waited behind traffic it was drawn stopped, but that time kept
    /// running, so when the queue ahead moved it appeared hundreds of metres down the taxiway. A queue
    /// hop over 150 m snapped outright, and an aircraft cleared from the back of the holding queue
    /// jumped to the runway because its lineup started at the holding point.
    ///
    /// Now each aircraft follows one continuous chain of legs: stand → holding point → runway on the
    /// way out, runway exit → wait → stand on the way in. The drawn aircraft keeps its own place on that
    /// chain. It follows the simulation exactly while in step. When the target jumps ahead it speeds up
    /// from where it is (up to 1.6× the planned pace) and catches up. It never moves backwards, and it
    /// eases to a stop when the target falls behind it.
    /// </summary>
    public sealed partial class AirsidePrototype
    {
        /// <summary>How much faster than plan an aircraft may go to catch up.</summary>
        private const float GroundFlowMaxRate = 1.6f;

        /// <summary>How quickly the pace can rise (per second), from standing to plan speed in ~4 s.</summary>
        private const float GroundFlowRateUp = 0.25f;

        /// <summary>How quickly the pace can fall (per second).</summary>
        private const float GroundFlowRateDown = 0.9f;

        private sealed class GroundFlowState
        {
            public string Chain;
            public double Tau;
            public float Rate;
            public double LastTime;
        }

        private readonly Dictionary<string, GroundFlowState> _groundFlow = new();

        /// <summary>The legs one aircraft drives in a row, so progress carries from one into the next.</summary>
        private readonly struct GroundChain
        {
            public GroundChain(string key, GroundLeg first, GroundLeg second)
            {
                Key = key;
                First = first;
                Second = second;
            }

            public string Key { get; }
            public GroundLeg First { get; }
            public GroundLeg Second { get; }
            public double Seconds => First.Seconds + (Second?.Seconds ?? 0.0);

            public GroundPose PoseAt(double tau)
            {
                if (Second == null || tau <= First.Seconds)
                    return First.PoseAt(tau);
                return Second.PoseAt(tau - First.Seconds);
            }
        }

        /// <summary>
        /// Where the simulation has the aircraft on its chain right now, in chain seconds. Null for a leg
        /// that is not part of a chain (parked).
        /// </summary>
        private bool TryGroundChainTarget(FleetAircraft aircraft, FleetVisual visual, out GroundChain chain,
            out double target)
        {
            chain = default;
            target = 0;
            var elapsed = _preciseTime - visual.LegStartedAt.ElapsedSeconds;
            switch (visual.Leg)
            {
                case FleetGroundLeg.TaxiOut:
                case FleetGroundLeg.HoldingShort:
                case FleetGroundLeg.Lineup:
                {
                    var taxi = AdelaideGround.TaxiOut(aircraft.DepartureStand, aircraft.Type, aircraft.AssignedRunway);
                    var lineup = AdelaideGround.LineupFor(aircraft.AssignedRunway, aircraft.Type);
                    chain = new GroundChain($"out:{aircraft.DepartureStand.Value}:{aircraft.AssignedRunway}", taxi, lineup);
                    if (visual.Leg == FleetGroundLeg.TaxiOut)
                    {
                        var scale = visual.LegSeconds > 0 ? taxi.Seconds / visual.LegSeconds : 1.0;
                        target = taxi.BrakedSeconds(elapsed * scale, GroundTraffic.QueuedSeconds(taxi,
                            FleetVisual.QueueAhead(_operations.Fleet, aircraft, _clock.Now)), out _);
                    }
                    else if (visual.Leg == FleetGroundLeg.HoldingShort)
                    {
                        target = GroundTraffic.QueuedSeconds(taxi,
                            FleetVisual.QueueSlot(_operations.Fleet, aircraft, _clock.Now));
                    }
                    else
                    {
                        var scale = visual.LegSeconds > 0 ? lineup.Seconds / visual.LegSeconds : 1.0;
                        target = taxi.Seconds + elapsed * scale;
                    }

                    return true;
                }
                case FleetGroundLeg.Vacate:
                case FleetGroundLeg.AwaitingStand:
                case FleetGroundLeg.TaxiIn:
                {
                    var vacate = AdelaideGround.VacateFor(aircraft.Type, aircraft.AssignedRunway);
                    var taxiIn = AdelaideGround.TaxiIn(aircraft.Stand, aircraft.Type, aircraft.AssignedRunway);
                    // Until a stand is chosen the chain is the vacate alone: the stand is not known yet.
                    var hasStand = !string.IsNullOrEmpty(aircraft.Stand.Value);
                    chain = new GroundChain($"in:{aircraft.AssignedRunway}", vacate, hasStand ? taxiIn : null);
                    if (visual.Leg == FleetGroundLeg.Vacate)
                    {
                        var scale = visual.LegSeconds > 0 ? vacate.Seconds / visual.LegSeconds : 1.0;
                        target = vacate.BrakedSeconds(elapsed * scale, GroundTraffic.QueuedSeconds(vacate,
                            FleetVisual.ExitQueueAhead(_operations.Fleet, aircraft, _clock.Now)), out _);
                    }
                    else if (visual.Leg == FleetGroundLeg.AwaitingStand)
                    {
                        target = GroundTraffic.QueuedSeconds(vacate,
                            FleetVisual.QueueSlot(_operations.Fleet, aircraft, _clock.Now));
                    }
                    else
                    {
                        var scale = visual.LegSeconds > 0 ? taxiIn.Seconds / visual.LegSeconds : 1.0;
                        target = vacate.Seconds + elapsed * scale;
                    }

                    return true;
                }
                default:
                    return false;
            }
        }

        /// <summary>
        /// The drawn pose on the chain: in step with the simulation when it can be, otherwise catching
        /// up or easing to a stop, never jumping. Null when the leg is not part of a chain.
        /// </summary>
        private GroundPose? GroundFlowPose(FleetAircraft aircraft, FleetVisual visual, float lookAheadSeconds)
        {
            if (!TryGroundChainTarget(aircraft, visual, out var chain, out var target))
            {
                _groundFlow.Remove(aircraft.Registration);
                return null;
            }

            target = Math.Max(0.0, Math.Min(target, chain.Seconds));
            if (!_groundFlow.TryGetValue(aircraft.Registration, out var state)
                || !ChainContinues(state.Chain, chain.Key))
            {
                state = new GroundFlowState { Chain = chain.Key, Tau = target, Rate = 1f, LastTime = _preciseTime };
                _groundFlow[aircraft.Registration] = state;
            }

            // The arrival chain gains its taxi-in once a stand is assigned: same key prefix, keep place.
            state.Chain = chain.Key;

            if (lookAheadSeconds == 0f)
                Advance(state, target);
            var tau = Math.Min(chain.Seconds, state.Tau + lookAheadSeconds * Math.Max(0.2f, state.Rate));
            var pose = chain.PoseAt(tau);
            var speed = pose.Speed * Mathf.Clamp(state.Rate, 0f, GroundFlowMaxRate);
            if (state.Rate < 0.02f && Math.Abs(target - state.Tau) < 0.05)
                speed = Mathf.Min(speed, pose.Speed);
            return new GroundPose(pose.X, pose.Z, pose.NoseX, pose.NoseZ, speed, pose.TailFirst);
        }

        private static bool ChainContinues(string was, string now) =>
            was == now || (was != null && now != null && was.StartsWith("in:", StringComparison.Ordinal)
                           && now.StartsWith("in:", StringComparison.Ordinal));

        private void Advance(GroundFlowState state, double target)
        {
            var dt = (float)Math.Max(0.0, Math.Min(0.5, _preciseTime - state.LastTime));
            state.LastTime = _preciseTime;
            if (dt <= 0f)
                return;
            var gap = target - state.Tau;
            // In step: the target moved about as far as the plan allows this frame. Follow it exactly.
            if (gap >= 0.0 && gap <= dt * (state.Rate + GroundFlowRateUp * dt) * 1.02 + 1e-4)
            {
                state.Rate = Mathf.Clamp(Mathf.MoveTowards(state.Rate, (float)(gap / dt), GroundFlowRateDown * dt), 0f,
                    GroundFlowMaxRate);
                state.Tau = target;
                return;
            }

            if (gap > 0.0)
            {
                // Behind: pick up pace toward a catch-up rate, never overshooting the target.
                var want = Mathf.Min(GroundFlowMaxRate, 1f + (float)gap / 10f);
                state.Rate = Mathf.MoveTowards(state.Rate, want, GroundFlowRateUp * dt);
                state.Tau = Math.Min(target, state.Tau + state.Rate * dt);
                return;
            }

            // Ahead of the target (it stepped back, e.g. a queue grew): ease to a stop in place.
            state.Rate = Mathf.MoveTowards(state.Rate, 0f, GroundFlowRateDown * dt);
            state.Tau += state.Rate * dt * 0.5f;
        }
    }
}
