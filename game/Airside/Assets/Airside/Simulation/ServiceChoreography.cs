using System;
using System.Collections.Generic;
using Airside.Domain;

namespace Airside.Simulation
{
    /// <summary>What a ramp worker has in their hands this moment.</summary>
    public enum CarriedItem
    {
        None,
        Bag,
        Nozzle,
        Canister,
        Trolley
    }

    /// <summary>
    /// Where one ramp worker is and what they are doing at an instant, in the aircraft's layout
    /// frame (<see cref="AircraftLayout"/>: x right, z forward) with a height above the apron.
    /// </summary>
    public readonly struct CrewAction
    {
        public CrewAction(float x, float z, float height, float facingDegrees, bool walking, CarriedItem item)
        {
            X = x;
            Z = z;
            Height = height;
            FacingDegrees = facingDegrees;
            Walking = walking;
            Item = item;
        }

        public float X { get; }
        public float Z { get; }
        public float Height { get; }
        public float FacingDegrees { get; }
        public bool Walking { get; }
        public CarriedItem Item { get; }
    }

    /// <summary>A bag, galley box or trolley on its way into the aircraft (off the hands, not yet inside).</summary>
    public readonly struct TransitItem
    {
        public TransitItem(CarriedItem kind, float x, float z, float height)
        {
            Kind = kind;
            X = x;
            Z = z;
            Height = height;
        }

        public CarriedItem Kind { get; }
        public float X { get; }
        public float Z { get; }
        public float Height { get; }
    }

    /// <summary>The equipment's state for one turnaround job at an instant.</summary>
    public sealed class ServiceScene
    {
        /// <summary>Share of the baggage train's load still on it (1 full, 0 empty).</summary>
        public float TrainLoad = 1f;

        /// <summary>Catering hi-loader box raised this far above its rest height, metres.</summary>
        public float LiftHeight;

        /// <summary>The fuel nozzle is off the truck (in a hand or on the coupling).</summary>
        public bool NozzleOut;

        /// <summary>Where the fuel hose leaves the truck's reel.</summary>
        public (float X, float Z) HoseReel;

        /// <summary>A jet's belt loader: foot on the apron, top at the hold sill.</summary>
        public bool BeltLoader;
        public (float X, float Z) BeltFoot;
        public (float X, float Z) BeltTop;
        public float BeltTopHeight;

        /// <summary>A turboprop's planeside bag cart by the stairs, and the bags on it now.</summary>
        public bool PlanesideCart;
        public (float X, float Z) PlanesideAt;
        public int PlanesideBags;

        public readonly List<TransitItem> Transit = new();

        public void Reset()
        {
            TrainLoad = 1f;
            LiftHeight = 0f;
            NozzleOut = false;
            HoseReel = default;
            BeltLoader = false;
            PlanesideCart = false;
            PlanesideBags = 0;
            Transit.Clear();
        }
    }

    /// <summary>
    /// Door sill heights above the apron for each type, from the same meshes as
    /// <see cref="AircraftLayout"/>: where a bag, galley box or trolley actually goes in.
    /// </summary>
    public readonly struct DoorSills
    {
        public DoorSills(float passenger, float cargo, float catering)
        {
            Passenger = passenger;
            Cargo = cargo;
            Catering = catering;
        }

        public float Passenger { get; }
        public float Cargo { get; }
        public float Catering { get; }

        public static DoorSills For(AircraftType type)
        {
            if (Is(type, AircraftType.Atr42)) return new DoorSills(1.22f, 1.17f, 1.22f);
            if (Is(type, AircraftType.Saab340)) return new DoorSills(1.29f, 1.43f, 1.29f);
            if (Is(type, AircraftType.Dash8Q400)) return new DoorSills(1.38f, 1.55f, 1.38f);
            var l1 = AircraftDoors.L1(type).SillY;
            if (Is(type, AircraftType.EmbraerE190)) return new DoorSills(l1, 2.39f, 3.37f);
            if (Is(type, AircraftType.AirbusA220300)) return new DoorSills(l1, 2.09f, 3.21f);
            if (Is(type, AircraftType.AirbusA320200) || Is(type, AircraftType.AirbusA321Neo))
                return new DoorSills(l1, 2.85f, 3.32f);
            if (Is(type, AircraftType.Boeing737800) || Is(type, AircraftType.Boeing7378))
                return new DoorSills(l1, 3.01f, 3.50f);
            // Widebodies: main deck around 4.7 m, lower-deck hold about 3.2 m.
            return new DoorSills(l1, 3.2f, 5.3f);
        }

        private static bool Is(AircraftType type, AircraftType candidate) => type != null && type.Id == candidate.Id;
    }

