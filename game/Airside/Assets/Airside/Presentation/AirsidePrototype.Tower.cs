using Airside.Simulation;
using UnityEngine;

namespace Airside.Presentation
{
    public sealed partial class AirsidePrototype
    {
        // Click the Adelaide control tower to stand in its cab and watch the field the way the controller does.
        private Transform _towerEye;
        private Transform _towerCab;
        private bool InTower => _cameraController != null && _cameraController.IsTower;
        private ControlTowerView.Pose _towerPose;
        private bool _towerPoseReady;
        private bool _towerHinted;

        private bool TowerPoseAvailable()
        {
            if (_towerPoseReady) return true;
            if (!ControlTowerView.TryFind(out var tower)) return false;
            ControlTowerView.Centre(tower.Xz, out var cx, out var cz);
            // Face the middle of the airfield; the player turns from there.
            _towerPose = ControlTowerView.PoseFor(tower, AirsideAdelaideGround.WorldHeight(cx, cz), 0f, 0f);
            return _towerPoseReady = true;
        }

        private bool TowerViewAllowed() =>
            _cameraController != null && !InCockpit && !WatchingOutstation && !_cameraController.WatchingIndependentAirport
            && _flightOriginX == 0 && _flightOriginZ == 0;

        /// <summary>A field click that lands on the tower enters its cab, unless an aircraft is in front of it.</summary>
        private bool TryEnterTowerAtScreen(Camera camera, Vector2 inputSystemPosition)
        {
            if (!TowerViewAllowed() || InTower || !TowerPoseAvailable()) return false;
            var ray = camera.ScreenPointToRay(inputSystemPosition);
            var o = ray.origin;
            var d = ray.direction;
            if (!ControlTowerView.RayHits(_towerPose, o.x, o.y, o.z, d.x, d.y, d.z, AirsideBareField.MaxOrbitDistance * 2f, out var distance))
                return false;
            var layer = LayerMask.NameToLayer(AircraftPickRouting.PickLayerName);
            if (layer >= 0 && Physics.Raycast(ray, distance, 1 << layer, QueryTriggerInteraction.Collide)) return false;
            return EnterTower();
        }

        private bool EnterTower()
        {
            if (!TowerViewAllowed() || !TowerPoseAvailable()) return false;
            if (_towerEye == null)
            {
                var go = new GameObject("Control tower eye");
                _towerEye = go.transform;
            }
            _towerEye.SetPositionAndRotation(new Vector3(_towerPose.X, _towerPose.Y, _towerPose.Z),
                Quaternion.Euler(0f, _towerPose.FacingDegrees, 0f));
            BuildTowerCab();
            _towerCab.gameObject.SetActive(true);
            ClearAircraftSelection();
            _activeWorkspace = HudWorkspace.None;
            _devToolsOpen = _controlsHelpOpen = false;
            if (!_cameraController.StartTower(_towerEye)) return false;
            PlayUiClick();
            if (!_towerHinted)
            {
                _towerHinted = true;
                ShowToast("Control tower: drag to look round, scroll to zoom, Esc to leave.");
            }
            return true;
        }

        private void ExitTower()
        {
            if (!InTower) return;
            _cameraController.EndCockpit();
            PlayUiClick();
        }

        /// <summary>Drops the eye object once the camera has left the tower by any route (Esc, follow, an aircraft view).</summary>
        private void UpdateTowerView()
        {
            if (_towerCab != null) _towerCab.gameObject.SetActive(InTower);
            if (_towerEye != null && !InTower)
            {
                Destroy(_towerEye.gameObject);
                _towerEye = null;
            }
        }

