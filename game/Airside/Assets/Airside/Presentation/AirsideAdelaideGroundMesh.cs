using UnityEngine;
using UnityEngine.Rendering;

namespace Airside.Presentation
{
    /// <summary>
    /// Builds the Adelaide bare-field ground mesh from <see cref="AirsideAdelaideGround"/>.
    /// Startup work is one grid allocate + three texture loads — no runtime heightmap
    /// or splatmap generation. Falls back to a tinted grass slab if the blend shader
    /// or CC0 maps are missing.
    /// </summary>
    public static class AirsideAdelaideGroundMesh
    {
        public const string ShaderName = "Airside/AdelaideGround";
        public const string ObjectName = AirsideBareField.GroundObjectName;

        public static bool TryBuild(Transform root)
        {
            var resX = AirsideRuntimeQuality.Current == AirsideRuntimeQuality.Ladder.High
                ? AirsideAdelaideGround.HighResolutionX
                : AirsideAdelaideGround.MediumResolutionX;
            var resZ = AirsideRuntimeQuality.Current == AirsideRuntimeQuality.Ladder.High
                ? AirsideAdelaideGround.HighResolutionZ
                : AirsideAdelaideGround.MediumResolutionZ;

            var mesh = BuildMesh(resX, resZ);
            if (mesh == null)
                return false;

            var go = new GameObject(ObjectName);
            go.transform.SetParent(root, false);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;

            var filter = go.AddComponent<MeshFilter>();
            filter.sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = BuildMaterial()
                ?? BuildFallbackMaterial();
            renderer.shadowCastingMode = ShadowCastingMode.On;
            renderer.receiveShadows = true;
            return true;
        }

        public static Mesh BuildMesh(int resX, int resZ)
        {
            if (resX < 2 || resZ < 2)
                return null;

            var verts = new Vector3[resX * resZ];
            var norms = new Vector3[resX * resZ];
            var uvs = new Vector2[resX * resZ];
            var colors = new Color[resX * resZ];
            var weights = new float[AirsideAdelaideGround.LayerCount];

            for (var zi = 0; zi < resZ; zi++)
            {
                var tz = zi / (resZ - 1f);
                var wz = AirsideAdelaideGround.OriginZ + AirsideAdelaideGround.SizeZ * tz;
                for (var xi = 0; xi < resX; xi++)
                {
                    var tx = xi / (resX - 1f);
                    var wx = AirsideAdelaideGround.OriginX + AirsideAdelaideGround.SizeX * tx;
                    var i = zi * resX + xi;
                    var y = AirsideAdelaideGround.WorldHeight(wx, wz);
                    verts[i] = new Vector3(wx, y, wz);
                    // World-metre UVs so overview sampling is not stretched by the slab.
                    uvs[i] = new Vector2(wx, wz);
                    AirsideAdelaideGround.LayerWeights(wx, wz, weights);
                    colors[i] = new Color(
                        weights[AirsideAdelaideGround.LayerDryGrass],
                        weights[AirsideAdelaideGround.LayerGreenGrass],
                        weights[AirsideAdelaideGround.LayerWornDirt],
                        1f);
                    norms[i] = Vector3.up;
                }
            }

            // Soft normals from neighbouring verts so the boundary lip reads as landform.
            for (var zi = 1; zi < resZ - 1; zi++)
            {
                for (var xi = 1; xi < resX - 1; xi++)
                {
                    var i = zi * resX + xi;
                    var dx = verts[i + 1] - verts[i - 1];
                    var dz = verts[i + resX] - verts[i - resX];
                    norms[i] = Vector3.Cross(dz, dx).normalized;
                }
            }

            var triCount = (resX - 1) * (resZ - 1) * 6;
            var tris = new int[triCount];
            var t = 0;
            for (var zi = 0; zi < resZ - 1; zi++)
            {
                for (var xi = 0; xi < resX - 1; xi++)
                {
                    var i = zi * resX + xi;
                    tris[t++] = i;
                    tris[t++] = i + resX;
                    tris[t++] = i + 1;
                    tris[t++] = i + 1;
                    tris[t++] = i + resX;
                    tris[t++] = i + resX + 1;
                }
            }

            var mesh = new Mesh
            {
                name = "Adelaide ground",
                indexFormat = verts.Length > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16
            };
            mesh.vertices = verts;
            mesh.normals = norms;
            mesh.uv = uvs;
            mesh.colors = colors;
            mesh.triangles = tris;
            mesh.RecalculateBounds();
            AirsideMeshUtil.UploadStatic(mesh);
            return mesh;
        }

        public static Material BuildMaterial()
        {
            var shader = Shader.Find(ShaderName);
            if (shader == null)
                return null;

            var dry = LoadGroundMap(AirsideAdelaideGround.LayerDryGrass, normal: false);
            var green = LoadGroundMap(AirsideAdelaideGround.LayerGreenGrass, normal: false);
            var dirt = LoadGroundMap(AirsideAdelaideGround.LayerWornDirt, normal: false);
            if (dry == null || green == null || dirt == null)
                return null;

            var material = new Material(shader) { name = "mat_adelaide_ground_v01", enableInstancing = true };
            material.SetTexture("_DryAlbedo", dry);
            material.SetTexture("_GreenAlbedo", green);
            material.SetTexture("_DirtAlbedo", dirt);

            var dryN = LoadGroundMap(AirsideAdelaideGround.LayerDryGrass, normal: true);
            var greenN = LoadGroundMap(AirsideAdelaideGround.LayerGreenGrass, normal: true);
            var dirtN = LoadGroundMap(AirsideAdelaideGround.LayerWornDirt, normal: true);
            if (dryN != null) material.SetTexture("_DryNormal", dryN);
            if (greenN != null) material.SetTexture("_GreenNormal", greenN);
            if (dirtN != null) material.SetTexture("_DirtNormal", dirtN);

            material.SetFloat("_DryTile", AirsideAdelaideGround.TileSize(AirsideAdelaideGround.LayerDryGrass));
            material.SetFloat("_GreenTile", AirsideAdelaideGround.TileSize(AirsideAdelaideGround.LayerGreenGrass));
            material.SetFloat("_DirtTile", AirsideAdelaideGround.TileSize(AirsideAdelaideGround.LayerWornDirt));
            material.SetFloat("_BumpScale", 0.55f);
            material.SetFloat("_Smoothness", 0.1f);
            material.SetColor("_Tint", new Color(0.92f, 0.94f, 0.88f, 1f));
            return material;
        }

        private static Material BuildFallbackMaterial()
        {
            var albedo = AirsideArtTextures.Load(
                "Textures/Surfaces/tx_grass_kingscote_basecolor_v02.png")
                ?? AirsideArtTextures.Load(
                    "Textures/Surfaces/tx_grass_kingscote_basecolor_v01.png");
            // Large irregular tile so overview does not show the old 16 m grid.
            return AirsideMaterialLibrary.CreateShared(
                new Color(0.55f, 0.58f, 0.42f, 1f),
                AirsideMaterialLibrary.SurfaceKind.Grass,
                albedo,
                new Vector2(
                    AirsideBareField.GroundLengthMetres / 47f,
                    AirsideBareField.GroundWidthMetres / 37f));
        }

        private static Texture2D LoadGroundMap(int layer, bool normal)
        {
            var path = normal
                ? AirsideAdelaideGround.LayerNormalPath(layer)
                : AirsideAdelaideGround.LayerBasecolorPath(layer);
            return AirsideArtTextures.Load(path, linear: normal);
        }
    }
}
