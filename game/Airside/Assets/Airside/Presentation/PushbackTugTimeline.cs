using System;

namespace Airside.Presentation
{
    /// <summary>Where a pushback tug is in its job (ADR 0126).</summary>
    public enum TugPhase
    {
        /// <summary>No tug for this aircraft now.</summary>
        None,
        /// <summary>Reversing onto the nose gear before the push.</summary>
        Approach,
        /// <summary>On the towbar: waiting for the push, or pushing.</summary>
        Coupled,
        /// <summary>Unhooked during the disconnect pause and driving clear to the side.</summary>
        Disconnect,
        /// <summary>Driving away once the aircraft taxis.</summary>
        Return
    }

    public readonly struct TugState
    {
        public TugState(TugPhase phase, float progress)
        {
            Phase = phase;
            Progress = progress;
        }

        public TugPhase Phase { get; }

        /// <summary>0…1 through the phase.</summary>
        public float Progress { get; }

        public bool Visible => Phase != TugPhase.None;
    }

    /// <summary>A tug pose on the ground: position and the unit direction its front faces.</summary>
    public readonly struct TugPose
    {
        public TugPose(float x, float z, float facingX, float facingZ)
        {
            X = x;
            Z = z;
            FacingX = facingX;
            FacingZ = facingZ;
        }

        public float X { get; }
        public float Z { get; }
        public float FacingX { get; }
        public float FacingZ { get; }
    }

    /// <summary>
    /// ADR 0126 — a visible tug for every tail-first pushback, presentation only: it reverses onto the
    /// nose gear before departure, stays on the towbar through the push (and through any hold at the
    /// stand), unhooks during the simulation's 25 s disconnect pause and drives clear of the wing to
    /// the side the aircraft will not turn, then leaves as the aircraft taxis. Nothing here changes
    /// when an aircraft pushes or taxis. Pure — no UnityEngine — so the geometry is tested headlessly.
    /// </summary>
    public static class PushbackTugTimeline
    {
        /// <summary>The tug sets off for the aircraft this long before the booked departure.</summary>
        public const double ApproachLeadSeconds = 150;

        /// <summary>…and is on the towbar by this long before it.</summary>
        public const double CoupleLeadSeconds = 60;

        /// <summary>Unhooking takes this long at the start of the disconnect pause before the tug moves.</summary>
        public const double UnhookSeconds = 7;

        /// <summary>Driving away after the aircraft starts to taxi.</summary>
        public const double ReturnSeconds = 12;

        /// <summary>How far straight ahead of the nose the tug drives before turning away.</summary>
        public const float PullForwardMetres = 12f;

        /// <summary>At the gate the nose is close to the terminal: the approach curve is tight and short.</summary>
        public const float StandPullForwardMetres = 3f;
        public const float StandLateralMetres = 9f;

        /// <summary>Clearance beyond the half-span when parked to the side.</summary>
        public const float WingClearanceMetres = 8f;

        public const float ReturnMetres = 25f;

        /// <summary>At the stand before pushback. <paramref name="secondsToDeparture"/> is negative once the booked time has passed.</summary>
        public static TugState AtStand(double secondsToDeparture)
        {
            if (secondsToDeparture > ApproachLeadSeconds)
                return new TugState(TugPhase.None, 0f);
            if (secondsToDeparture > CoupleLeadSeconds)
                return new TugState(TugPhase.Approach,
                    (float)((ApproachLeadSeconds - secondsToDeparture) / (ApproachLeadSeconds - CoupleLeadSeconds)));
            return new TugState(TugPhase.Coupled, 0f);
        }

        /// <summary>On the taxi-out leg: <paramref name="legSeconds"/> into it, with its push and pause lengths.</summary>
        public static TugState OnTaxiOut(double legSeconds, double pushSeconds, double pauseSeconds)
        {
            if (legSeconds < pushSeconds)
                return new TugState(TugPhase.Coupled, (float)Math.Max(0, legSeconds / Math.Max(1e-6, pushSeconds)));
            var intoPause = legSeconds - pushSeconds;
            if (intoPause < pauseSeconds)
            {
                var unhook = Math.Min(UnhookSeconds, pauseSeconds * 0.4);
                var drive = intoPause <= unhook ? 0.0 : (intoPause - unhook) / Math.Max(1e-6, pauseSeconds - unhook);
                return new TugState(TugPhase.Disconnect, (float)drive);
            }

            var intoReturn = intoPause - pauseSeconds;
            return intoReturn < ReturnSeconds
                ? new TugState(TugPhase.Return, (float)(intoReturn / ReturnSeconds))
                : new TugState(TugPhase.None, 0f);
        }

