using System;

namespace Airside.Presentation
{
    /// <summary>How a main gear leg folds into its bay.</summary>
    public enum GearRetractStyle
    {
        /// <summary>Tip swings forward (nose gears, ATR 42 and Saab 340 mains).</summary>
        Forward,
        /// <summary>Tip swings aft into the nacelle (Dash 8 mains).</summary>
        Aft,
        /// <summary>Leg swings inboard about the fuselage axis into the belly (every jet's mains).</summary>
        Inboard
    }

    /// <summary>A gear door either rides on its leg or hangs from the belly and swings open.</summary>
    public enum GearDoorKind
    {
        /// <summary>Thin vertical plate beside the leg: carried by the leg, never moves on its own.</summary>
        LegMounted,
        /// <summary>Horizontal panel in the belly: hinged at its outer edge, opens as the gear passes.</summary>
        Belly
    }

    /// <summary>
    /// Pure rules for the aircraft physical-animation pass: control-surface laws, hinge geometry,
    /// gear kinematics and wheel spin. No UnityEngine types, so the headless harness checks the
    /// same signs the editor applies.
    ///
    /// Sign conventions are stated in aircraft terms and converted to a hinge-axis angle in exactly
    /// one place (<see cref="HingeAngleForTrailingEdgeDown"/>). The airframe faces +Z, is right-handed
    /// across +X, and the project pitches the nose up with a NEGATIVE Euler X and banks a wing down with
    /// a POSITIVE Euler Z on the left (so a positive bank is a LEFT bank). A hinge line is oriented with
    /// +X (or +Y for the rudder), so a positive rotation about it lifts the trailing edge (rudder: swings
    /// it left). Trailing-edge-down is therefore a negative hinge angle.
    /// </summary>
    public static class AircraftArticulation
    {
        /// <summary>One presentation timeline per aircraft, retained when its part cache is rebuilt.</summary>
        public sealed class GearCycle
        {
            public float Retract { get; private set; }
            private bool _seeded;

            public float Step(float target, float deltaSeconds, float extendSeconds)
            {
                target = Clamp(target, 0f, 1f);
                if (!_seeded) { Retract = target; _seeded = true; }
                else if (deltaSeconds > 0f)
                {
                    var rate = target < Retract ? 1f / Math.Max(1f, extendSeconds) : 1f / 7f;
                    Retract = MoveToward(Retract, target, deltaSeconds * rate);
                }
                return Retract;
            }
        }

        public const float MaxElevatorUpDegrees = 25f;
        public const float MaxElevatorDownDegrees = 16f;
        public const float MaxAileronUpDegrees = 22f;
        public const float MaxAileronDownDegrees = 16f;
        public const float MaxRudderDegrees = 20f;
        public const float MaxRollSpoilerDegrees = 25f;

        // ---- Hinge geometry --------------------------------------------------------------------

        /// <summary>Hinge-axis rotation (degrees) that deflects a flap/aileron/elevator trailing edge down.</summary>
        public static float HingeAngleForTrailingEdgeDown(float trailingEdgeDownDegrees) => -trailingEdgeDownDegrees;

        /// <summary>Hinge-axis rotation (degrees) that swings a rudder's trailing edge to the right.</summary>
        public static float HingeAngleForRudderRight(float trailingEdgeRightDegrees) => -trailingEdgeRightDegrees;

