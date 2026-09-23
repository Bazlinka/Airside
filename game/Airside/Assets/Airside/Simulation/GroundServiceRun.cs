using System;

namespace Airside.Simulation
{
    public enum GroundServiceKind
    {
        Fuel,
        Catering,
        Baggage
    }

    /// <summary>Where a ground vehicle is in its trip, independent of which stage is running.</summary>
    public enum GroundServicePhase
    {
        /// <summary>Parked at its depot; nothing to do.</summary>
        Staged,

        /// <summary>Driving the frontage road from the depot toward the stand.</summary>
        Outbound,

        /// <summary>Alongside the aircraft, working.</summary>
        Servicing,

        /// <summary>Driving the frontage road back to the depot.</summary>
        Inbound
    }

    public readonly struct GroundServiceRunStatus
    {
        public GroundServiceRunStatus(GroundServiceKind kind, GroundServicePhase phase,
            float roadX, float roadZ, bool underTerminal, float legProgress)
        {
            Kind = kind;
            Phase = phase;
            RoadX = roadX;
            RoadZ = roadZ;
            UnderTerminal = underTerminal;
            LegProgress = legProgress;
        }

        public GroundServiceKind Kind { get; }
        public GroundServicePhase Phase { get; }

        /// <summary>Position on the frontage road while driving; the stand entry otherwise.</summary>
        public float RoadX { get; }
        public float RoadZ { get; }

        /// <summary>True while the vehicle is inside the terminal undercroft.</summary>
        public bool UnderTerminal { get; }

        /// <summary>0..1 through the current leg.</summary>
        public float LegProgress { get; }

        public bool Visible => Phase != GroundServicePhase.Staged;
        public bool Driving => Phase is GroundServicePhase.Outbound or GroundServicePhase.Inbound;
    }

    /// <summary>
    /// Drives the fuel, catering and baggage vehicles along the real airside frontage road
    /// (<see cref="AdelaideServiceRoads"/>) instead of having them appear beside the aircraft.
    ///
    /// Each vehicle leaves its depot ahead of its stage, drives the frontage — passing under
    /// the Terminal 1 aerobridges, and through the undercroft when its depot is on the far
    /// side — works while the stage runs, then drives back. Presentation only: nothing in the
    /// simulation waits for a vehicle, exactly as with <see cref="BoardingFlow"/>.
    ///
    /// Pure function of fleet state and time, with no UnityEngine types, so the headless
    /// harness checks it.
    /// </summary>
    public static class GroundServiceRun
    {
        /// <summary>Typical apron service speed, metres per second (about 25 km/h).</summary>
        public const float RoadSpeedMetresPerSecond = 7.0f;

        /// <summary>A vehicle sets off this long before its stage is due to start.</summary>
        public const double ApproachLeadSeconds = 90.0;

        /// <summary>Depot position along the frontage, as a fraction of its length.</summary>
        public static float DepotFraction(GroundServiceKind kind) => kind switch
        {
            GroundServiceKind.Fuel => 0.04f,        // fuel farm, south-west end
            GroundServiceKind.Catering => 0.96f,    // catering unit, north-east end
            _ => 0.52f                              // baggage: reached via the undercroft spur
        };

        /// <summary>
        /// Baggage works out of the hall beneath the terminal, so its trip starts at the
        /// landside end of the authored undercroft spur and drives out under the building
        /// before joining the frontage. Fuel and catering start on the frontage itself.
        /// </summary>
        public static bool UsesUndercroft(GroundServiceKind kind) => kind == GroundServiceKind.Baggage;

        public static int DepotIndex(GroundServiceKind kind) =>
            UsesUndercroft(kind)
                ? AdelaideServiceRoads.UndercroftJoinIndex
                : (int)Math.Round(DepotFraction(kind) * (AdelaideServiceRoadPath.PointCount - 1));

        /// <summary>
        /// Fraction of a baggage trip spent on the spur rather than the frontage. The spur is
        /// short, so it is a small slice of the run — but it is the slice under the terminal.
        /// </summary>
        public static float SpurShare(int standIndex)
        {
            var spur = AdelaideServiceRoadPath.SpurDistanceBetween(
                AdelaideServiceRoadPath.SpurPointCount - 1, 0);
            var road = AdelaideServiceRoadPath.DistanceBetween(
                AdelaideServiceRoads.UndercroftJoinIndex, standIndex);
            var total = spur + road;
            return total <= 0.01f ? 1f : spur / total;
        }

