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
        public const string FarDetailKeyword = "_GROUND_FAR_DETAIL";
        public const float MacroScaleMetres = 240f;
        public const float MacroStrength = 0.08f;
        public const float FarBlendStartMetres = 120f;
        public const float FarBlendEndMetres = 900f;

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
            // The ground only receives shadows. Casting from its lowered outer lip draws a
            // rectangular line onto the surroundings beneath it in the overview camera.
            renderer.shadowCastingMode = ShadowCastingMode.Off;
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

            // Soft normals from neighbouring verts so the boundary lip reads as landform. Edge
            // verts use one-sided differences: leaving them straight up lit the outermost row
            // differently from its sloped neighbours and drew a faint line round the field.
            for (var zi = 0; zi < resZ; zi++)
            {
                for (var xi = 0; xi < resX; xi++)
                {
                    var i = zi * resX + xi;
                    var dx = verts[zi * resX + Mathf.Min(xi + 1, resX - 1)] - verts[zi * resX + Mathf.Max(xi - 1, 0)];
                    var dz = verts[Mathf.Min(zi + 1, resZ - 1) * resX + xi] - verts[Mathf.Max(zi - 1, 0) * resX + xi];
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

            var dry = LoadGroundMap(AirsideAdelaideGround.LayerDryGrass, GroundMap.Basecolor);
            var green = LoadGroundMap(AirsideAdelaideGround.LayerGreenGrass, GroundMap.Basecolor);
            var dirt = LoadGroundMap(AirsideAdelaideGround.LayerWornDirt, GroundMap.Basecolor);
            if (dry == null || green == null || dirt == null)
                return null;

            var material = new Material(shader) { name = "mat_adelaide_ground_v01", enableInstancing = true };
            material.SetTexture("_DryAlbedo", dry);
            material.SetTexture("_GreenAlbedo", green);
            material.SetTexture("_DirtAlbedo", dirt);

            var dryN = LoadGroundMap(AirsideAdelaideGround.LayerDryGrass, GroundMap.Normal);
            var greenN = LoadGroundMap(AirsideAdelaideGround.LayerGreenGrass, GroundMap.Normal);
            var dirtN = LoadGroundMap(AirsideAdelaideGround.LayerWornDirt, GroundMap.Normal);
            if (dryN != null) material.SetTexture("_DryNormal", dryN);
            if (greenN != null) material.SetTexture("_GreenNormal", greenN);
            if (dirtN != null) material.SetTexture("_DirtNormal", dirtN);

            var dryM = LoadGroundMap(AirsideAdelaideGround.LayerDryGrass, GroundMap.Mask);
            var greenM = LoadGroundMap(AirsideAdelaideGround.LayerGreenGrass, GroundMap.Mask);
            var dirtM = LoadGroundMap(AirsideAdelaideGround.LayerWornDirt, GroundMap.Mask);
            if (dryM != null) material.SetTexture("_DryMask", dryM);
            if (greenM != null) material.SetTexture("_GreenMask", greenM);
            if (dirtM != null) material.SetTexture("_DirtMask", dirtM);

            material.SetFloat("_DryTile", AirsideAdelaideGround.TileSize(AirsideAdelaideGround.LayerDryGrass));
            material.SetFloat("_GreenTile", AirsideAdelaideGround.TileSize(AirsideAdelaideGround.LayerGreenGrass));
            material.SetFloat("_DirtTile", AirsideAdelaideGround.TileSize(AirsideAdelaideGround.LayerWornDirt));
            material.SetFloat("_BumpScale", 0.42f);
            // Multiplies each source mask's restrained smoothness (roughly 0.27–0.45).
            material.SetFloat("_Smoothness", 0.28f);
            // Albedo multiplier under a ~2.0 daytime sun; matches the URP Lit fallback's brightness.
            material.SetColor("_Tint", new Color(0.59f, 0.61f, 0.55f, 1f));
            material.SetFloat("_MacroScale", MacroScaleMetres);
            material.SetFloat("_MacroStrength", MacroStrength);
            material.SetFloat("_FarBlendStart", FarBlendStartMetres);
            material.SetFloat("_FarBlendEnd", FarBlendEndMetres);
            // The far-detail samples cost six texture reads; Medium keeps the single scale.
            if (AirsideRuntimeQuality.Current == AirsideRuntimeQuality.Ladder.High)
                material.EnableKeyword(FarDetailKeyword);
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

        private enum GroundMap { Basecolor, Normal, Mask }

        private static Texture2D LoadGroundMap(int layer, GroundMap map)
        {
            var path = map switch
            {
                GroundMap.Normal => AirsideAdelaideGround.LayerNormalPath(layer),
                GroundMap.Mask => AirsideAdelaideGround.LayerMaskPath(layer),
                _ => AirsideAdelaideGround.LayerBasecolorPath(layer)
            };
            return AirsideArtTextures.Load(path, linear: map != GroundMap.Basecolor);
        }
    }
}