    /// <summary>
    /// The work of a turnaround, done by hand and item by item (ADR 0176): bags walked from the
    /// train and put in the hold — by hand on a turboprop, up a belt loader on a jet — the fuel
    /// nozzle walked out on its hose, connected and brought back, a hi-loader raised to a jet's
    /// service door and trolleys pushed across, galley boxes carried up a turboprop's airstair,
    /// and on a turboprop the passengers' roller bags left planeside and loaded by a handler.
    ///
    /// How many items move is set by the stage's length at a realistic pace per trip (and
    /// never more bags than seats), so a short stage moves a few bags and a long one many.
    ///
    /// A pure function of the stage clock like <see cref="BoardingFlow"/>: no state, nothing
    /// persisted, right through pause, time acceleration and reload. No UnityEngine types.
    /// </summary>
    public static class ServiceChoreography
    {
        /// <summary>Walking pace with a load, metres per second.</summary>
        public const float CarryPace = 1.3f;

        /// <summary>Shortest believable single-bag trip, seconds (pick, walk, put in, walk back).</summary>
        public const float MinBagCycleSeconds = 6.5f;

        public const float BeltSeconds = 4.5f;

        private const float HandHeight = 0.95f;

        /// <summary>The hi-loader's platform floor at rest (VEH-004); it rises from here to the door sill.</summary>
        public const float HiLoaderFloorMetres = 2.05f;

        /// <summary>Bags loaded in a baggage stage of <paramref name="stageSeconds"/> for a walk of <paramref name="walkMetres"/>.</summary>
        public static int BagsToLoad(AircraftType type, double stageSeconds, float walkMetres) =>
            Clamp((int)Math.Floor((stageSeconds - 6.0) / BagCycle(walkMetres)), 1, Math.Max(1, AircraftCatalogue.TypicalSeats(type)));

        public static float BagCycle(float walkMetres) => Math.Max(MinBagCycleSeconds, 2f * walkMetres / CarryPace + 3f);

        // ---- Shuttle ---------------------------------------------------------------------------

        /// <summary>One worker moving items from A to B one at a time.</summary>
        public readonly struct Shuttle
        {
            public Shuttle(int picked, int delivered, float leg, bool carrying, float handoff, bool walking)
            {
                Picked = picked;
                Delivered = delivered;
                Leg = leg;
                Carrying = carrying;
                Handoff = handoff;
                Walking = walking;
            }

            /// <summary>Items taken from A so far.</summary>
            public int Picked { get; }

            /// <summary>Items put down at B so far.</summary>
            public int Delivered { get; }

            /// <summary>0 at A, 1 at B.</summary>
            public float Leg { get; }

            public bool Carrying { get; }

            /// <summary>0..1 while the item leaves the hands at B; -1 otherwise.</summary>
            public float Handoff { get; }

            public bool Walking { get; }
        }

        /// <summary>
        /// Trips of <paramref name="cycle"/> seconds from <paramref name="start"/>: pick up at A,
        /// carry to B, put down, walk back. After <paramref name="count"/> trips the worker waits at A.
        /// </summary>
        public static Shuttle Trips(double elapsed, double start, double cycle, int count)
        {
            if (elapsed < start || count <= 0 || cycle <= 0)
                return new Shuttle(0, 0, 0f, false, -1f, false);
            var k = (int)Math.Floor((elapsed - start) / cycle);
            if (k >= count)
                return new Shuttle(count, count, 0f, false, -1f, false);
            return Trip(k, (float)((elapsed - start - k * cycle) / cycle));
        }

        /// <summary>
        /// Trips that each wait for an item to arrive at A (<paramref name="arrivals"/>, sorted):
        /// trip k starts at the later of its item's arrival and the end of trip k-1.
        /// </summary>
        public static Shuttle Queue(IReadOnlyList<double> arrivals, double cycle, double elapsed)
        {
            var free = double.MinValue;
            for (var k = 0; k < arrivals.Count; k++)
            {
                var start = Math.Max(arrivals[k], free);
                if (elapsed < start)
                    return new Shuttle(k, k, 0f, false, -1f, false);
                if (elapsed < start + cycle)
                    return Trip(k, (float)((elapsed - start) / cycle));
                free = start + cycle;
            }

            return new Shuttle(arrivals.Count, arrivals.Count, 0f, false, -1f, false);
        }