        /// <summary>
        /// Fits a straight line through the front (hinge) edge of a control surface. The surfaces on a swept
        /// wing or fin are hinged along a slanted line, and turning them about the plain lateral/vertical axis
        /// tears the outboard end away from the wing. <paramref name="span"/> is the coordinate along the
        /// hinge (X for a wing or tailplane, Y for a rudder) and <paramref name="front"/> is the forward (Z)
        /// coordinate. The leading-edge sample of each of a few span bins is the largest front value in it.
        /// </summary>
        /// <returns>
        /// False when fewer than two bins hold vertices; the line then is level at the furthest-forward vertex.
        /// <paramref name="frontAtCentre"/> is the hinge line's forward position at the mid span.
        /// </returns>
        public static bool FitHingeLine(float[] span, float[] front, int count,
            out float slope, out float spanCentre, out float frontAtCentre)
        {
            slope = 0f;
            spanCentre = 0f;
            frontAtCentre = 0f;
            if (span == null || front == null || count <= 0)
                return false;

            var lo = float.MaxValue;
            var hi = float.MinValue;
            var forwardMost = float.MinValue;
            for (var i = 0; i < count; i++)
            {
                lo = Math.Min(lo, span[i]);
                hi = Math.Max(hi, span[i]);
                forwardMost = Math.Max(forwardMost, front[i]);
            }

            spanCentre = (lo + hi) * 0.5f;
            frontAtCentre = forwardMost;
            var length = hi - lo;
            if (length < 0.2f)
                return false;

            const int bins = 8;
            var edge = new float[bins];
            var edgeSpan = new float[bins];
            var seen = new bool[bins];
            for (var i = 0; i < count; i++)
            {
                var bin = Math.Min(bins - 1, (int)((span[i] - lo) / length * bins));
                if (!seen[bin] || front[i] > edge[bin])
                {
                    edge[bin] = front[i];
                    edgeSpan[bin] = span[i];
                    seen[bin] = true;
                }
            }

            float n = 0f, sx = 0f, sy = 0f, sxx = 0f, sxy = 0f;
            for (var b = 0; b < bins; b++)
            {
                if (!seen[b])
                    continue;
                // Regress against where the front-most vertex of the bin actually is, not the bin's centre,
                // or a swept edge reads a bin-width too far forward.
                var x = edgeSpan[b] - spanCentre;
                n++;
                sx += x;
                sy += edge[b];
                sxx += x * x;
                sxy += x * edge[b];
            }

            var denominator = n * sxx - sx * sx;
            if (n < 2f || Math.Abs(denominator) < 1e-6f)
                return false;

            slope = (n * sxy - sx * sy) / denominator;
            frontAtCentre = (sy - slope * sx) / n;
            // A hinge slanted past ~50 degrees is the fit chasing a notch or a chamfer, not the sweep.
            if (Math.Abs(slope) > 1.2f)
            {
                slope = 0f;
                frontAtCentre = forwardMost;
                return false;
            }

            return true;
        }

        // ---- Control laws ----------------------------------------------------------------------

        /// <summary>
        /// Elevator trailing-edge-up degrees. It holds roughly in proportion to the nose-up attitude (trim),
        /// and kicks further the faster the nose is being rotated up, so it is up on rotation and the flare
        /// and eases back once the attitude settles.
        /// </summary>
        public static float ElevatorTrailingEdgeUpDegrees(float pitchUpDegrees, float pitchUpRateDegreesPerSecond)
        {
            var command = 0.55f * pitchUpDegrees + 2.4f * pitchUpRateDegreesPerSecond;
            return Clamp(command, -MaxElevatorDownDegrees, MaxElevatorUpDegrees);
        }

        /// <summary>
        /// Aileron trailing-edge-down degrees for one wing. Ailerons command roll RATE: they deflect to roll
        /// into a turn, neutralise on the steady bank and reverse to roll out. Rolling left lifts the left
        /// aileron and drops the right one.
        /// </summary>
        public static float AileronTrailingEdgeDownDegrees(float rollLeftRateDegreesPerSecond, bool rightWing)
        {
            var left = Clamp(rollLeftRateDegreesPerSecond * 1.6f, -MaxAileronUpDegrees, MaxAileronUpDegrees);
            var down = rightWing ? left : -left;
            return Clamp(down, -MaxAileronUpDegrees, MaxAileronDownDegrees);
        }

        /// <summary>
        /// Rudder trailing-edge-right degrees: a pulse of rudder with the roll (to cancel adverse yaw) and a
        /// token amount held on the bank, because a coordinated turn needs almost none.
        /// </summary>
        public static float RudderTrailingEdgeRightDegrees(float rollLeftRateDegreesPerSecond, float bankLeftDegrees)
        {
            var command = -0.5f * rollLeftRateDegreesPerSecond - 0.07f * bankLeftDegrees;
            return Clamp(command, -MaxRudderDegrees, MaxRudderDegrees);
        }

        /// <summary>Roll spoiler lift on one wing: only the wing going down raises its spoiler.</summary>
        public static float RollSpoilerDegrees(float rollLeftRateDegreesPerSecond, bool rightWing)
        {
            var wingDownRate = rightWing ? -rollLeftRateDegreesPerSecond : rollLeftRateDegreesPerSecond;
            const float deadband = 1.5f;
            if (wingDownRate <= deadband)
                return 0f;
            return Clamp((wingDownRate - deadband) * 1.1f, 0f, MaxRollSpoilerDegrees);
        }

        /// <summary>
        /// Droop of a surface with the hydraulics off: ailerons and elevators relax trailing-edge down on a
        /// parked, shut-down airframe, as they do on a real apron.
        /// </summary>
        public static float ParkedDroopDegrees(bool hydraulicsOff) => hydraulicsOff ? 7f : 0f;

        /// <summary>How far a Fowler/slotted flap track carries the flap aft, in metres.</summary>
        public static float FlapAftTravelMetres(float chordMetres, float trailingEdgeDownDegrees) =>
            Math.Max(0f, chordMetres) * 0.0055f * Math.Max(0f, trailingEdgeDownDegrees);