        /// <summary>
        /// The tug pose. <paramref name="gearX"/>/<paramref name="gearZ"/> is the nose-gear contact point and
        /// <paramref name="noseX"/>/<paramref name="noseZ"/> the unit direction the aircraft's nose points;
        /// <paramref name="reach"/> is how far the tug's root sits ahead of the towbar eye; <paramref name="side"/>
        /// is ±1 for the side it clears to and <paramref name="lateral"/> how far.
        /// </summary>
        public static TugPose Pose(TugState state, float gearX, float gearZ, float noseX, float noseZ, float reach,
            float side, float lateral, float pullForward = PullForwardMetres)
        {
            // Coupled: on the towbar, facing along the nose, the towbar running back to the gear.
            var p0X = gearX + noseX * reach;
            var p0Z = gearZ + noseZ * reach;
            if (state.Phase == TugPhase.Coupled)
                return new TugPose(p0X, p0Z, noseX, noseZ);

            // One curve serves both the approach (reversing in) and the disconnect (driving out):
            // straight ahead of the nose, then round to the clearing side.
            var sideX = -noseZ * side;
            var sideZ = noseX * side;
            var cX = p0X + noseX * pullForward;
            var cZ = p0Z + noseZ * pullForward;
            var p2X = cX + sideX * lateral;
            var p2Z = cZ + sideZ * lateral;

            TugPose OnCurve(float s)
            {
                s = Math.Max(0f, Math.Min(1f, s));
                var u = 1f - s;
                var x = u * u * p0X + 2f * u * s * cX + s * s * p2X;
                var z = u * u * p0Z + 2f * u * s * cZ + s * s * p2Z;
                var dx = 2f * u * (cX - p0X) + 2f * s * (p2X - cX);
                var dz = 2f * u * (cZ - p0Z) + 2f * s * (p2Z - cZ);
                var length = (float)Math.Sqrt(dx * dx + dz * dz);
                return length < 1e-5f ? new TugPose(x, z, noseX, noseZ) : new TugPose(x, z, dx / length, dz / length);
            }

            switch (state.Phase)
            {
                case TugPhase.Approach:
                    // Reversing in: faces along the curve the way it will drive out.
                    return OnCurve(1f - Ease(state.Progress));
                case TugPhase.Disconnect:
                    return OnCurve(Ease(state.Progress));
                case TugPhase.Return:
                {
                    var t = Ease(state.Progress) * ReturnMetres;
                    return new TugPose(p2X + sideX * t, p2Z + sideZ * t, sideX, sideZ);
                }
                default:
                    return new TugPose(p0X, p0Z, noseX, noseZ);
            }
        }

        /// <summary>How far to the side the tug parks: clear of the wing whatever the type.</summary>
        public static float LateralFor(double halfSpanMetres) => (float)Math.Max(14.0, halfSpanMetres + WingClearanceMetres);

        /// <summary>
        /// Clear to the side the aircraft will not turn: <paramref name="aheadX"/>/<paramref name="aheadZ"/> is a
        /// point some way along its taxi path from the push-end pose.
        /// </summary>
        public static float ClearingSide(float fromX, float fromZ, float noseX, float noseZ, float aheadX, float aheadZ)
        {
            var dx = aheadX - fromX;
            var dz = aheadZ - fromZ;
            // Positive when the taxi heads to the nose's left (sideX = -noseZ, sideZ = noseX for side = +1).
            var towardLeft = -noseZ * dx + noseX * dz;
            return towardLeft > 0f ? -1f : 1f;
        }

        private static float Ease(float t)
        {
            t = Math.Max(0f, Math.Min(1f, t));
            return t * t * (3f - 2f * t);
        }
    }
}