        private static Shuttle Trip(int k, float f)
        {
            const float pick = 0.12f, carry = 0.5f, drop = 0.62f;
            if (f < pick)
                return new Shuttle(f >= pick * 0.5f ? k + 1 : k, k, 0f, f >= pick * 0.5f, -1f, false);
            if (f < carry)
                return new Shuttle(k + 1, k, Smooth((f - pick) / (carry - pick)), true, -1f, true);
            if (f < drop)
                return new Shuttle(k + 1, k, 1f, false, (f - carry) / (drop - carry), false);
            return new Shuttle(k + 1, k + 1, 1f - Smooth((f - drop) / (1f - drop)), false, -1f, true);
        }

        // ---- Scene -----------------------------------------------------------------------------

        /// <summary>
        /// Actions for <paramref name="crew"/> (from <see cref="RampCrew"/>) at
        /// <paramref name="elapsed"/> seconds into a job of <paramref name="seconds"/>, and the
        /// equipment state in <paramref name="scene"/>. <paramref name="planesideDrops"/> are the
        /// seconds into the job at which boarding passengers leave a roller bag at
        /// <paramref name="planesideCart"/> (turboprop boarding only).
        /// </summary>
        public static void Act(RampActivity activity, AircraftType type, double elapsed, double seconds,
            IReadOnlyList<RampCrewMember> crew, List<CrewAction> actions, ServiceScene scene,
            IReadOnlyList<double> planesideDrops = null, (float X, float Z)? planesideCart = null, bool hiLoader = true)
        {
            actions.Clear();
            scene.Reset();
            var layout = AircraftLayout.For(type);
            var sills = DoorSills.For(type);
            seconds = Math.Max(1.0, seconds);
            elapsed = Math.Max(0.0, Math.Min(seconds, elapsed));
            foreach (var member in crew)
                actions.Add(Act(member, activity, layout, sills, type, elapsed, seconds, scene, planesideDrops, planesideCart,
                    hiLoader));
        }

        private static CrewAction Act(RampCrewMember m, RampActivity activity, AircraftLayout layout, DoorSills sills,
            AircraftType type, double e, double s, ServiceScene scene, IReadOnlyList<double> drops, (float X, float Z)? cart,
            bool hiLoader)
        {
            var still = new CrewAction(m.AcrossMetres, m.AlongMetres, 0f, m.FacingDegrees, false, CarriedItem.None);
            switch (m.Task)
            {
                case RampTask.FuelCoupling:
                    return FuelCoupling(layout, e, s, scene);
                case RampTask.BaggageCart when activity == RampActivity.Baggage:
                    return BaggageCarrier(layout, sills, type, e, s, scene);
                case RampTask.BaggageHold when activity == RampActivity.Baggage:
                    return BaggageReceiver(layout, sills, m);
                case RampTask.BaggageHold when activity == RampActivity.Boarding && drops != null && cart.HasValue:
                    return Planeside(layout, sills, e, drops, cart.Value, scene);
                case RampTask.CateringDoor when activity == RampActivity.Catering:
                    if (!layout.CateredByTruck)
                        return GalleyCarrier(layout, sills, e, s, scene);
                    return hiLoader ? HiLoaderPlatform(layout, sills, e, s, scene) : still;
                case RampTask.CateringLoader when activity == RampActivity.Catering && !layout.CateredByTruck:
                    return TrolleyPusher(layout, e, s);
                case RampTask.CateringLoader when activity == RampActivity.Catering && hiLoader:
                    scene.LiftHeight = Lift(layout, sills, e, s);
                    return still;
                default:
                    return still;
            }
        }

        private static CrewAction FuelCoupling(AircraftLayout layout, double e, double s, ServiceScene scene)
        {
            var truck = layout.FuelTruck;
            var coupling = layout.FuelCoupling;
            var toward = Math.Sign(coupling.X - truck.X);
            var reel = (truck.X + toward * 1.3f, truck.Z);
            var a = (reel.Item1 + toward * 0.8f, reel.Item2);
            scene.HoseReel = reel;
            var outAt = 0.02 * s;
            var connected = 0.14 * s;
            var disconnect = 0.86 * s;
            var stowed = 0.98 * s;
            scene.NozzleOut = e >= outAt && e < stowed;
            float leg;
            if (e < outAt) leg = 0f;
            else if (e < connected) leg = Smooth((float)((e - outAt) / (connected - outAt)));
            else if (e < disconnect) leg = 1f;
            else if (e < stowed) leg = 1f - Smooth((float)((e - disconnect) / (stowed - disconnect)));
            else leg = 0f;
            var walking = (e >= outAt && e < connected) || (e >= disconnect && e < stowed);
            var x = a.Item1 + (coupling.X - a.Item1) * leg;
            var z = a.Item2 + (coupling.Z - a.Item2) * leg;
            var facing = walking
                ? Heading(e < disconnect ? coupling.X - a.Item1 : a.Item1 - coupling.X,
                    e < disconnect ? coupling.Z - a.Item2 : a.Item2 - coupling.Z)
                : Heading(-toward, 0.4f);
            return new CrewAction(x, z, 0f, facing, walking, scene.NozzleOut ? CarriedItem.Nozzle : CarriedItem.None);
        }

