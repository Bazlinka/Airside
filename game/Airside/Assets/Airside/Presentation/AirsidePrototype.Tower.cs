using Airside.Simulation;
using UnityEngine;

namespace Airside.Presentation
{
    public sealed partial class AirsidePrototype
    {
        // Click the Adelaide control tower to stand in its cab and watch the field the way the controller does.
        private Transform _towerEye;
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
            if (_towerEye != null && !InTower)
            {
                Destroy(_towerEye.gameObject);
                _towerEye = null;
            }
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