        // Original, lightweight interior of the existing mapped cab; no new building or surveyed layout.
        private void BuildTowerCab()
        {
            if (_towerCab != null || !ControlTowerView.TryFind(out var tower)) return;
            _towerCab = new GameObject("Control tower cab interior").transform;
            var floorY = _towerPose.Y - ControlTowerView.EyeAboveFloorMetres;
            var ceilingY = _towerPose.GroundY + tower.HeightMetres - 3.2f - 0.04f;
            var centre = new Vector3(_towerPose.X, floorY, _towerPose.Z);
            var floor = new SurfaceMesh();
            var ceiling = new SurfaceMesh();
            var structure = new SurfaceMesh();
            var desks = new SurfaceMesh();
            var equipment = new SurfaceMesh();
            var displays = new SurfaceMesh();
            var marks = new SurfaceMesh();
            var count = tower.Xz.Length / 2;
            var low = new int[count];
            var high = new int[count];
            Vector3 Corner(int i, float scale, float y) => new Vector3(
                centre.x + (tower.Xz[i * 2] - centre.x) * scale, y,
                centre.z + (tower.Xz[i * 2 + 1] - centre.z) * scale);
            void Box(SurfaceMesh mesh, Vector3 at, float length, float height, float depth, Vector3 along) =>
                AddBox(mesh, new DetailBox(BuildingPart.Trim, at.x, at.y, at.z,
                    length, height, depth, along.x, along.z));
            for (var i = 0; i < count; i++)
            {
                low[i] = floor.Add(Corner(i, 0.88f, floorY + 0.025f));
                high[i] = ceiling.Add(Corner(i, 1.07f, ceilingY));
                var next = (i + 1) % count;
                var a = Corner(i, 0.88f, floorY);
                var b = Corner(next, 0.88f, floorY);
                var edge = b - a;
                var along = edge.normalized;
                var midpoint = (a + b) * 0.5f;
                var inward = (centre - midpoint).normalized;
                // Knee wall, continuous sill, and a ceiling fascia make the cab's enclosure readable.
                Box(structure, midpoint + Vector3.up * 0.32f, edge.magnitude, 0.64f, 0.18f, along);
                Box(desks, midpoint + Vector3.up * 0.72f, edge.magnitude, 0.12f, 0.35f, along);
                var topMid = (Corner(i, 1.07f, ceilingY) + Corner(next, 1.07f, ceilingY)) * 0.5f;
                Box(structure, topMid - Vector3.up * 0.16f, edge.magnitude * 1.07f / 0.88f, 0.32f, 0.22f, along);
                // Canted structural posts follow the existing wider upper glazing ring.
                var bottom = a + Vector3.up * 0.7f;
                var top = Corner(i, 1.07f, ceilingY);
                var post = CreateBlock("Tower window mullion", (bottom + top) * 0.5f,
                    new Vector3(0.16f, (top - bottom).magnitude, 0.16f), new Color(0.38f, 0.42f, 0.43f));
                post.transform.rotation = Quaternion.FromToRotation(Vector3.up, top - bottom);
                post.transform.SetParent(_towerCab, true);
                // The console is below the sight line and leaves a clear central circulation space.
                var station = midpoint + inward * 0.7f;
                Box(desks, station + Vector3.up * 0.89f, edge.magnitude * 0.83f, 0.12f, 0.9f, along);
                Box(equipment, station + Vector3.up * 0.44f, edge.magnitude * 0.7f, 0.8f, 0.52f, along);
                var screen = station + Vector3.up * 1.25f - inward * 0.15f;
                Box(equipment, screen, 0.98f, 0.57f, 0.09f, along);
                Box(displays, screen + inward * 0.052f, 0.88f, 0.47f, 0.012f, along);
                // Abstract radar grid/tracks in geometry, not baked text or a second operational radar.
                for (var line = -1; line <= 1; line++)
                {
                    Box(marks, screen + inward * 0.06f + Vector3.up * line * 0.11f, 0.8f, 0.005f, 0.004f, along);
                    Box(marks, screen + inward * 0.06f + along * line * 0.21f, 0.005f, 0.41f, 0.004f, along);
                }
                Box(marks, screen + inward * 0.065f + along * 0.13f + Vector3.up * 0.07f, 0.045f, 0.025f, 0.004f, along);
                Box(equipment, station + inward * 0.18f + Vector3.up * 0.96f, 0.53f, 0.035f, 0.2f, along);
            }
            foreach (var (a, b, c) in EarClip(tower.Xz))
            {
                floor.Triangle(low[a], low[b], low[c], Vector3.up);
                // Exterior prisms have no underside: this is the missing inward-facing roof surface.
                ceiling.Triangle(high[a], high[b], high[c], Vector3.down);
            }
            SpawnSurface(_towerCab, "Tower carpet", floor, new Color(0.21f, 0.25f, 0.26f), null, false, false);
            SpawnSurface(_towerCab, "Tower ceiling", ceiling, new Color(0.67f, 0.69f, 0.68f), null, false, false);
            SpawnSurface(_towerCab, "Tower interior framing", structure, new Color(0.47f, 0.51f, 0.51f), null, false, false);
            SpawnSurface(_towerCab, "Tower console worktops", desks, new Color(0.36f, 0.41f, 0.42f), null, false, false);
            SpawnSurface(_towerCab, "Tower console equipment", equipment, new Color(0.09f, 0.13f, 0.15f), null, false, false);
            SpawnSurface(_towerCab, "Tower display glass", displays, new Color(0.025f, 0.07f, 0.08f), null, false, false,
                "Universal Render Pipeline/Unlit");
            SpawnSurface(_towerCab, "Tower display tracks", marks, new Color(0.24f, 0.56f, 0.46f), null, false, false,
                "Universal Render Pipeline/Unlit");
        }

        private void DrawTowerHud(HudLayout layout, GUIStyle panel, GUIStyle title, GUIStyle button)
        {
            _hudPanels.Clear();
            _cameraController.KeyboardCaptured = _menuOpen || GUIUtility.keyboardControl != 0;
            const float width = 360f;
            var box = new Rect((layout.Viewport.x - width) * 0.5f, 16f, width, 92f);
            _hudPanels.Add(box);
            GUI.Box(box, GUIContent.none, panel);
            GUI.Label(new Rect(box.x + 16f, box.y + 10f, width - 32f, 26f), "CONTROL TOWER · YPAD", title);
            GUI.Label(new Rect(box.x + 16f, box.y + 40f, width - 32f, 20f), "Drag to look · scroll to zoom · Home recentres");
            if (GUI.Button(new Rect(box.x + 16f, box.y + 60f, 130f, 24f), "LEAVE TOWER", button)) ExitTower();
        }
    }
}
