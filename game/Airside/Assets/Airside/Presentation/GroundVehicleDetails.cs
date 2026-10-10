using System;
using System.Linq;
using UnityEngine;

namespace Airside.Presentation
{
    /// <summary>Driver and lamps on the existing vehicle, updated only by its owning motion path.</summary>
    public sealed class GroundVehicleDetails : MonoBehaviour
    {
        private Light _beacon;
        private Transform[] _frontAxles;
        private Quaternion[] _frontRest;
        private float _steering;
        public static Color Shade(Color color, float scale) => new(color.r * scale, color.g * scale, color.b * scale, 1f);
        public static void Build(Transform vehicle)
        {
            var details = vehicle.gameObject.AddComponent<GroundVehicleDetails>();
            var parts = vehicle.GetComponentsInChildren<MeshFilter>(true);
            var seat = parts.FirstOrDefault(p => p.name.EndsWith("tug_seat", StringComparison.OrdinalIgnoreCase));
            var cab = parts.FirstOrDefault(p => p.name.EndsWith(" cab", StringComparison.OrdinalIgnoreCase)
                || p.name.EndsWith(" cab_roof", StringComparison.OrdinalIgnoreCase));
            var center = seat != null ? seat.transform.TransformPoint(seat.sharedMesh.bounds.center)
                : cab != null ? cab.transform.TransformPoint(cab.sharedMesh.bounds.center) : Vector3.zero;
            if (seat != null || cab != null)
                Driver(vehicle, center + Vector3.up * (seat != null ? .25f : -.35f));
            foreach (var headlamp in parts.Where(p => p.name.EndsWith("Headlight L", StringComparison.OrdinalIgnoreCase)
                || p.name.EndsWith("Headlight R", StringComparison.OrdinalIgnoreCase)))
            {
                var spot = new GameObject("Vehicle safety headlamp").AddComponent<Light>();
                spot.transform.SetParent(headlamp.transform,false);
                spot.transform.localPosition = headlamp.sharedMesh.bounds.center;
                spot.transform.rotation = Quaternion.LookRotation(vehicle.right,vehicle.up);
                spot.type = LightType.Spot; spot.color = new Color(1,.94f,.8f); spot.intensity=.7f;
                spot.range=16; spot.spotAngle=48; spot.shadows=LightShadows.None;
            }
            // A small amber source on the existing beacon; no light floating above a missing lamp.
            var lamp = parts.FirstOrDefault(p => p.name.EndsWith(" beacon", StringComparison.OrdinalIgnoreCase));
            if (lamp != null)
            {
                var root = new GameObject("Amber working beacon").transform;
                root.SetParent(lamp.transform, false); root.localPosition = lamp.sharedMesh.bounds.center;
                details._beacon = root.gameObject.AddComponent<Light>();
                details._beacon.type = LightType.Point; details._beacon.color = new Color(1,.48f,.08f);
                details._beacon.range = 2.8f; details._beacon.shadows = LightShadows.None;
            }
        }
        private static void Driver(Transform vehicle, Vector3 hipsAt)
        {
            var prefab = Resources.Load<GameObject>("Airside/Characters/chr_ramp_m_worker");
            if (prefab == null) return;
            var driver = Instantiate(prefab, vehicle);
            driver.name = "Service vehicle driver";
            if (driver.TryGetComponent<Animator>(out var animator)) animator.enabled = false;
            driver.transform.localScale = Vector3.one * (1.72f / AirsidePrototype.MeasureFigureHeight(driver));
            var clip = Resources.LoadAll<AnimationClip>("Airside/Characters/chr_ramp_m_worker")
                .FirstOrDefault(c => c.name.EndsWith("Idle_Neutral", StringComparison.Ordinal));
            if (clip != null) clip.SampleAnimation(driver, 0);
            var bones = driver.GetComponentsInChildren<Transform>(true);
            // Original seated pose on the shared rig; soles down, thighs forward, arms toward wheel.
            foreach (var bone in bones)
            {
                if (bone.name.StartsWith("UpperLeg.", StringComparison.Ordinal)) bone.localRotation *= Quaternion.Euler(-78,0,0);
                if (bone.name.StartsWith("LowerLeg.", StringComparison.Ordinal)) bone.localRotation *= Quaternion.Euler(82,0,0);
                if (bone.name.StartsWith("UpperArm.", StringComparison.Ordinal)) bone.localRotation *= Quaternion.Euler(-35,0,0);
            }
            driver.transform.localRotation = Quaternion.Euler(0,90,0); // Kit's forward is +X until FacingOffset is applied.
            var hips = bones.FirstOrDefault(t => t.name == "Hips");
            if (hips != null) driver.transform.position += hipsAt - hips.position;
        }
        public void Motion(float distance, float headingChange, float dt, double clock)
        {
            if (dt <= 0) return;
            if (_frontAxles == null)
            {
                var wheels = GetComponentsInChildren<Transform>(true).Where(t => t.name == "Service axle pivot"
                    && t.childCount > 0 && ((t.GetChild(0).name.EndsWith("wheel_fl", StringComparison.OrdinalIgnoreCase) || t.GetChild(0).name.EndsWith("wheel FL", StringComparison.OrdinalIgnoreCase))
                        || (t.GetChild(0).name.EndsWith("wheel_fr", StringComparison.OrdinalIgnoreCase) || t.GetChild(0).name.EndsWith("wheel FR", StringComparison.OrdinalIgnoreCase)))).ToArray();
                // A steering parent leaves axle spin independent of yaw.
                _frontAxles = new Transform[wheels.Length]; _frontRest = new Quaternion[wheels.Length];
                for (var i=0;i<wheels.Length;i++)
                {
                    var axle = wheels[i]; var steer = new GameObject("Service steering pivot").transform;
                    steer.SetParent(axle.parent, false); steer.localPosition = axle.localPosition;
                    steer.localRotation = Quaternion.identity; axle.SetParent(steer, true);
                    _frontAxles[i] = steer; _frontRest[i] = steer.localRotation;
                }
            }
            var target = distance > .001f ? Mathf.Clamp(Mathf.Atan(2.5f * headingChange * Mathf.Deg2Rad / distance) * Mathf.Rad2Deg,-28,28) : 0;
            _steering = Mathf.MoveTowards(_steering,target,dt*90);
            for(var i=0;i<_frontAxles.Length;i++) _frontAxles[i].localRotation = _frontRest[i] * Quaternion.AngleAxis(_steering, Vector3.up);
            if (_beacon != null) _beacon.intensity = distance > .001f ? .5f + 1.5f * Mathf.Max(0,Mathf.Sin((float)(clock*2*Math.PI*1.4))) : .25f;
        }
    }
}
