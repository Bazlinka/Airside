using System.Linq;
using Airside.Domain;
using Airside.Presentation;
using Airside.Simulation;
using NUnit.Framework;
using UnityEngine;

namespace Airside.Tests
{
    public sealed class ParafieldAirportPresentationTests
    {
        [Test]
        public void RealTrainerKitLoadsAndAirportStaysOutsideStaticBatching()
        {
            var parent=new GameObject("Parafield test world");
            try
            {
                var clock=new ManualSimulationClock(new SimulationTime(0));
                var airport=AirsideParafieldAirport.Create(parent.transform,clock);
                airport.Tick(0,true,0);
                Assert.That(AirsideStaticWorld.IsDynamic(airport.gameObject),Is.True);
                var planes=airport.GetComponentsInChildren<Transform>().Where(t=>t.name.StartsWith("Parafield trainer ") && t.parent==airport.transform).ToArray();
                Assert.That(planes.Length,Is.EqualTo(4));
                foreach(var plane in planes)
                {
                    Assert.That(plane.GetComponentsInChildren<MeshRenderer>().Length,Is.GreaterThanOrEqualTo(31),"real kit, not fallback");
                    Assert.That(plane.GetComponentsInChildren<Transform>().Any(t=>t.name.Contains("wing_strut_left")),Is.True);
                }
                Assert.That(airport.transform.position.x,Is.EqualTo(ParafieldLayout.CentreX).Within(.01));
                clock.Set(new SimulationTime(20));airport.Tick(20,true,1);
                Assert.That(airport.GetComponentsInChildren<Light>().All(l=>l.enabled),Is.True);
                airport.Tick(20,false,1);Assert.That(airport.gameObject.activeSelf,Is.False);
                airport.Tick(20,true,0);Assert.That(airport.gameObject.activeSelf,Is.True);
            }
            finally { Object.DestroyImmediate(parent); }
        }

        [Test]
        public void WatchCameraKeepsLocalPanningAndReturnsToAdelaideOverview()
        {
            var go=new GameObject("Parafield camera test");go.AddComponent<Camera>();
            try
            {
                var camera=go.AddComponent<AirsideCameraController>();
                var x=(float)ParafieldLayout.CentreX;var z=(float)ParafieldLayout.CentreZ;
                camera.WatchAirport(new Vector3(x,12,z),2800,52,140);
                camera.CentreOn(x+50,z+50);
                Assert.That(camera.WatchingIndependentAirport,Is.True);
                Assert.That(camera.FocusPoint.x,Is.EqualTo(x+50).Within(.01));
                camera.ReturnToOverview();
                Assert.That(camera.WatchingIndependentAirport,Is.False);
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void OperationsWatchButtonFitsBesideCloseAtDesktopWidths()
        {
            foreach(var width in new[] {540f,800f,1100f})
            {
                var list=new HudDrawList();var layout=OperationsWorkspaceLayout.Create(new HudBox(20,20,width,760),0,airlineView:true);
                OperationsWorkspacePainter.Paint(list,new OperationsWorkspaceModel(),layout,null,0);
                var watch=list.Commands.Single(c=>c.ActionId==HudAction.WatchParafield);
                var close=list.Commands.Single(c=>c.ActionId==HudAction.Close);
                Assert.That(watch.Box.Right,Is.LessThan(close.Box.X));
                Assert.That(watch.Box.X,Is.GreaterThan(layout.Surface.X));
            }
        }
    }
}