        /// <summary>Hold end of the bag run: by hand into a turboprop hold, onto a belt loader for a jet.</summary>
        private static ((float X, float Z) A, (float X, float Z) B, float Walk) BagRun(AircraftLayout layout, ServiceScene scene,
            DoorSills sills)
        {
            var door = layout.CargoDoor;
            var side = AircraftLayout.SideOf(door);
            var train = layout.BaggageTrain;
            (float X, float Z) b;
            if (layout.IsTurboprop)
                b = (door.X + side * 1.0f, door.Z - 0.6f);
            else
            {
                scene.BeltLoader = true;
                scene.BeltFoot = (door.X + side * 5.5f, door.Z);
                scene.BeltTop = (door.X + side * 0.3f, door.Z);
                scene.BeltTopHeight = sills.Cargo;
                b = (scene.BeltFoot.X + side * 0.8f, door.Z + 0.9f);
            }

            var a = (train.X - side * 1.2f, train.Z + Math.Sign(door.Z - train.Z) * 1.2f);
            var walk = Distance(a, b);
            return (a, b, walk);
        }

        private static CrewAction BaggageCarrier(AircraftLayout layout, DoorSills sills, AircraftType type, double e, double s,
            ServiceScene scene)
        {
            var (a, b, walk) = BagRun(layout, scene, sills);
            var cycle = BagCycle(walk);
            var count = BagsToLoad(type, s, walk);
            var trips = Trips(e, 3.0, cycle, count);
            scene.TrainLoad = 1f - trips.Picked / (float)count;
            var door = layout.CargoDoor;
            var side = AircraftLayout.SideOf(door);

            if (layout.IsTurboprop && trips.Handoff >= 0f)
            {
                // Lifted from the handler's hands through the hold door.
                var t = trips.Handoff;
                scene.Transit.Add(new TransitItem(CarriedItem.Bag, Lerp(b.X, door.X - side * 0.4f, t),
                    Lerp(b.Z, door.Z, t), Lerp(HandHeight, sills.Cargo + 0.35f, t)));
            }
            else if (!layout.IsTurboprop)
            {
                // Put on the belt, then carried up it into the hold.
                var since = BeltTime(e, 3.0, cycle, count);
                if (since >= 0f && since < BeltSeconds)
                {
                    var t = since / BeltSeconds;
                    scene.Transit.Add(new TransitItem(CarriedItem.Bag, Lerp(scene.BeltFoot.X, scene.BeltTop.X, t),
                        Lerp(scene.BeltFoot.Z, scene.BeltTop.Z, t), Lerp(0.8f, scene.BeltTopHeight + 0.2f, t)));
                }
            }

            var x = Lerp(a.X, b.X, trips.Leg);
            var z = Lerp(a.Z, b.Z, trips.Leg);
            var outbound = trips.Carrying || (trips.Handoff < 0f && trips.Leg > 0f && trips.Delivered < trips.Picked);
            var facing = trips.Walking
                ? (outbound ? Heading(b.X - a.X, b.Z - a.Z) : Heading(a.X - b.X, a.Z - b.Z))
                : trips.Leg > 0.5f ? Heading(door.X - x, door.Z - z) : Heading(a.X - b.X, a.Z - b.Z);
            return new CrewAction(x, z, 0f, facing, trips.Walking, trips.Carrying ? CarriedItem.Bag : CarriedItem.None);
        }

        /// <summary>Seconds since the latest bag was put on the belt, or -1.</summary>
        private static float BeltTime(double e, double start, double cycle, int count)
        {
            if (e < start)
                return -1f;
            var k = (int)Math.Floor((e - start) / cycle);
            var into = e - start - k * cycle;
            // The handoff ends 0.62 of the way through a trip.
            var placed = 0.62 * cycle;
            if (k < count && into >= placed)
                return (float)(into - placed);
            if (k >= 1 && k - 1 < count)
                return (float)(into + cycle - placed);
            return -1f;
        }

