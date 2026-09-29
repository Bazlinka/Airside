using System.Collections.Generic;
using Airside.Simulation;
using UnityEngine;

namespace Airside.Presentation
{
    /// <summary>The airport security fence along the real aerodrome boundary (ADR 0181).</summary>
    public sealed partial class AirsidePrototype
    {
        /// <summary>Launch flag that leaves the boundary fence out.</summary>
        private const string NoBoundaryFenceFlag = "-airsideNoBoundaryFence";

        private static void BuildAdelaideBoundaryFence()
        {
            var runs = AdelaideBoundaryFence.Runs();
            if (runs.Length == 0)
                return;
            var h = AirsideAdelaidePerimeter.FenceHeightMetres;
            var guard = AirsideAdelaidePerimeter.TopGuardHeightMetres;
            var thick = AirsideAdelaidePerimeter.PanelThicknessMetres;
            var post = AirsideAdelaidePerimeter.PostSizeMetres;
            var root = new GameObject("Adelaide boundary fence").transform;
            if (_airfieldRoot != null)
                root.SetParent(_airfieldRoot, false);

            float BaseY(float x, float z) => _bareGroundFollowsLandform
                ? AirsideAdelaidePerimeter.FenceBaseY(x, z)
                : AirsideAdelaideGround.PavementWorldY - AirsideAdelaidePerimeter.FenceEmbedMetres;

            var panels = new List<Matrix4x4>(runs.Length);
            var guards = new List<Matrix4x4>(runs.Length);
            var posts = new List<Matrix4x4>(runs.Length * 2);
            foreach (var run in runs)
            {
                var dx = run.X1 - run.X0;
                var dz = run.Z1 - run.Z0;
                var length = run.Length;
                if (length < 0.05f)
                    continue;
                var yaw = Quaternion.Euler(0f, Mathf.Atan2(dx, dz) * Mathf.Rad2Deg, 0f);
                var midX = (run.X0 + run.X1) * 0.5f;
                var midZ = (run.Z0 + run.Z1) * 0.5f;
                var baseY = _bareGroundFollowsLandform
                    ? AirsideAdelaidePerimeter.FenceBaseYAlongSegment(run.X0, run.Z0, run.X1, run.Z1)
                    : BaseY(midX, midZ);
                panels.Add(Matrix4x4.TRS(new Vector3(midX, baseY + h * 0.5f, midZ), yaw, new Vector3(thick, h, length)));
                guards.Add(Matrix4x4.TRS(new Vector3(midX, baseY + h + guard * 0.5f, midZ), yaw,
                    new Vector3(thick * 0.7f, guard, length)));
                posts.Add(Matrix4x4.TRS(new Vector3(run.X0, BaseY(run.X0, run.Z0) + (h + guard) * 0.5f, run.Z0),
                    Quaternion.identity, new Vector3(post, h + guard, post)));
                posts.Add(Matrix4x4.TRS(new Vector3(run.X1, BaseY(run.X1, run.Z1) + (h + guard) * 0.5f, run.Z1),
                    Quaternion.identity, new Vector3(post, h + guard, post)));
            }

            SpawnBoundaryBatch(root, "Boundary fence mesh", panels, new Color(0.42f, 0.44f, 0.46f));
            SpawnBoundaryBatch(root, "Boundary fence guard", guards, new Color(0.55f, 0.56f, 0.58f));
            SpawnBoundaryBatch(root, "Boundary fence posts", posts, new Color(0.32f, 0.33f, 0.35f));

            // A yellow gate leaf pair at each vehicle gate, where an airside road leaves the airport.
            var gates = AdelaideBoundary.Gates;
            var leaves = new List<Matrix4x4>();
            for (var g = 0; g + 1 < gates.Length; g += 2)
            {
                var gx = gates[g];
                var gz = gates[g + 1];
                var facing = NearestRunDirection(runs, gx, gz);
                var yaw = Quaternion.Euler(0f, Mathf.Atan2(facing.x, facing.y) * Mathf.Rad2Deg, 0f);
                var along = new Vector3(facing.x, 0f, facing.y);
                var gw = AirsideAdelaidePerimeter.VehicleGateWidthMetres;
                var gh = AirsideAdelaidePerimeter.GateLeafHeightMetres;
                var y = BaseY(gx, gz);
                for (var side = -1; side <= 1; side += 2)
                    leaves.Add(Matrix4x4.TRS(new Vector3(gx, y + gh * 0.5f, gz) + along * (side * gw * 0.26f), yaw,
                        new Vector3(thick * 1.2f, gh, gw * 0.48f)));
            }

            SpawnBoundaryBatch(root, "Boundary gate leaves", leaves, new Color(0.90f, 0.75f, 0.10f));
        }

        private static Vector2 NearestRunDirection(FenceRun[] runs, float x, float z)
        {
            var best = float.MaxValue;
            var direction = Vector2.right;
            foreach (var run in runs)
            {
                var mx = (run.X0 + run.X1) * 0.5f;
                var mz = (run.Z0 + run.Z1) * 0.5f;
                var d = (mx - x) * (mx - x) + (mz - z) * (mz - z);
                if (d >= best)
                    continue;
                best = d;
                direction = new Vector2(run.X1 - run.X0, run.Z1 - run.Z0).normalized;
            }

            return direction;
        }

        private static void SpawnBoundaryBatch(Transform root, string name, List<Matrix4x4> locals, Color color)
        {
            if (locals.Count == 0)
                return;
            var mesh = AirsideMeshUtil.CombineTransformed(BuiltinCube(), locals.ToArray());
            if (mesh == null)
                return;
            var go = new GameObject(name);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = CreateSharedSurfaceMaterial(color);
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            go.transform.SetParent(root, false);
            AirsideSceneIndex.Remember(go);
        }
    }
}
