using Airside.Presentation;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Airside.Tests
{
    /// <summary>
    /// The legacy full-airport terrain calibration (diagnostic -airsideFullAirport only): layer
    /// copies are tinted and matte enough for the noon rig, the baked assets stay untouched, and
    /// the release bare field never builds the terrain.
    /// </summary>
    public sealed class AirsideTerrainGroundCalibrationTests
    {
        private static readonly string[] LayerPaths =
        {
            "Assets/Airside/Art/Terrain/trn_ground_drygrass_v01.terrainlayer",
            "Assets/Airside/Art/Terrain/trn_ground_greengrass_v01.terrainlayer",
            "Assets/Airside/Art/Terrain/trn_ground_worndirt_v01.terrainlayer",
            "Assets/Airside/Art/Terrain/trn_ground_coastsand_v01.terrainlayer"
        };

        [Test]
        public void CalibratedLayer_ScalesAlbedoAndCapsMaskSmoothness_WithoutTouchingTheAsset()
        {
            foreach (var path in LayerPaths)
            {
                var source = AssetDatabase.LoadAssetAtPath<TerrainLayer>(path);
                Assert.That(source, Is.Not.Null, path);
                var sourceAlbedo = source.diffuseRemapMax;
                var sourceMask = source.maskMapRemapMax;

                var copy = AirsideTerrainGround.CalibratedLayer(source);
                try
                {
                    Assert.That(copy, Is.Not.SameAs(source));
                    Assert.That(copy.diffuseTexture, Is.SameAs(source.diffuseTexture));
                    Assert.That(copy.normalMapTexture, Is.SameAs(source.normalMapTexture));
                    Assert.That(copy.maskMapTexture, Is.SameAs(source.maskMapTexture));
                    Assert.That(copy.diffuseRemapMax.x, Is.EqualTo(sourceAlbedo.x * AirsideTerrainGround.LayerAlbedoScale.x).Within(1e-5f));
                    Assert.That(copy.diffuseRemapMax.y, Is.EqualTo(sourceAlbedo.y * AirsideTerrainGround.LayerAlbedoScale.y).Within(1e-5f));
                    Assert.That(copy.diffuseRemapMax.z, Is.EqualTo(sourceAlbedo.z * AirsideTerrainGround.LayerAlbedoScale.z).Within(1e-5f));
                    // Mask alpha is URP smoothness: it may never exceed what the layer was authored at.
                    Assert.That(copy.maskMapRemapMax.w, Is.LessThanOrEqualTo(source.smoothness + 1e-5f), path);
                    Assert.That(copy.maskMapRemapMax.w, Is.LessThanOrEqualTo(0.2f), "grass and dirt stay matte");

                    Assert.That(source.diffuseRemapMax, Is.EqualTo(sourceAlbedo), "asset albedo untouched");
                    Assert.That(source.maskMapRemapMax, Is.EqualTo(sourceMask), "asset mask remap untouched");
                }
                finally
                {
                    Object.DestroyImmediate(copy);
                }
            }
        }

        [Test]
        public void LayerAlbedoScale_DarkensEveryChannel()
        {
            var scale = AirsideTerrainGround.LayerAlbedoScale;
            foreach (var channel in new[] { scale.x, scale.y, scale.z })
                Assert.That(channel, Is.InRange(0.3f, 0.9f));
            Assert.That(scale.z, Is.LessThan(scale.x), "warmer than neutral: URP Lit reads cool under the noon rig");
        }

        [Test]
        public void Calibrate_GivesTheTerrainItsOwnDataAndLeavesTheBakedAssetAlone()
        {
            var prefab = Resources.Load<GameObject>($"{ArtPresentationLoader.ResourcesPrefabRoot}/{AirsideTerrainGround.PrefabKey}");
            if (prefab == null)
                Assert.Ignore("Terrain has not been baked in this checkout.");
            var sourceData = prefab.GetComponent<Terrain>().terrainData;
            var sourceLayers = sourceData.terrainLayers;

            var go = Object.Instantiate(prefab);
            try
            {
                var terrain = go.GetComponent<Terrain>();
                AirsideTerrainGround.Calibrate(terrain);

                Assert.That(terrain.terrainData, Is.Not.SameAs(sourceData));
                Assert.That(terrain.terrainData.terrainLayers.Length, Is.EqualTo(sourceLayers.Length));
                for (var i = 0; i < sourceLayers.Length; i++)
                {
                    Assert.That(terrain.terrainData.terrainLayers[i], Is.Not.SameAs(sourceLayers[i]));
                    Assert.That(sourceData.terrainLayers[i], Is.SameAs(sourceLayers[i]), "baked asset still points at its own layers");
                }
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void TryBuild_DoesNothingInTheReleaseBareField()
        {
            Assume.That(AirsideBareField.Enabled, Is.True, "EditMode runs without -airsideFullAirport");
            var root = new GameObject("Terrain test root").transform;
            try
            {
                Assert.That(AirsideTerrainGround.TryBuild(root), Is.False);
                Assert.That(root.childCount, Is.EqualTo(0));
                Assert.That(AirsideTerrainGround.Active, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(root.gameObject);
            }
        }
    }
}
