using System;
using System.Collections.Generic;
using System.IO;
using Airside.Simulation;
using UnityEngine;
using UnityEngine.Rendering;

namespace Airside.Presentation
{
    /// <summary>49 small terrain tiles around the spectator, one build per frame, no network at runtime.</summary>
    public sealed class AirsideFlightWorldTerrain : MonoBehaviour
    {
        private readonly Dictionary<(int x, int z), GameObject> _tiles = new();
        private readonly List<(int x, int z)> _drop = new();
        private readonly List<(RegionalRunway runway,Transform view)> _runways = new();
        private Material _material;
        private FlightWorldHeights _heights;
        private FlightWorldLandCover _cover;
        private readonly float[] _tint = new float[3];
        private float[] _seaLinear;
        private double _originX, _originZ;
        public int ResidentTiles => _tiles.Count;
        public bool HasElevation => _heights != null;
        public static AirsideFlightWorldTerrain Create()
        {
            var result = new GameObject("South Australia flight terrain").AddComponent<AirsideFlightWorldTerrain>();
            var path = ArtRuntimePaths.ResolveExisting(FlightWorldHeights.ArtPath);
            if (path != null) result._heights = FlightWorldHeights.Parse(File.ReadAllBytes(path));
            // State-wide land cover (ADR 0250). Without the file the tiles keep their height-only colouring.
            var coverPath = ArtRuntimePaths.ResolveExisting(FlightWorldLandCover.ArtPath);
            if (coverPath != null) result._cover = FlightWorldLandCover.Parse(File.ReadAllBytes(coverPath));
            var sea = AirsideAdelaideSurroundings.DeepWater.linear;
            result._seaLinear = new[] { sea.r, sea.g, sea.b };
            // Same vertex-colour shader as the existing Adelaide outer landscape.
            var shader = Shader.Find("Airside/Surroundings");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Lit");
            result._material = new Material(shader) { name = "mat_sa_flight_terrain" };
            result._material.SetFloat("_SatelliteNearStrength",0f);
            result._material.SetFloat("_SatelliteFarStrength",0f);
            result._material.SetFloat("_HorizonFadeStart",40000f);
            result._material.SetFloat("_HorizonFadeEnd",55000f);
            foreach(var runway in RegionalRunways.All)
            {
                var go=new GameObject("Regional runway "+runway.Code);go.transform.SetParent(result.transform,false);
                var length=(float)runway.Length;var width=(float)runway.Width;
                var mesh=new Mesh {name=go.name};
                mesh.vertices=new[]{new Vector3(-width/2,0,-length/2),new Vector3(width/2,0,-length/2),
                    new Vector3(-width/2,0,length/2),new Vector3(width/2,0,length/2)};
                mesh.colors=new[]{Color.gray.linear,Color.gray.linear,Color.gray.linear,Color.gray.linear};
                mesh.triangles=new[]{0,2,1,1,2,3};mesh.RecalculateNormals();mesh.RecalculateBounds();
                go.AddComponent<MeshFilter>().sharedMesh=mesh;
                var renderer=go.AddComponent<MeshRenderer>();renderer.sharedMaterial=result._material;
                renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
                go.transform.rotation=Quaternion.LookRotation(new Vector3((float)(runway.Bx-runway.Ax),0,(float)(runway.Bz-runway.Az)));
                result._runways.Add((runway,go.transform));
            }
            return result;
        }
        public void Tick(double worldX, double worldZ, double originX, double originZ)
        {
            _originX = originX; _originZ = originZ;
            foreach(var strip in _runways)
                strip.view.position=new Vector3((float)((strip.runway.Ax+strip.runway.Bx)/2-originX),
                    (float)(strip.runway.Elevation+AirsideFlightPath.GroundY+.02),(float)((strip.runway.Az+strip.runway.Bz)/2-originZ));
            var cx = FlightWorldGrid.Tile(worldX); var cz = FlightWorldGrid.Tile(worldZ);
            _drop.Clear();
            foreach (var pair in _tiles)
            {
                if (!FlightWorldGrid.Resident(pair.Key.x,pair.Key.z,cx,cz)) _drop.Add(pair.Key);
                else Position(pair.Key, pair.Value.transform);
            }
            foreach (var key in _drop)
            {
                var go = _tiles[key];
                Destroy(go.GetComponent<MeshFilter>().sharedMesh);
                Destroy(go); _tiles.Remove(key);
            }
            // Fill nearest first, bounded work. Existing tiles stay visible throughout travel.
            for (var ring = 0; ring <= FlightWorldGrid.RadiusTiles; ring++)
            for (var dz = -ring; dz <= ring; dz++)
            for (var dx = -ring; dx <= ring; dx++)
            {
                if (Math.Max(Math.Abs(dx),Math.Abs(dz)) != ring) continue;
                var key = (cx+dx,cz+dz);
                if (_tiles.ContainsKey(key)) continue;
                var go = Build(key.Item1,key.Item2);
                _tiles.Add(key,go); Position(key,go.transform);
                return;
            }
        }
        private void Position((int x,int z) key, Transform tile) => tile.position =
            new Vector3((float)(key.x*(double)FlightWorldGrid.TileMetres-_originX),0,
                        (float)(key.z*(double)FlightWorldGrid.TileMetres-_originZ));
        private GameObject Build(int tx, int tz)
        {
            var n = FlightWorldGrid.Cells+1;
            var vertices = new Vector3[n*n]; var colors = new Color[n*n];
            var triangles = new List<int>(FlightWorldGrid.Cells*FlightWorldGrid.Cells*6);
            var stride = FlightWorldGrid.TileMetres / FlightWorldGrid.Cells;
            for (var z=0; z<n; z++) for (var x=0; x<n; x++)
            {
                var wx=tx*(double)FlightWorldGrid.TileMetres+x*stride;
                var wz=tz*(double)FlightWorldGrid.TileMetres+z*stride;
                YpadFrame.ToLatLon(wx,wz,out var lat,out var lon);
                var land=MapGeography.OnLand(lon,lat);
                var height=0.0;
                if (land) _heights?.TryHeight(lat,lon,out height);
                var y = land ? AirsideFlightPath.GroundY + RegionalRunways.Ground(wx,wz,height-AdelaideTerrainHeights.PlainAboveSeaMetres) : -4;
                // The original detailed meshes own Adelaide. This landscape tucks under them.
                if (Math.Abs(wx)<=96000 && Math.Abs(wz)<=96000) y-=12;
                vertices[z*n+x]=new Vector3(x*stride,(float)y,z*stride);
                if (land && _cover != null && _cover.TryCell(lat,lon,out var cx,out var cz) && _cover.TryClass(lat,lon,out var cls))
                {
                    // Real land cover, in the palette the Adelaide rings use so the two meet without a step.
                    var alpha=AdelaideOuterTerrainGeometry.LandCoverColour(cls,cx,cz,0f,_seaLinear,_tint);
                    colors[z*n+x]=new Color(_tint[0],_tint[1],_tint[2],alpha);
                    continue;
                }
                var color=land ? Color.Lerp(new Color(.54f,.53f,.34f),new Color(.35f,.40f,.28f),(float)Math.Min(1,height/800))
                    : AirsideAdelaideSurroundings.DeepWater;
                colors[z*n+x]=color.linear;
                colors[z*n+x].a=land ? 0 : 1;
            }
            for(var z=0;z<n-1;z++) for(var x=0;x<n-1;x++)
            {
                var a=z*n+x; var b=a+1; var c=a+n; var d=c+1;
                var wx=tx*(double)FlightWorldGrid.TileMetres+(x+.5)*stride;
                var wz=tz*(double)FlightWorldGrid.TileMetres+(z+.5)*stride;
                if(Math.Abs(wx)<95000 && Math.Abs(wz)<95000) continue;
                triangles.Add(a);triangles.Add(c);triangles.Add(b);
                triangles.Add(b);triangles.Add(c);triangles.Add(d);
            }
            var mesh=new Mesh {name=$"SA terrain {tx},{tz}"};
            mesh.vertices=vertices;mesh.colors=colors;mesh.SetTriangles(triangles,0);
            mesh.RecalculateNormals();mesh.RecalculateBounds();
            var go=new GameObject(mesh.name);go.transform.SetParent(transform,false);
            go.AddComponent<MeshFilter>().sharedMesh=mesh;
            var renderer=go.AddComponent<MeshRenderer>();renderer.sharedMaterial=_material;
            renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
            return go;
        }
        private void OnDestroy()
        {
            foreach(var tile in _tiles.Values) if(tile!=null) Destroy(tile.GetComponent<MeshFilter>().sharedMesh);
            foreach(var strip in _runways) if(strip.view!=null) Destroy(strip.view.GetComponent<MeshFilter>().sharedMesh);
            if(_material!=null) Destroy(_material);
        }
    }
}
