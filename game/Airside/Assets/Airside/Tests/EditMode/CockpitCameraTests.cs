using System.Reflection;
using Airside.Presentation;
using NUnit.Framework;
using UnityEngine;

namespace Airside.Tests
{
    public sealed class CockpitCameraTests
    {
        [Test] public void CockpitTracksSeatAfterAircraftPoseAndRestoresCameraSettings()
        {
            var host = new GameObject("Test camera");
            var aircraft = new GameObject("Test aircraft");
            try
            {
                var camera = host.AddComponent<Camera>();
                var controller = host.AddComponent<AirsideCameraController>();
                // EditMode AddComponent does not invoke this runtime MonoBehaviour's Awake.
                typeof(AirsideCameraController).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(controller, null);
                controller.KeyboardCaptured = true;
                camera.nearClipPlane = 0.6f;
                camera.farClipPlane = 24000f;
                camera.fieldOfView = 48f;
                var seat = new GameObject("Seat").transform;
                seat.SetParent(aircraft.transform);
                seat.localPosition = new Vector3(-0.38f, 1.65f, 7.1f);
                Assert.That(controller.StartCockpit(seat), Is.True);
                aircraft.transform.SetPositionAndRotation(new Vector3(12f, 100f, 42f), Quaternion.Euler(-8f, 230f, 12f));
                typeof(AirsideCameraController).GetMethod("LateUpdate", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(controller, null);
                Assert.That(Vector3.Distance(host.transform.position, seat.position), Is.LessThan(0.001f));
                Assert.That(Quaternion.Angle(host.transform.rotation, seat.rotation), Is.LessThan(0.01f));
                Assert.That(camera.nearClipPlane, Is.EqualTo(0.035f).Within(0.001f));
                Assert.That(AirsideCameraController.CurrentDistance, Is.EqualTo(0f),
                    "Cockpit fog must not inherit a far overview's thinned weather");
                controller.EndCockpit();
                Assert.That(controller.IsCockpit, Is.False);
                Assert.That(camera.nearClipPlane, Is.EqualTo(0.6f).Within(0.001f));
                Assert.That(camera.farClipPlane, Is.EqualTo(24000f));
                Assert.That(camera.fieldOfView, Is.EqualTo(48f));
            }
            finally { Object.DestroyImmediate(aircraft); Object.DestroyImmediate(host); }
        }

        [Test] public void InteriorExitPreservesAlreadyHiddenGlazingAndKeepsWingVisible()
        {
            var aircraft = new GameObject("Aircraft");
            try
            {
                var hull = GameObject.CreatePrimitive(PrimitiveType.Cube);
                hull.name = "Fuselage"; hull.transform.SetParent(aircraft.transform);
                var glazing = GameObject.CreatePrimitive(PrimitiveType.Cube);
                glazing.name = "Cockpit"; glazing.transform.SetParent(aircraft.transform);
                glazing.GetComponent<Renderer>().forceRenderingOff = true;
                var wing = GameObject.CreatePrimitive(PrimitiveType.Cube);
                wing.name = "Wing L"; wing.transform.SetParent(aircraft.transform);
                var rig = SaabCockpitInterior.Build(aircraft.transform);
                rig.Enter();
                Assert.That(hull.GetComponent<Renderer>().forceRenderingOff, Is.True);
                Assert.That(wing.GetComponent<Renderer>().forceRenderingOff, Is.False);
                rig.Leave();
                Assert.That(hull.GetComponent<Renderer>().forceRenderingOff, Is.False);
                Assert.That(glazing.GetComponent<Renderer>().forceRenderingOff, Is.True);
            }
            finally { Object.DestroyImmediate(aircraft); }
        }
    }
}
