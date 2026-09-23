using System;
using System.Collections.Generic;

namespace Airside.Simulation
{
    /// <summary>What a ramp worker is doing, which decides where they stand.</summary>
    public enum RampRole
    {
        /// <summary>Beside the vehicle currently working the aircraft.</summary>
        Attending,

        /// <summary>At the aircraft door or hold the vehicle is serving.</summary>
        Receiving,

        /// <summary>Marshalling clear of the wingtip while a vehicle manoeuvres.</summary>
        Marshalling
    }

    /// <summary>One hi-vis worker to draw, in stand-local metres.</summary>
    public readonly struct RampCrewMember
    {
        public RampCrewMember(RampRole role, float alongMetres, float acrossMetres, float facingDegrees)
        {
            Role = role;
            AlongMetres = alongMetres;
            AcrossMetres = acrossMetres;
            FacingDegrees = facingDegrees;
        }

        public RampRole Role { get; }

        /// <summary>Metres ahead of the stand stop along the aircraft nose direction; negative is aft.</summary>
        public float AlongMetres { get; }

        /// <summary>Metres to the aircraft's right of the centreline; negative is left.</summary>
        public float AcrossMetres { get; }

        /// <summary>Heading in degrees clockwise from the aircraft nose.</summary>
        public float FacingDegrees { get; }
    }

    /// <summary>
    /// Places the hi-vis ramp workers around a turnaround.
    ///
    /// ADR 0114 imported two ramp characters (<c>chr_ramp_m_worker</c>,
    /// <c>chr_ramp_f_worker</c>) and never placed them, so the only people ever visible were
    /// boarding passengers — and those are drawn for stairs boarding only. On an aerobridge
    /// gate, which is where the player's jets park, the apron had nobody on it at all.
    ///
    /// Crew appear for whichever vehicle is actually working, so the apron is populated
    /// through fuelling, catering and baggage rather than only during boarding.
    ///
    /// Pure function of the prep stage; no UnityEngine types, so the headless harness checks it.
    /// </summary>
    public static class RampCrew
    {
        /// <summary>The most workers drawn for one aircraft. Two characters are available.</summary>
        public const int MaxPerAircraft = 2;

        /// <summary>
        /// Crew for the stage <paramref name="prep"/> is in, appended to <paramref name="into"/>.
        /// Idle and Ready produce none: the aircraft is not being worked.
        /// </summary>
        public static void For(DeparturePrepStatus prep, GroundServiceKind? activeVehicle,
            List<RampCrewMember> into)
        {
            if (into == null)
                return;
            into.Clear();

            switch (prep.Stage)
            {
                case DeparturePrepStage.Fuel:
                    // One at the truck's panel, one under the wing at the fuelling point.
                    into.Add(new RampCrewMember(RampRole.Attending, -11.0f, 6.2f, 250f));
                    into.Add(new RampCrewMember(RampRole.Receiving, -8.5f, 3.6f, 90f));
                    break;

                case DeparturePrepStage.Catering:
                    // One steadying the hi-loader, one at the forward galley door.
                    into.Add(new RampCrewMember(RampRole.Attending, -6.0f, -6.2f, 110f));
                    into.Add(new RampCrewMember(RampRole.Receiving, -3.2f, -3.4f, 270f));
                    break;

                case DeparturePrepStage.Baggage:
                    // One at the hold, one on the cart.
                    into.Add(new RampCrewMember(RampRole.Receiving, -14.0f, -3.8f, 270f));
                    into.Add(new RampCrewMember(RampRole.Attending, -16.5f, -6.8f, 300f));
                    break;

                case DeparturePrepStage.Boarding:
                    // Boarding is the passengers' business; one marshaller stays clear ahead.
                    into.Add(new RampCrewMember(RampRole.Marshalling, 9.0f, 7.5f, 200f));
                    break;

                default:
                    return;
            }

            if (activeVehicle is null && into.Count > 1)
                into.RemoveAt(0);
        }

        /// <summary>The vehicle working during a stage, or null when none is.</summary>
        public static GroundServiceKind? VehicleFor(DeparturePrepStage stage) => stage switch
        {
            DeparturePrepStage.Fuel => GroundServiceKind.Fuel,
            DeparturePrepStage.Catering => GroundServiceKind.Catering,
            DeparturePrepStage.Baggage => GroundServiceKind.Baggage,
            _ => null
        };

        /// <summary>
        /// Which character to use for a crew slot. Alternating by index keeps both imported
        /// workers in play rather than showing the same person twice beside one aircraft.
        /// </summary>
        public static bool IsFemale(int index) => index % 2 == 1;
    }
}