        private static CrewAction BaggageReceiver(AircraftLayout layout, DoorSills sills, RampCrewMember m)
        {
            var door = layout.CargoDoor;
            var side = AircraftLayout.SideOf(door);
            if (layout.IsTurboprop)
            {
                var at = (door.X + side * 0.9f, door.Z + 0.7f);
                return new CrewAction(at.Item1, at.Item2, 0f, Heading(door.X - at.Item1, door.Z - at.Item2), false, CarriedItem.None);
            }

            // At the belt foot, feeding bags on.
            var foot = (door.X + side * 5.5f, door.Z - 1.0f);
            return new CrewAction(foot.Item1 + side * 0.8f, foot.Item2, 0f, Heading(-side, 0f), false, CarriedItem.None);
        }

        private static CrewAction Planeside(AircraftLayout layout, DoorSills sills, double e, IReadOnlyList<double> drops,
            (float X, float Z) cart, ServiceScene scene)
        {
            var door = layout.CargoDoor;
            var side = AircraftLayout.SideOf(door);
            var a = (cart.X + side * 0.7f, cart.Z);
            var b = (door.X + side * 1.0f, door.Z);
            var cycle = BagCycle(Distance(a, b));
            var trips = Queue(drops, cycle, e);
            var arrived = 0;
            foreach (var t in drops)
                if (t <= e)
                    arrived++;
            scene.PlanesideCart = true;
            scene.PlanesideAt = cart;
            scene.PlanesideBags = Math.Max(0, arrived - trips.Picked);
            if (trips.Handoff >= 0f)
            {
                var t = trips.Handoff;
                scene.Transit.Add(new TransitItem(CarriedItem.Bag, Lerp(b.Item1, door.X - side * 0.4f, t),
                    Lerp(b.Item2, door.Z, t), Lerp(HandHeight, sills.Cargo + 0.35f, t)));
            }

            var x = Lerp(a.Item1, b.Item1, trips.Leg);
            var z = Lerp(a.Item2, b.Item2, trips.Leg);
            var facing = trips.Walking
                ? (trips.Carrying ? Heading(b.Item1 - a.Item1, b.Item2 - a.Item2) : Heading(a.Item1 - b.Item1, a.Item2 - b.Item2))
                : trips.Leg > 0.5f ? Heading(door.X - x, door.Z - z) : Heading(cart.X - x, cart.Z - z);
            return new CrewAction(x, z, 0f, facing, trips.Walking, trips.Carrying ? CarriedItem.Bag : CarriedItem.None);
        }

        private static float Lift(AircraftLayout layout, DoorSills sills, double e, double s)
        {
            // Up over the first 14% of the job, down over the last 14%, to put the box floor at the sill.
            var up = Smooth((float)Math.Min(1.0, e / (0.14 * s)));
            var down = Smooth((float)Math.Max(0.0, (e - 0.86 * s) / (0.14 * s)));
            return Math.Max(0f, sills.Catering - HiLoaderFloorMetres) * Math.Max(0f, up - down);
        }

        private static CrewAction HiLoaderPlatform(AircraftLayout layout, DoorSills sills, double e, double s, ServiceScene scene)
        {
            var door = layout.CateringDoor ?? layout.PassengerDoor;
            var side = AircraftLayout.SideOf(door);
            scene.LiftHeight = Lift(layout, sills, e, s);
            var floor = HiLoaderFloorMetres + scene.LiftHeight;
            // From the box mouth across the platform over the cab to the door threshold.
            var a = (door.X + side * 2.3f, door.Z);
            var b = (door.X + side * 0.5f, door.Z);
            var window = 0.68 * s;
            var count = Clamp((int)Math.Floor(window / 10.0), 2, 6);
            var trips = Trips(e, 0.16 * s, window / count, count);
            if (trips.Handoff >= 0f)
                scene.Transit.Add(new TransitItem(CarriedItem.Trolley, Lerp(b.Item1, door.X - side * 0.9f, trips.Handoff),
                    door.Z, floor));
            var x = Lerp(a.Item1, b.Item1, trips.Leg);
            var facing = trips.Carrying || trips.Handoff >= 0f ? Heading(-side, 0f) : trips.Walking ? Heading(side, 0f) : Heading(-side, 0f);
            return new CrewAction(x, door.Z - 0.5f, floor, facing, trips.Walking,
                trips.Carrying ? CarriedItem.Trolley : CarriedItem.None);
        }

