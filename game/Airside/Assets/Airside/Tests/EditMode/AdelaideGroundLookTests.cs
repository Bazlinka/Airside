using Airside.Presentation;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Airside.Tests
{
    /// <summary>Ground mesh lighting continuity and the ground shader compiling with its far-detail variant.</summary>
    public sealed class AdelaideGroundLookTests
    {
        [Test]
        public void GroundShader_CompilesWithoutErrors()
        {
            var shader = Shader.Find(AirsideAdelaideGroundMesh.ShaderName);
            Assert.That(shader, Is.Not.Null);
            Assert.That(ShaderUtil.ShaderHasError(shader), Is.False, "AdelaideGround.shader has compile errors");
            var messages = ShaderUtil.GetShaderMessages(shader);
            foreach (var message in messages)
                Assert.That(message.severity, Is.Not.EqualTo(UnityEditor.Rendering.ShaderCompilerMessageSeverity.Error), message.message);
        }

        [Test]
        public void SurroundingsShader_CompilesAndCarriesTheGroundTextureAcrossTheEdge()
        {
            var shader = Shader.Find(AirsideAdelaideSurroundings.ShaderName);
            Assert.That(shader, Is.Not.Null);
            Assert.That(ShaderUtil.ShaderHasError(shader), Is.False, "Surroundings.shader has compile errors");
            var material = AirsideAdelaideSurroundings.BuildMaterial(shader);
            Assert.That(material, Is.Not.Null);
            Assert.That(material.GetTexture("_AirfieldAlbedo"), Is.Not.Null);
            Assert.That(material.GetTexture("_SatelliteAlbedo"), Is.Not.Null,
                "runway-aligned ESA Adelaide ground image is missing");
            Assert.That(material.GetFloat("_SatelliteExtent"),
                Is.EqualTo(AirsideAdelaideSurroundings.SatelliteExtentMetres));
            Assert.That(material.GetFloat("_SatelliteStrength"), Is.GreaterThan(0.85f));
            Assert.That(material.GetFloat("_EdgeTextureBlend"), Is.EqualTo(AirsideAdelaideSurroundings.EdgeTextureBlendMetres));
            Object.DestroyImmediate(material);
        }

        [Test]
        public void GroundMesh_EdgeNormalsFollowTheSlopeLikeTheirNeighbours()
        {
            const int resX = 65, resZ = 45;
            var mesh = AirsideAdelaideGroundMesh.BuildMesh(resX, resZ);
            var normals = mesh.normals;
            var worst = 0f;
            for (var zi = 0; zi < resZ; zi++)
            for (var xi = 0; xi < resX; xi++)
            {
                if (xi != 0 && xi != resX - 1 && zi != 0 && zi != resZ - 1)
                    continue;
                var inwardX = Mathf.Clamp(xi, 1, resX - 2);
                var inwardZ = Mathf.Clamp(zi, 1, resZ - 2);
                var edge = normals[zi * resX + xi];
                var inner = normals[inwardZ * resX + inwardX];
                Assert.That(edge.y, Is.GreaterThan(0f), "edge normal points down");
                worst = Mathf.Max(worst, Vector3.Angle(edge, inner));
            }

            Assert.That(worst, Is.LessThan(6f), "edge row lit differently from the row inside it");
            Object.DestroyImmediate(mesh);
        }

        [Test]
        public void GroundMaterial_EnablesFarDetailOnlyOnHighQuality()
        {
            var material = AirsideAdelaideGroundMesh.BuildMaterial();
            if (material == null)
                Assert.Ignore("Ground shader or CC0 maps missing in this checkout");
            var high = AirsideRuntimeQuality.Current == AirsideRuntimeQuality.Ladder.High;
            Assert.That(material.IsKeywordEnabled(AirsideAdelaideGroundMesh.FarDetailKeyword), Is.EqualTo(high));
            Assert.That(material.GetFloat("_MacroStrength"), Is.EqualTo(AirsideAdelaideGroundMesh.MacroStrength));
            Assert.That(material.GetTexture("_DryMask"), Is.Not.Null);
            Assert.That(material.GetTexture("_GreenMask"), Is.Not.Null);
            Assert.That(material.GetTexture("_DirtMask"), Is.Not.Null);
            Assert.That(material.GetTexture("_SatelliteAlbedo"), Is.Not.Null);
            Assert.That(material.GetFloat("_SatelliteEdgeBlend"), Is.GreaterThan(500f));
            Object.DestroyImmediate(material);
        }

        [Test]
        public void GroundMesh_HighQualityBlendGridIsFineEnoughForPavementShoulders()
        {
            var spacingX = AirsideAdelaideGround.SizeX / (AirsideAdelaideGround.HighResolutionX - 1f);
            var spacingZ = AirsideAdelaideGround.SizeZ / (AirsideAdelaideGround.HighResolutionZ - 1f);
            Assert.That(spacingX, Is.LessThan(18f));
            Assert.That(spacingZ, Is.LessThan(19f));
        }
    }
}
