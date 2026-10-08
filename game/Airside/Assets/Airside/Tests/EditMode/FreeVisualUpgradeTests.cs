using System;
using System.Linq;
using System.Reflection;
using Airside.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEditor;

namespace Airside.Tests
{
    public sealed class FreeVisualUpgradeTests
    {
        [Test]
        public void A320DerivativeKeepsCabinOpeningsAndDoorGeometry()
        {
            const string old="Models/Aircraft/mdl_a320_200_v01.gltf";
            const string current="Models/Aircraft/mdl_a320_200_v02.gltf";
            foreach(var name in new[]{"fuselage","door_fwd","cargo_door","cabin_window_1","windscreen_l"})
            {
                Assert.That(ArtGltfLoader.TryGetSharedMesh(old,name,out var before),Is.True,name);
                Assert.That(ArtGltfLoader.TryGetSharedMesh(current,name,out var after),Is.True,name);
                Assert.That(after.vertices,Is.EqualTo(before.vertices),name+" position");
                Assert.That(after.triangles,Is.EqualTo(before.triangles),name+" apertures");
            }
            Assert.That(ArtGltfLoader.TryGetSharedMesh(current,"engine_left",out var imported),Is.True);
            Assert.That(imported.triangles.Length/3,Is.GreaterThan(250),"open source casing");
        }

        [Test]
        public void FacadeFittingsAvoidEntranceIntervals()
        {
            var set=new BuildingDetailSet();
            set.Openings.Add(new DetailOpening(0,0,40,0,5));
            BuildingFacadeModules.Add(set,new[]{0f,0f,40f,0f,40f,12f,0f,12f},0,7);
            Assert.That(set.Boxes.Any(x=>x.Part==BuildingPart.Trim && x.Height>6f),Is.True);
            Assert.That(set.Boxes.Where(x=>x.Part==BuildingPart.Trim && x.Height>6f).All(x=>Math.Abs(x.Z)>.2f),Is.True,
                "No pipe runs across the front entrance");
        }

        [Test]
        public void ConveyorMotionFreezesWithSimTimeAndKeepsPhysicalSpeed()
        {
            var rig=new GameObject("Test treads");
            try
            {
                var tread=new GameObject("tread").transform;tread.SetParent(rig.transform,false);
                var pose=typeof(AirsidePrototype).GetMethod("PoseBeltTreads",BindingFlags.NonPublic|BindingFlags.Static);
                pose.Invoke(null,new object[]{new[]{tread},2.0,4f});var paused=tread.localPosition;
                pose.Invoke(null,new object[]{new[]{tread},2.0,4f});Assert.That(tread.localPosition,Is.EqualTo(paused));
                pose.Invoke(null,new object[]{new[]{tread},2.1,4f});var shortAdvance=(tread.localPosition.z-paused.z)*4;
                pose.Invoke(null,new object[]{new[]{tread},2.0,8f});var longStart=tread.localPosition.z;
                pose.Invoke(null,new object[]{new[]{tread},2.1,8f});var longAdvance=(tread.localPosition.z-longStart)*8;
                Assert.That(longAdvance,Is.EqualTo(shortAdvance).Within(.00001f));
                Assert.That(shortAdvance,Is.GreaterThan(0));
            }
            finally {UnityEngine.Object.DestroyImmediate(rig);}
        }

        [Test]
        public void RuntimePavementShadersCompileAndOpaqueMaterialsUseScans()
        {
            foreach(var name in new[]{"Airside/Pavement","Airside/SurfaceStain","Airside/AdelaideGround"})
            {
                var shader=Shader.Find(name);Assert.That(shader,Is.Not.Null,name);
                Assert.That(ShaderUtil.ShaderHasError(shader),Is.False,name);
            }
            var material=AirsideMaterialLibrary.Create(Color.gray,AirsideMaterialLibrary.SurfaceKind.Concrete);
            try
            {
                Assert.That(material.shader.name,Is.EqualTo("Airside/Pavement"));
                Assert.That(material.GetTexture("_BaseMap"),Is.Not.Null);
                Assert.That(material.GetTexture("_BumpMap"),Is.Not.Null);
                Assert.That(material.GetFloat("_TileMetres"),Is.EqualTo(4));
            }
            finally {UnityEngine.Object.DestroyImmediate(material);}
        }
    }
}