        private static CrewAction TrolleyPusher(AircraftLayout layout, double e, double s)
        {
            var door = layout.PassengerDoor;
            var side = AircraftLayout.SideOf(door);
            var from = (door.X + side * 8f, door.Z + 3.5f);
            var to = TrolleyStop(layout);
            var t = Smooth((float)Math.Min(1.0, e / (0.15 * s)));
            var x = Lerp(from.Item1, to.X, t);
            var z = Lerp(from.Item2, to.Z, t);
            return new CrewAction(x, z, 0f, Heading(to.X - from.Item1, to.Z - from.Item2), t < 1f, CarriedItem.Trolley);
        }

        /// <summary>Where a turboprop's galley trolley stands, beside its airstair.</summary>
        public static (float X, float Z) TrolleyStop(AircraftLayout layout)
        {
            var door = layout.PassengerDoor;
            var side = AircraftLayout.SideOf(door);
            return (door.X + side * 2.8f, door.Z + 1.6f);
        }

        private static CrewAction GalleyCarrier(AircraftLayout layout, DoorSills sills, double e, double s, ServiceScene scene)
        {
            var door = layout.PassengerDoor;
            var side = AircraftLayout.SideOf(door);
            var trolley = TrolleyStop(layout);
            var run = sills.Passenger * 1.15f + 0.5f;
            // Trolley → stair foot → up the airstair → just inside the door.
            var path = new[]
            {
                (trolley.X - side * 0.2f, trolley.Z - 1.0f, 0f),
                (door.X + side * run, door.Z, 0f),
                (door.X + side * 0.35f, door.Z, sills.Passenger),
                (door.X - side * 0.6f, door.Z, sills.Passenger)
            };
            var count = Clamp((int)Math.Floor(0.78 * s / 13.0), 2, 6);
            var trips = Trips(e, 0.16 * s, 0.8 * s / count, count);
            var (x, z, h, dx, dz) = AlongPath(path, trips.Leg);
            var facing = trips.Carrying || trips.Leg >= 0.999f ? Heading(dx, dz) : Heading(-dx, -dz);
            return new CrewAction(x, z, h, facing, trips.Walking, trips.Carrying ? CarriedItem.Canister : CarriedItem.None);
        }

        // ---- Helpers ---------------------------------------------------------------------------

        private static (float X, float Z, float H, float Dx, float Dz) AlongPath((float X, float Z, float H)[] path, float t)
        {
            var lengths = new float[path.Length - 1];
            var total = 0f;
            for (var i = 0; i < lengths.Length; i++)
            {
                var dx = path[i + 1].X - path[i].X;
                var dz = path[i + 1].Z - path[i].Z;
                var dh = path[i + 1].H - path[i].H;
                lengths[i] = (float)Math.Sqrt(dx * dx + dz * dz + dh * dh);
                total += lengths[i];
            }

            var d = t * total;
            for (var i = 0; i < lengths.Length; i++)
            {
                if (d <= lengths[i] || i == lengths.Length - 1)
                {
                    var f = lengths[i] > 1e-4f ? Math.Min(1f, d / lengths[i]) : 1f;
                    var p = path[i];
                    var q = path[i + 1];
                    return (Lerp(p.X, q.X, f), Lerp(p.Z, q.Z, f), Lerp(p.H, q.H, f), q.X - p.X, q.Z - p.Z);
                }

                d -= lengths[i];
            }

            var last = path[path.Length - 1];
            return (last.X, last.Z, last.H, 0f, 1f);
        }

        /// <summary>Facing in degrees clockwise from the aircraft's nose for a direction (dx right, dz forward).</summary>
        private static float Heading(float dx, float dz) =>
            Math.Abs(dx) + Math.Abs(dz) < 1e-5f ? 0f : (float)(Math.Atan2(dx, dz) * 180.0 / Math.PI);

        private static float Distance((float X, float Z) a, (float X, float Z) b)
        {
            var dx = b.X - a.X;
            var dz = b.Z - a.Z;
            return (float)Math.Sqrt(dx * dx + dz * dz);
        }

        private static float Lerp(float a, float b, float t) => a + (b - a) * t;
        private static float Smooth(float t) => t <= 0f ? 0f : t >= 1f ? 1f : t * t * (3f - 2f * t);
        private static int Clamp(int v, int min, int max) => v < min ? min : v > max ? max : v;
    }
}
