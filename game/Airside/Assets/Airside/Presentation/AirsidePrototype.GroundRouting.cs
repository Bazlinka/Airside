using System.Collections.Generic;
using Airside.Domain;
using Airside.Simulation;
using UnityEngine;

namespace Airside.Presentation
{
    /// <summary>
    /// Ground vehicles and walkers go round what is in their way instead of through it.
    ///
    /// Service trucks, AI turnaround sets, remote buses and boarding passengers used to move in
    /// straight lines between two points, so they drove through parked aircraft, nacelles,
    /// propellers, buildings and the terminal. Every such trip is now planned by
    /// <see cref="GroundRouter"/> round:
    ///
    /// * every parked aircraft's <see cref="AircraftLayout"/> footprint — fuselage, nacelles,
    ///   propeller arcs, and any wing or tailplane too low to pass under;
    /// * YPAD's buildings and terminal outlines and the aerobridge rotundas.
    ///
    /// Aircraft that are moving (taxiing, pushing back) are not planned round — they will not
    /// be there for long — but a vehicle will not drive into one's path: it waits.
    ///
    /// Presentation only. The frontage-road legs of <see cref="GroundServiceRun"/> still follow
    /// the road itself, including the terminal undercroft, exactly as before.
    /// </summary>
    public sealed partial class AirsidePrototype
    {
        private const float VehicleClearanceMetres = 2.2f;
        private const float PersonClearanceMetres = 0.8f;
        private const float RouteReplanSeconds = 4f;

        private sealed class VehicleRoute
        {
            public Vector3 Goal;
            public readonly List<float> Points = new();
            public int Next;
            public float PlannedAt;
        }

        private readonly Dictionary<Transform, VehicleRoute> _vehicleRoutes = new();
        private readonly List<RouteObstacle> _routeObstacles = new();
        private readonly List<LayoutRect> _layoutRects = new();
        private readonly List<(Transform View, AircraftLayout Layout)> _movingAircraft = new();
        private List<RouteObstacle> _staticRouteObstacles;
        private int _movingAircraftFrame = -1;
        private int _routeObstaclesFrame = -1;
        private bool _routeObstaclesForVehicles;

        /// <summary>Buildings, terminal outlines and aerobridge rotundas: built once.</summary>
        private static List<RouteObstacle> BuildStaticRouteObstacles()
        {
            var list = new List<RouteObstacle>();
            foreach (var building in AdelaideBuildings.All)
                if (building.Xz != null && building.Xz.Length >= 6)
                    list.Add(new RouteObstacle(building.Xz, true));
            foreach (var outline in AdelaideLayout.Terminals)
                if (outline.Xz != null && outline.Xz.Length >= 6)
                    list.Add(new RouteObstacle(outline.Xz, true));
            foreach (var site in AdelaideAerobridges.Sites)
                list.Add(Octagon(site.RotundaX, site.RotundaZ, 2.6f));
            return list;
        }

        private static RouteObstacle Octagon(float x, float z, float radius)
        {
            var xz = new float[16];
            for (var i = 0; i < 8; i++)
            {
                var a = i * Mathf.PI / 4f;
                xz[i * 2] = x + Mathf.Cos(a) * radius;
                xz[i * 2 + 1] = z + Mathf.Sin(a) * radius;
            }

            return new RouteObstacle(xz, true);
        }

        /// <summary>
        /// Static obstacles plus every parked aircraft, for a vehicle or a walker. Rebuilt at
        /// most once a frame, and only when something is actually being planned.
        /// </summary>
        private List<RouteObstacle> RouteObstacles(bool forVehicles)
        {
            if (_routeObstaclesFrame == Time.frameCount && _routeObstaclesForVehicles == forVehicles)
                return _routeObstacles;
            _routeObstaclesFrame = Time.frameCount;
            _routeObstaclesForVehicles = forVehicles;
            _routeObstacles.Clear();
            _staticRouteObstacles ??= BuildStaticRouteObstacles();
            _routeObstacles.AddRange(_staticRouteObstacles);
            if (!FleetMode || _operations == null)
                return _routeObstacles;
            foreach (var aircraft in _operations.Fleet)
            {
                if (aircraft.Type.IsRotorcraft || aircraft.State != FleetState.AtStand
                    || !TryGroundView(aircraft, out var view))
                    continue;
                AddAircraftObstacles(view, AircraftLayout.For(aircraft.Type), forVehicles, _routeObstacles);
            }

            return _routeObstacles;
        }

        private bool TryGroundView(FleetAircraft aircraft, out Transform view)
        {
            view = null;
            return _fleetViewById.TryGetValue(aircraft.Registration, out view) && view != null
                && view.gameObject.activeInHierarchy && view.position.y < AirsideFlightPath.GroundY + 3f;
        }