        // ---- Landing gear ----------------------------------------------------------------------

        /// <summary>
        /// Mains fold: jets inboard into the belly, and so does an ATR whose legs hang from a short fuselage
        /// sponson (folded forward or aft its wheels would stand proud of the fairing); a Dash 8 swings them aft
        /// into its nacelle, a Saab forward into its nacelle.
        /// </summary>
        public static GearRetractStyle MainGearStyle(bool turboprop, bool hasInnerNacelleDoors,
            bool legsOnFuselageSponson = false)
        {
            if (!turboprop || legsOnFuselageSponson)
                return GearRetractStyle.Inboard;
            return hasInnerNacelleDoors ? GearRetractStyle.Aft : GearRetractStyle.Forward;
        }

        /// <summary>How far the leg has travelled (0..1) for gear retraction (0 down and locked, 1 up and locked).</summary>
        public static float GearLegSwing01(float retract01)
        {
            var t = Clamp(retract01, 0f, 1f);
            return SmoothStep((t - 0.18f) / 0.64f);
        }

        /// <summary>Door opening for the same travel: open before the leg moves, shut after it is stowed.</summary>
        public static float GearDoorOpen01(float retract01)
        {
            var t = Clamp(retract01, 0f, 1f);
            if (t <= 0f || t >= 1f)
                return 0f;
            return SmoothStep(t / 0.12f) * (1f - SmoothStep((t - 0.88f) / 0.12f));
        }

        /// <summary>
        /// The rotation a gear leg takes at <paramref name="swing01"/> about its top pivot, as an axis (in the
        /// leg's parent frame) and an angle. Forward/aft legs pitch about the lateral axis; inboard legs roll
        /// about the longitudinal axis toward the centreline.
        /// </summary>
        public static (float AxisX, float AxisZ, float Degrees) GearRetractRotation(
            GearRetractStyle style, bool leftSide, float swing01)
        {
            var s = Clamp(swing01, 0f, 1f);
            switch (style)
            {
                case GearRetractStyle.Aft:
                    return (1f, 0f, 90f * s);
                case GearRetractStyle.Inboard:
                    return (0f, 1f, (leftSide ? 100f : -100f) * s);
                default:
                    // The simplified straight leg folds above horizontal to enclose its tyre envelope in the bay.
                    return (1f, 0f, -110f * s);
            }
        }

        /// <summary>
        /// Truck-beam pitch for a multi-axle main gear (787, A330, A350): the beam tips forward end up during
        /// the swing so the six wheels fit the bay. Negative X pitch raises the forward axle.
        /// </summary>
        public static float TruckTiltDegrees(float swing01) =>
            -30f * SmoothStep((Clamp(swing01, 0f, 1f) - 0.15f) / 0.55f);

        /// <summary>Belly door opening angle about its longitudinal hinge, positive swings the free edge down.</summary>
        public static float BellyDoorDegrees(float open01) => 72f * Clamp(open01, 0f, 1f);

        /// <summary>
        /// A thin vertical plate (a few centimetres wide, tall) is a leg-mounted door; a thin horizontal panel is a
        /// belly door. Sizes are the part's extents in metres.
        /// </summary>
        public static GearDoorKind ClassifyGearDoor(float sizeX, float sizeY, float sizeZ) =>
            sizeX < 0.2f && sizeY >= sizeX * 3f ? GearDoorKind.LegMounted : GearDoorKind.Belly;

        // ---- Wheels ----------------------------------------------------------------------------

        /// <summary>
        /// Wheel surface speed (m/s, signed) after <paramref name="deltaSeconds"/>. The tyres spin up hard when the
        /// mains touch down, and spin down gently once the aircraft is off the ground and the brakes stop them on
        /// retraction, rather than snapping between zero and ground speed.
        /// </summary>
        public static float WheelSpinStep(float current, float target, float deltaSeconds)
        {
            if (deltaSeconds <= 0f)
                return current;
            const float spinUp = 180f;
            const float spinDown = 28f;
            var limit = (Math.Abs(target) > Math.Abs(current) ? spinUp : spinDown) * deltaSeconds;
            return MoveToward(current, target, limit);
        }

        public static float MoveToward(float current, float target, float maxDelta)
        {
            var difference = target - current;
            if (Math.Abs(difference) <= maxDelta)
                return target;
            return current + Math.Sign(difference) * maxDelta;
        }

        private static float SmoothStep(float x)
        {
            var t = Clamp(x, 0f, 1f);
            return t * t * (3f - 2f * t);
        }

        private static float Clamp(float value, float min, float max) =>
            value < min ? min : value > max ? max : value;
    }
}