        /// <summary>
        /// Where a vehicle is a fraction <paramref name="t"/> through its outbound trip.
        /// A baggage vehicle drives the spur out from under the terminal first, then the
        /// frontage; everything else drives the frontage alone.
        /// </summary>
        public static void TravelOut(GroundServiceKind kind, int depot, int stand, float t,
            out float x, out float z, out bool underTerminal)
        {
            if (!UsesUndercroft(kind))
            {
                AdelaideServiceRoadPath.Travel(depot, stand, t, out x, out z, out underTerminal);
                return;
            }

            var share = SpurShare(stand);
            if (t <= share && share > 0f)
            {
                AdelaideServiceRoadPath.TravelSpur(
                    AdelaideServiceRoadPath.SpurPointCount - 1, 0, t / share, out x, out z, out underTerminal);
                return;
            }

            var after = share >= 1f ? 1f : (t - share) / (1f - share);
            AdelaideServiceRoadPath.Travel(
                AdelaideServiceRoads.UndercroftJoinIndex, stand, after, out x, out z, out underTerminal);
        }

        private static DeparturePrepStage StageFor(GroundServiceKind kind) => kind switch
        {
            GroundServiceKind.Fuel => DeparturePrepStage.Fuel,
            GroundServiceKind.Catering => DeparturePrepStage.Catering,
            _ => DeparturePrepStage.Baggage
        };

        /// <summary>0..1 through the stage this vehicle serves, from the prep status.</summary>
        public static double StageProgress(DeparturePrepStatus prep, GroundServiceKind kind) => kind switch
        {
            GroundServiceKind.Fuel => prep.FuelProgress,
            GroundServiceKind.Catering => prep.CateringProgress,
            _ => prep.BaggageProgress
        };

        /// <summary>
        /// Where <paramref name="kind"/> is right now for an aircraft parked at
        /// <paramref name="standX"/>/<paramref name="standZ"/>.
        ///
        /// <paramref name="secondsUntilStage"/> is how long until this vehicle's stage begins;
        /// negative once it has begun. Callers that only know the current stage can pass 0
        /// while it is running and a large positive number otherwise.
        /// </summary>
        public static GroundServiceRunStatus For(GroundServiceKind kind, DeparturePrepStatus prep,
            float standX, float standZ, double secondsUntilStage)
        {
            var depot = DepotIndex(kind);
            var stand = AdelaideServiceRoadPath.NearestIndex(standX, standZ);
            var progress = StageProgress(prep, kind);
            var running = prep.Stage == StageFor(kind);

            // Working at the aircraft.
            if (running)
            {
                AdelaideServiceRoadPath.PointAt(stand, out var sx, out var sz);
                return new GroundServiceRunStatus(kind, GroundServicePhase.Servicing, sx, sz,
                    AdelaideServiceRoadPath.IsUndercroft(stand), (float)Math.Clamp(progress, 0.0, 1.0));
            }

            var driveSeconds = DriveSeconds(depot, stand);

            // Outbound: within the lead-in window before the stage starts.
            if (secondsUntilStage > 0 && secondsUntilStage <= ApproachLeadSeconds && driveSeconds > 0)
            {
                var elapsed = ApproachLeadSeconds - secondsUntilStage;
                var t = (float)Math.Clamp(elapsed / Math.Min(driveSeconds, ApproachLeadSeconds), 0.0, 1.0);
                TravelOut(kind, depot, stand, t, out var x, out var z, out var under);
                return new GroundServiceRunStatus(kind, GroundServicePhase.Outbound, x, z, under, t);
            }

            // Inbound: the stage is finished but the vehicle has not got home yet.
            if (progress >= 1.0 && driveSeconds > 0)
            {
                var sinceDone = -secondsUntilStage;
                if (sinceDone >= 0 && sinceDone < driveSeconds)
                {
                    var t = (float)Math.Clamp(sinceDone / driveSeconds, 0.0, 1.0);
                    TravelOut(kind, depot, stand, 1f - t, out var x, out var z, out var under);
                    return new GroundServiceRunStatus(kind, GroundServicePhase.Inbound, x, z, under, t);
                }
            }

            AdelaideServiceRoadPath.PointAt(depot, out var dx, out var dz);
            return new GroundServiceRunStatus(kind, GroundServicePhase.Staged, dx, dz,
                AdelaideServiceRoadPath.IsUndercroft(depot), 0f);
        }

        /// <summary>Seconds to drive between two frontage points at apron service speed.</summary>
        public static double DriveSeconds(int fromIndex, int toIndex) =>
            AdelaideServiceRoadPath.DistanceBetween(fromIndex, toIndex) / RoadSpeedMetresPerSecond;
    }
}