        /// <summary>One aircraft's footprint as world quads (and, for walkers, its propeller arcs).</summary>
        private void AddAircraftObstacles(Transform view, AircraftLayout layout, bool forVehicles, List<RouteObstacle> into)
        {
            layout.Footprint(_layoutRects, forVehicles);
            if (!forVehicles && layout.IsTurboprop)
            {
                // People keep well clear of the arcs: further in front than behind.
                var e = layout.Engine;
                foreach (var side in new[] { -1f, 1f })
                    _layoutRects.Add(new LayoutRect(side * e.X - e.PropellerRadius - 1f, side * e.X + e.PropellerRadius + 1f,
                        e.PropellerZ - 1.5f, e.PropellerZ + 2.5f));
            }

            foreach (var r in _layoutRects)
            {
                var a = view.TransformPoint(new Vector3(r.MinX, 0f, r.MinZ));
                var b = view.TransformPoint(new Vector3(r.MaxX, 0f, r.MinZ));
                var c = view.TransformPoint(new Vector3(r.MaxX, 0f, r.MaxZ));
                var d = view.TransformPoint(new Vector3(r.MinX, 0f, r.MaxZ));
                into.Add(RouteObstacle.Quad(a.x, a.z, b.x, b.z, c.x, c.z, d.x, d.z));
            }
        }

        private void RefreshMovingAircraft()
        {
            if (_movingAircraftFrame == Time.frameCount)
                return;
            _movingAircraftFrame = Time.frameCount;
            _movingAircraft.Clear();
            if (!FleetMode || _operations == null)
                return;
            foreach (var aircraft in _operations.Fleet)
                if (!aircraft.Type.IsRotorcraft && aircraft.State != FleetState.AtStand
                    && TryGroundView(aircraft, out var view))
                    _movingAircraft.Add((view, AircraftLayout.For(aircraft.Type)));
        }

        /// <summary>Vehicle slow zone within 15 m of the visible ground-aircraft footprint.</summary>
        private bool WithinAircraftVehicleSlowZone(Vector3 point)
        {
            if (!FleetMode || _operations == null) return false;
            foreach (var aircraft in _operations.Fleet)
            {
                if (!TryGroundView(aircraft, out var view) || view.position.y - point.y > 4f) continue;
                var local = view.InverseTransformPoint(point);
                var layout = AircraftLayout.For(aircraft.Type);
                var dx = Mathf.Max(0f, Mathf.Abs(local.x) - layout.HalfSpan);
                var dz = Mathf.Max(0f, Mathf.Max(layout.TailZ - local.z, local.z - layout.NoseZ));
                if (dx * dx + dz * dz <= 15f * 15f) return true;
            }
            return false;
        }

        /// <summary>
        /// True when <paramref name="point"/> is under, or in the path of, a taxiing or
        /// pushing-back aircraft: its whole span, plus a corridor ahead of the nose and behind
        /// the tail.
        /// </summary>
        private bool InMovingAircraftPath(Vector3 point)
        {
            RefreshMovingAircraft();
            foreach (var (view, layout) in _movingAircraft)
            {
                var local = view.InverseTransformPoint(point);
                var halfSpan = layout.HalfSpan + 3f;
                if (Mathf.Abs(local.x) < halfSpan && local.z > layout.TailZ - 18f && local.z < layout.NoseZ + 25f)
                    return true;
            }

            return false;
        }

        /// <summary>
        /// The next point to steer for on a planned route from where the vehicle is to
        /// <paramref name="goal"/>. Plans on first use, when the goal moves, and every few
        /// seconds while travelling (aircraft come and go).
        /// </summary>
        private Vector3 RouteWaypoint(Transform vehicle, Vector3 goal, float clearance, bool forVehicles = true)
        {
            if (!_vehicleRoutes.TryGetValue(vehicle, out var route))
                _vehicleRoutes[vehicle] = route = new VehicleRoute();
            var here = vehicle.position;
            var arrived = Flat(here - goal).sqrMagnitude < 0.25f;
            var now = Time.unscaledTime;
            if (route.Points.Count < 4 || Flat(route.Goal - goal).sqrMagnitude > 2.25f
                || (!arrived && now - route.PlannedAt > RouteReplanSeconds))
            {
                GroundRouter.Plan(here.x, here.z, goal.x, goal.z, RouteObstacles(forVehicles), clearance, route.Points);
                route.Goal = goal;
                route.Next = 1;
                route.PlannedAt = now;
            }

            var last = route.Points.Count / 2 - 1;
            while (route.Next < last
                   && Flat(here - new Vector3(route.Points[route.Next * 2], 0f, route.Points[route.Next * 2 + 1])).sqrMagnitude < 0.64f)
                route.Next++;
            return route.Next >= last
                ? goal
                : new Vector3(route.Points[route.Next * 2], goal.y, route.Points[route.Next * 2 + 1]);
        }

        /// <summary>A planned polyline between two fixed points (a bus's park bay and its stand).</summary>
        private List<float> PlanFixedRoute(Vector3 from, Vector3 to, float clearance, bool forVehicles, List<float> into)
        {
            GroundRouter.Plan(from.x, from.z, to.x, to.z, RouteObstacles(forVehicles), clearance, into);
            return into;
        }

        /// <summary>World position of a point given in an aircraft's layout frame on a ground pose.</summary>
        private static Vector3 LayoutToWorld(GroundPose pose, (float X, float Z) local, float y)
        {
            var (x, z) = AircraftLayout.ToWorld(pose, local.X, local.Z);
            return new Vector3(x, y, z);
        }
    }
}
