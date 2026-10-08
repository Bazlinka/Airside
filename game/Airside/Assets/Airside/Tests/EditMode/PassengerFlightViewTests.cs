using System.Linq;
using System.Reflection;
using Airside.Domain;
using Airside.Presentation;
using NUnit.Framework;
using UnityEngine;
using Object=UnityEngine.Object;

namespace Airside.Tests
{
    public sealed class PassengerFlightViewTests
    {
        private static void FinishEntry(AirsideCameraController controller)
        {
            var step = typeof(AirsideCameraController).GetMethod("AdvanceFlightViewTransition",
                BindingFlags.Instance | BindingFlags.NonPublic);
            for (var i = 0; i < 12; i++) step.Invoke(controller, new object[] { 0.1f });
        }

        [TestCase("SF34")][TestCase("ATR42")][TestCase("DH8D")]
        [TestCase("B738")][TestCase("B38M")][TestCase("A320")][TestCase("A21N")]
        [TestCase("E190")][TestCase("A223")][TestCase("A359")][TestCase("A339")][TestCase("B789")][TestCase("B78X")]
        public void BothWindowSeatsHaveOpenSightlinesAndRestoreExterior(string id)
        {
            AircraftType.TryFromId(id,out var type);
            var root=new GameObject("Aircraft");
            var hull=GameObject.CreatePrimitive(PrimitiveType.Cube);hull.name="Fuselage";hull.transform.SetParent(root.transform,false);
            var wing=GameObject.CreatePrimitive(PrimitiveType.Cube);wing.name="Wing Left";wing.transform.SetParent(root.transform,false);
            try
            {
                var rig=PassengerCabinInterior.Build(root.transform,type,false);
                Assert.That(rig.GetComponentsInChildren<Collider>(),Is.Empty);
                var renderers = rig.GetComponentsInChildren<MeshRenderer>();
                Assert.That(renderers.Length, Is.LessThanOrEqualTo(8), "Refined cabin surfaces remain bounded");
                Assert.That(renderers.Select(r => r.sharedMaterial).Distinct().Count(), Is.EqualTo(renderers.Length),
                    "Cabin must batch each material into one draw");
                foreach(var filter in rig.GetComponentsInChildren<MeshFilter>())
                    filter.gameObject.AddComponent<MeshCollider>().sharedMesh=filter.sharedMesh;
                rig.Enter();
                Assert.That(hull.GetComponent<Renderer>().forceRenderingOff,Is.True);
                Assert.That(wing.GetComponent<Renderer>().forceRenderingOff,Is.False);
                foreach(var right in new[]{false,true})
                {
                    rig.SelectSide(right);Physics.SyncTransforms();
                    // Test the generated interior's actual geometry, not a copied aperture formula.
                    var ray=new Ray(rig.Seat.position,rig.Seat.forward);
                    var blocked=Physics.RaycastAll(ray,1f).Any(h=>h.transform.IsChildOf(rig.transform));
                    Assert.That(blocked,Is.False,id+" window blocked on "+right);
                    Assert.That(Mathf.Abs(rig.Seat.localPosition.x),Is.LessThan(rig.Profile.HalfWidth));
                }
                rig.Leave();Assert.That(hull.GetComponent<Renderer>().forceRenderingOff,Is.False);
                rig.Enter();Object.DestroyImmediate(rig.gameObject);
                Assert.That(hull.GetComponent<Renderer>().forceRenderingOff,Is.False);
            }
            finally {Object.DestroyImmediate(root);}
        }
        [Test] public void ExteriorIgnoresInteriorMotionAndKeepsItsOwnOptics()
        {
            var host = new GameObject("Exterior camera");
            var aircraft = new GameObject("Watched aircraft");
            try
            {
                var camera = host.AddComponent<Camera>();
                var controller = host.AddComponent<AirsideCameraController>();
                typeof(AirsideCameraController).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(controller, null);
                aircraft.transform.SetPositionAndRotation(new Vector3(30f, 800f, 50f), Quaternion.Euler(-10f, 120f, 8f));
                Assert.That(controller.StartFlightExterior(aircraft.transform, new Vector3(0f, 2f, -10f), 38f), Is.True);
                FinishEntry(controller);
                typeof(AirsideCameraController).GetMethod("ApplyCockpitPose", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(controller, null);
                var position = host.transform.position;
                var rotation = host.transform.rotation;
                controller.SetCockpitMotion(new Vector3(0.1f, -0.1f, 0.05f), new Vector3(3f, 4f, 2f));
                typeof(AirsideCameraController).GetMethod("ApplyCockpitPose", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(controller, null);
                Assert.That(Vector3.Distance(host.transform.position, position), Is.LessThan(0.001f));
                Assert.That(Quaternion.Angle(host.transform.rotation, rotation), Is.LessThan(0.001f));
                controller.RecenterCockpit();
                typeof(AirsideCameraController).GetMethod("ApplyCockpitPose", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(controller, null);
                Assert.That(Vector3.Distance(host.transform.position, position), Is.LessThan(0.001f));
                Assert.That(camera.fieldOfView, Is.EqualTo(48f).Within(0.001f));
            }
            finally { Object.DestroyImmediate(host); Object.DestroyImmediate(aircraft); }
        }
        [Test]
        public void OriginShiftMovesEntryAndExitGlideWithTheTarget()
        {
            var host = new GameObject("Camera");
            var aircraft = new GameObject("Aircraft");
            try
            {
                host.AddComponent<Camera>();
                var controller = host.AddComponent<AirsideCameraController>();
                var flags = BindingFlags.Instance | BindingFlags.NonPublic;
                typeof(AirsideCameraController).GetMethod("Awake", flags).Invoke(controller, null);
                controller.StartFlightExterior(aircraft.transform, new Vector3(0, 2, 0), 38);
                typeof(AirsideCameraController).GetMethod("AdvanceFlightViewTransition", flags)
                    .Invoke(controller, new object[] { 0.1f });
                var apply = typeof(AirsideCameraController).GetMethod("ApplyCockpitPose", flags);
                apply.Invoke(controller, null);
                var prior = host.transform.position;
                var delta = new Vector3(-8000, 0, -8000);
                aircraft.transform.position += delta;
                controller.ShiftFlightOrigin(delta);
                apply.Invoke(controller, null);
                Assert.That(Vector3.Distance(host.transform.position, prior + delta), Is.LessThan(.01f));
                controller.EndCockpit();
                var exitFrom = host.transform.position;
                controller.ShiftFlightOrigin(-delta);
                host.transform.position = Vector3.zero; // newly resolved overview pose
                typeof(AirsideCameraController).GetField("_exitBlendFrame", flags)
                    .SetValue(controller, Time.frameCount); // entry of exit glide, no editor-clock step
                typeof(AirsideCameraController).GetMethod("ApplyExitBlend", flags).Invoke(controller, null);
                Assert.That(Vector3.Distance(host.transform.position, exitFrom - delta), Is.LessThan(.01f));
            }
            finally { Object.DestroyImmediate(host); Object.DestroyImmediate(aircraft); }
        }

        [Test] public void ExteriorTracksAircraftAndSurvivesOriginShiftThenRestoresOptics()
        {
            var host=new GameObject("Camera");var aircraft=new GameObject("Aircraft");
            try
            {
                var camera=host.AddComponent<Camera>();var controller=host.AddComponent<AirsideCameraController>();
                typeof(AirsideCameraController).GetMethod("Awake",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(controller,null);
                camera.nearClipPlane=.6f;camera.farClipPlane=24000;camera.fieldOfView=48;
                controller.KeyboardCaptured=true;
                Assert.That(controller.StartFlightExterior(aircraft.transform,new Vector3(0,2,-10),38),Is.True);
                aircraft.transform.SetPositionAndRotation(new Vector3(1200,2000,400),Quaternion.Euler(-8,130,15));
                var late=typeof(AirsideCameraController).GetMethod("LateUpdate",BindingFlags.Instance|BindingFlags.NonPublic);
                FinishEntry(controller);late.Invoke(controller,null);var prior=host.transform.position;
                aircraft.transform.position-=new Vector3(8000,0,8000);late.Invoke(controller,null);
                Assert.That(Vector3.Distance(host.transform.position,prior-new Vector3(8000,0,8000)),Is.LessThan(.01));
                Assert.That(camera.farClipPlane,Is.GreaterThanOrEqualTo(55000));
                var seat=new GameObject("Passenger eye").transform;seat.SetParent(aircraft.transform,false);
                Assert.That(controller.StartPassenger(seat),Is.True);Assert.That(controller.IsFlightExterior,Is.False);
                FinishEntry(controller);late.Invoke(controller,null);
                Assert.That(Vector3.Distance(host.transform.position,seat.position),Is.LessThan(.001));
                Assert.That(AirsideCameraController.CurrentDistance,Is.EqualTo(0));
                Assert.That(controller.StartCockpit(seat),Is.True);
                controller.EndCockpit();Assert.That(camera.nearClipPlane,Is.EqualTo(.6f).Within(.001));
                Assert.That(camera.farClipPlane,Is.EqualTo(24000));Assert.That(camera.fieldOfView,Is.EqualTo(48));
            }
            finally{Object.DestroyImmediate(host);Object.DestroyImmediate(aircraft);}
        }
    }
}
