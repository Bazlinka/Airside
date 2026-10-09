using System;
using System.Collections.Generic;
using System.IO;
using Airside.Simulation;
using UnityEngine;
using UnityEngine.Rendering;

namespace Airside.Presentation
{
    /// <summary>
    /// Offline Australian terrain: 49 bounded near tiles, denser mapped airport approaches, cheap cruise cells,
    /// and an interleaved horizon ring. Only one terrain mesh is built per frame; floating origins stay double precision.
    /// </summary>
    public sealed class AirsideFlightWorldTerrain : MonoBehaviour
    {
        private readonly Dictionary<(int x, int z), GameObject> _tiles = new();
        private readonly Dictionary<(int x, int z), GameObject> _coarse = new();
        private readonly Dictionary<(int x, int z), int> _tileCells = new();
        private FlightWorldHeights _nationalHeights, _approachHeights;
        private FlightWorldLandCover _nationalCover, _approachCover;
        private RegionalRunway? _airport;
        private AirsideFlightAirportEnvironment _airportEnvironment;
        private bool _cruise;
        private int _streamFrame;
        private readonly List<(int x, int z)> _drop = new();
        private readonly List<(RegionalRunway runway,Transform view)> _runways = new();
        private Material _material, _runwayMaterial;
        private FlightWorldHeights _heights;
        private FlightWorldLandCover _cover;
        private readonly float[] _tint = new float[3];
        private float[] _seaLinear;
        private double _originX, _originZ;
        public int ResidentTiles => _tiles.Count;
        public int ResidentCoarseTiles => _coarse.Count;
        public bool HasElevation => _heights != null || _nationalHeights != null;
        public static AirsideFlightWorldTerrain Create()
        {
            var result = new GameObject("Australia flight terrain").AddComponent<AirsideFlightWorldTerrain>();
            result._heights = LoadHeights(FlightWorldHeights.ArtPath);
            // State-wide land cover (ADR 0250). Without the file the tiles keep their height-only colouring.
            result._cover = LoadCover(FlightWorldLandCover.ArtPath);
            result._nationalHeights = LoadHeights("Terrain/dem_australia_v01.bin");
            result._nationalCover = LoadCover("Terrain/landcover_australia_v01.bin");
            var sea = AirsideAdelaideSurroundings.DeepWater.linear;
            result._seaLinear = new[] { sea.r, sea.g, sea.b };
            // Same vertex-colour shader as the existing Adelaide outer landscape.
            var shader = Shader.Find("Airside/Surroundings");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Lit");
            result._material = new Material(shader) { name = "mat_australia_flight_terrain" };
            result._material.SetFloat("_SatelliteNearStrength",0f);
            result._material.SetFloat("_SatelliteFarStrength",0f);
            result._material.SetFloat("_HorizonFadeStart",CockpitFadeStartMetres);
            result._material.SetFloat("_HorizonFadeEnd",CockpitFadeEndMetres);
            result._runwayMaterial=new Material(result._material) {name="Australian runway paint"};
            result._runwayMaterial.SetFloat("_VertexSurface",1f);
            foreach(var runway in RegionalRunways.Strips)
            {
                var go=new GameObject("Regional runway "+runway.Code);go.transform.SetParent(result.transform,false);
                var mesh=BuildRunwayMesh(runway);
                go.AddComponent<MeshFilter>().sharedMesh=mesh;
                var renderer=go.AddComponent<MeshRenderer>();renderer.sharedMaterial=result._runwayMaterial;
                renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
                go.transform.rotation=Quaternion.LookRotation(new Vector3((float)(runway.Bx-runway.Ax),0,(float)(runway.Bz-runway.Az)));
                result._runways.Add((runway,go.transform));
            }
            return result;
        }
        private static Mesh BuildRunwayMesh(RegionalRunway runway)
        {
            var vertices=new List<Vector3>();var colours=new List<Color>();var indices=new List<int>();
            var length=(float)runway.Length;var width=(float)runway.Width;
            void Rectangle(float x,float z,float w,float l,Color colour,float height=.025f)
            {
                var i=vertices.Count;vertices.Add(new Vector3(x-w/2,height,z-l/2));vertices.Add(new Vector3(x+w/2,height,z-l/2));
                vertices.Add(new Vector3(x-w/2,height,z+l/2));vertices.Add(new Vector3(x+w/2,height,z+l/2));
                for(var n=0;n<4;n++) colours.Add(colour.linear);
                indices.Add(i);indices.Add(i+2);indices.Add(i+1);indices.Add(i+1);indices.Add(i+2);indices.Add(i+3);
            }
            Rectangle(0,0,width,length,new Color(.26f,.28f,.28f),0);
            var white=new Color(.84f,.84f,.79f);
            Rectangle(-width/2+1.2f,0,.35f,length-12,white);Rectangle(width/2-1.2f,0,.35f,length-12,white);
            for(var z=-length/2+80;z<length/2-80;z+=60) Rectangle(0,z,.9f,30,white);
            foreach(var sign in new[]{-1,1})
            {
                var z=sign*(length/2-25);var bars=width>=40 ? 5 : 3;
                for(var n=1;n<=bars;n++)
                { Rectangle(-n*width/(2*bars+3),z,1.8f,30,white);Rectangle(n*width/(2*bars+3),z,1.8f,30,white); }
                if(length>1400)
                {
                    Rectangle(-width*.23f,sign*(length/2-300),3,45,white);
                    Rectangle(width*.23f,sign*(length/2-300),3,45,white);
                }
            }
            var mesh=new Mesh {name="Mapped runway "+runway.Code};mesh.SetVertices(vertices);mesh.SetColors(colours);
            mesh.SetTriangles(indices,0);mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;
        }
        public const float CockpitFadeStartMetres = 40000f, CockpitFadeEndMetres = 55000f;
        private float _fadeStart = CockpitFadeStartMetres, _fadeEnd = CockpitFadeEndMetres;
        private readonly System.Diagnostics.Stopwatch _buildClock = new();
        /// <summary>Slowest single tile build so far, in milliseconds (the soak log reports it).</summary>
        public double SlowestBuildMs { get; private set; }
        /// <summary>
        /// The horizon fade (metres before <c>_AirsideHorizonScale</c>). The wide overview pulls it in so the terrain
        /// dissolves into the sky before its streamed ring ends, instead of stopping at a hard edge.
        /// </summary>
        public void SetHorizonFade(float start, float end)
        {
            if (Math.Abs(start - _fadeStart) < 1f && Math.Abs(end - _fadeEnd) < 1f) return;
            _fadeStart = start; _fadeEnd = end;
            _material.SetFloat("_HorizonFadeStart", start);
            _material.SetFloat("_HorizonFadeEnd", end);
        }
        public void Tick(double worldX, double worldZ, double originX, double originZ, bool wide = false,
            int coarseRadius = FlightWorldGrid.CoarseRadiusTiles, double altitudeMetres = 0)
        {
            if (!wide) SetHorizonFade(CockpitFadeStartMetres, CockpitFadeEndMetres);
            _originX = originX; _originZ = originZ;
            _cruise = !wide && FlightWorldDetail.Cruise(altitudeMetres, _cruise);
            UpdateAirport(worldX,worldZ,altitudeMetres,wide);
            _streamFrame++;
            var keepCoarse = wide || _cruise;
            if (_cruise) { coarseRadius=3; SetHorizonFade(110000,170000); }
            foreach(var strip in _runways)
            {
                strip.view.gameObject.SetActive(!wide && FlightWorldDetail.RunwayDistance(worldX,worldZ,strip.runway)<85000);
                strip.view.position=new Vector3((float)((strip.runway.Ax+strip.runway.Bx)/2-originX),
                    (float)(strip.runway.Elevation+AirsideFlightPath.GroundY+.02),(float)((strip.runway.Az+strip.runway.Bz)/2-originZ));
            }
            var cx = FlightWorldGrid.Tile(worldX); var cz = FlightWorldGrid.Tile(worldZ);
            var ccx = FlightWorldGrid.CoarseTile(worldX); var ccz = FlightWorldGrid.CoarseTile(worldZ);
            Retire(_tiles, cx, cz, false, true, 0);
            Retire(_coarse, ccx, ccz, true, keepCoarse, coarseRadius);
            // Alternate rings: cruise horizon appears immediately instead of starving behind all fine tiles.
            if (keepCoarse && _streamFrame % 2 == 0 && FillCoarse(ccx,ccz,coarseRadius)) return;
            // Fill nearest first, bounded work. Existing tiles stay visible throughout travel.
            for (var ring = 0; ring <= FlightWorldGrid.RadiusTiles; ring++)
            for (var dz = -ring; dz <= ring; dz++)
            for (var dx = -ring; dx <= ring; dx++)
            {
                if (Math.Max(Math.Abs(dx),Math.Abs(dz)) != ring) continue;
                var key = (cx+dx,cz+dz);
                var cells=CellsFor(key.Item1,key.Item2);
                if (_tiles.ContainsKey(key))
                {
                    if(_tileCells[key]==cells) continue;
                    // Replace one density per frame. The previous tile remains until its successor is ready.
                    var previous=_tiles[key];var replacement=Build(key.Item1,key.Item2,false);
                    _tiles[key]=replacement;_tileCells[key]=cells;Position(key,replacement.transform,FlightWorldGrid.TileMetres);
                    Destroy(previous.GetComponent<MeshFilter>().sharedMesh);Destroy(previous);return;
                }
                var go = Build(key.Item1,key.Item2,false);
                _tiles.Add(key,go); _tileCells[key]=cells; Position(key,go.transform,FlightWorldGrid.TileMetres);
                return;
            }
            if(keepCoarse) FillCoarse(ccx,ccz,coarseRadius);
        }
        private bool FillCoarse(int cx,int cz,int radius)
        {
            for(var ring=0;ring<=radius;ring++)
            for(var dz=-ring;dz<=ring;dz++)
            for(var dx=-ring;dx<=ring;dx++)
            {
                if(Math.Max(Math.Abs(dx),Math.Abs(dz))!=ring) continue;
                var key=(cx+dx,cz+dz);if(_coarse.ContainsKey(key)) continue;
                var go=Build(key.Item1,key.Item2,true);_coarse.Add(key,go);
                Position(key,go.transform,FlightWorldGrid.CoarseTileMetres);return true;
            }
            return false;
        }
        private static FlightWorldHeights LoadHeights(string path)
        {
            try { var file=ArtRuntimePaths.ResolveExisting(path);return file==null ? null : FlightWorldHeights.Parse(File.ReadAllBytes(file)); }
            catch(IOException) { return null; }
        }
        private static FlightWorldLandCover LoadCover(string path)
        {
            try { var file=ArtRuntimePaths.ResolveExisting(path);return file==null ? null : FlightWorldLandCover.Parse(File.ReadAllBytes(file)); }
            catch(IOException) { return null; }
        }
        private int CellsFor(int tx,int tz)
        {
            var approach=!_cruise && _airport.HasValue && FlightWorldDetail.ApproachTile(tx,tz,
                (_airport.Value.Ax+_airport.Value.Bx)/2,(_airport.Value.Az+_airport.Value.Bz)/2);
            return FlightWorldDetail.Cells(_cruise,approach);
        }
        private void UpdateAirport(double x,double z,double altitude,bool wide)
        {
            RegionalRunway? nearest=null;var distance=FlightWorldDetail.AirportLoadMetres;
            var approaching=!wide && altitude<FlightWorldDetail.AirportPrefetchCeilingMetres;
            if(approaching)
            foreach(var runway in RegionalRunways.All)
            {
                var d=FlightWorldDetail.RunwayDistance(x,z,runway);
                if(d<distance){distance=d;nearest=runway;}
            }
            if(!nearest.HasValue && approaching && _airport.HasValue &&
                FlightWorldDetail.RunwayDistance(x,z,_airport.Value)<FlightWorldDetail.AirportUnloadMetres) nearest=_airport;
            if(nearest?.Code!=_airport?.Code)
            {
                if(_airportEnvironment!=null) Destroy(_airportEnvironment.gameObject);
                _airportEnvironment=null;_airport=nearest;_approachHeights=null;_approachCover=null;
                if(nearest.HasValue)
                {
                    var code=nearest.Value.Code.ToLowerInvariant();
                    _approachHeights=LoadHeights("Terrain/dem_approach_"+code+"_v01.bin");
                    _approachCover=LoadCover("Terrain/landcover_approach_"+code+"_v01.bin");
                    _airportEnvironment=AirsideFlightAirportEnvironment.Create(nearest.Value,_material,_approachHeights ?? _nationalHeights ?? _heights, _approachCover ?? _nationalCover ?? _cover);
                    if(_airportEnvironment!=null) _airportEnvironment.transform.SetParent(transform,false);
                }
            }
            if(_airportEnvironment!=null) _airportEnvironment.Tick(_originX,_originZ,!_cruise && !wide && altitude<5000);
        }
        /// <summary>Re-seats resident tiles for the current origin and destroys those that left the ring (all of them when not kept).</summary>
        private void Retire(Dictionary<(int x, int z), GameObject> tiles, int cx, int cz, bool coarse, bool keep, int coarseRadius)
        {
            _drop.Clear();
            var size = coarse ? FlightWorldGrid.CoarseTileMetres : FlightWorldGrid.TileMetres;
            foreach (var pair in tiles)
            {
                var resident = keep && (coarse ? FlightWorldGrid.CoarseResident(pair.Key.x,pair.Key.z,cx,cz,coarseRadius)
                    : FlightWorldGrid.Resident(pair.Key.x,pair.Key.z,cx,cz));
                if (!resident) _drop.Add(pair.Key);
                else Position(pair.Key, pair.Value.transform, size);
            }
            foreach (var key in _drop)
            {
                var go = tiles[key];
                Destroy(go.GetComponent<MeshFilter>().sharedMesh);
                Destroy(go); tiles.Remove(key); if(!coarse) _tileCells.Remove(key);
            }
        }
        private void Position((int x,int z) key, Transform tile, int tileMetres) => tile.position =
            new Vector3((float)(key.x*(double)tileMetres-_originX),0,
                        (float)(key.z*(double)tileMetres-_originZ));
        private GameObject Build(int tx, int tz, bool coarse)
        {
            _buildClock.Restart();
            var go = BuildTile(tx, tz, coarse);
            var ms = _buildClock.Elapsed.TotalMilliseconds;
            if (ms > SlowestBuildMs)
            {
                SlowestBuildMs = ms;
                if (ms > 50) Debug.Log($"[Airside terrain] slowest tile so far {ms:F0} ms ({(coarse ? "coarse" : "fine")} {tx},{tz})");
            }
            return go;
        }
        private GameObject BuildTile(int tx, int tz, bool coarse)
        {
            var tileMetres = coarse ? FlightWorldGrid.CoarseTileMetres : FlightWorldGrid.TileMetres;
            var cells = coarse ? FlightWorldGrid.CoarseCells : CellsFor(tx,tz);
            var n = cells+1;
            var vertices = new Vector3[n*n]; var colors = new Color[n*n];
            var triangles = new List<int>(cells*cells*6);
            var stride = tileMetres / cells;
            var settlementLights = new SettlementLights.Batch();

            for (var z=0; z<n; z++) for (var x=0; x<n; x++)
            {
                var wx=tx*(double)tileMetres+x*stride;
                var wz=tz*(double)tileMetres+z*stride;
                YpadFrame.ToLatLon(wx,wz,out var lat,out var lon);
                // The coarse ring takes known dry land from the cover map and only runs the (costly) coast test for water,
                // so a 1,089-vertex tile costs about what a fine tile does.
                // Water comes from real cover at the active density, so Derwent estuary and coastal airports
                // use their mapped shorelines. Coast polygons are the offline/missing-grid fallback.
                var cover = !coarse && !_cruise && _approachCover!=null && _approachCover.TryClass(lat,lon,out _)
                    ? _approachCover : _nationalCover!=null && _nationalCover.TryClass(lat,lon,out _)
                    ? _nationalCover : _cover;
                var sampled=cover!=null && cover.TryClass(lat,lon,out _);
                var land=sampled ? cover.TryClass(lat,lon,out var cls) && cls!=AdelaideFarLandCover.Water : MapGeography.OnLand(lon,lat);
                var height=0.0;
                if(land && !(!_cruise && !coarse && _approachHeights!=null && _approachHeights.TryHeight(lat,lon,out height))
                    && !(_nationalHeights!=null && _nationalHeights.TryHeight(lat,lon,out height))) _heights?.TryHeight(lat,lon,out height);
                var y = land ? AirsideFlightPath.GroundY + RegionalRunways.Ground(wx,wz,height-AdelaideTerrainHeights.PlainAboveSeaMetres) : -4;
                if (coarse) y-=FlightWorldGrid.CoarseDropMetres;
                // The original detailed meshes own Adelaide. This landscape tucks under them.
                if (Math.Abs(wx)<=96000 && Math.Abs(wz)<=96000) y-=12;
                vertices[z*n+x]=new Vector3(x*stride,(float)y,z*stride);
                if (land && cover != null && CoverColour(cover,lat,lon,coarse,out var coverColour))
                {
                    colors[z*n+x]=coverColour;
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
                var wx=tx*(double)tileMetres+(x+.5)*stride;
                var wz=tz*(double)tileMetres+(z+.5)*stride;
                if(Math.Abs(wx)<95000 && Math.Abs(wz)<95000) continue;
                triangles.Add(a);triangles.Add(c);triangles.Add(b);
                triangles.Add(b);triangles.Add(c);triangles.Add(d);
            }
            if(!coarse)
            {
                // Lighting density is geographic, independent of the altitude-driven terrain LOD.
                // Sampling only coarse mesh vertices missed small towns and changed lights in cruise.
                const int lightStep=500;
                for(var z=lightStep/2;z<tileMetres;z+=lightStep)
                for(var x=lightStep/2;x<tileMetres;x+=lightStep)
                {
                    var wx=tx*(double)tileMetres+x;var wz=tz*(double)tileMetres+z;
                    if(Math.Abs(wx)<=96000 && Math.Abs(wz)<=96000) continue;
                    YpadFrame.ToLatLon(wx,wz,out var lat,out var lon);
                    var cover=_approachCover!=null && _approachCover.TryClass(lat,lon,out _) ? _approachCover
                        : _nationalCover!=null && _nationalCover.TryClass(lat,lon,out _) ? _nationalCover : _cover;
                    if(cover==null || !cover.TryClass(lat,lon,out var cls) || cls!=AdelaideFarLandCover.Built) continue;
                    var gx=x/stride;var gz=z/stride;var ix=(int)gx;var iz=(int)gz;
                    var u=gx-ix;var v=gz-iz;
                    var a=vertices[iz*n+ix].y;var b=vertices[iz*n+ix+1].y;
                    var c=vertices[(iz+1)*n+ix].y;var d=vertices[(iz+1)*n+ix+1].y;
                    // Drape on the actual rendered triangle, rather than beneath a coarse hills mesh.
                    var y=u+v<=1 ? a+(b-a)*u+(c-a)*v : d+(c-d)*(1-u)+(b-d)*(1-v);
                    settlementLights.Glow(new Vector3(x,y+5,z),2,new Color(1,.70f,.36f).linear*.7f,20);
                }
            }
            var mesh=new Mesh {name=coarse ? $"SA coarse terrain {tx},{tz}" : $"SA terrain {tx},{tz}"};
            mesh.vertices=vertices;mesh.colors=colors;mesh.SetTriangles(triangles,0);
            mesh.RecalculateNormals();mesh.RecalculateBounds();
            var go=new GameObject(mesh.name);go.transform.SetParent(transform,false);
            go.AddComponent<MeshFilter>().sharedMesh=mesh;
            var renderer=go.AddComponent<MeshRenderer>();renderer.sharedMaterial=_material;
            renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
            settlementLights.Attach(go.transform,"Mapped settlement lights",40000,55000);
            return go;
        }
        /// <summary>
        /// Real land cover, in the palette the Adelaide rings use so the two meet without a step. A vertex stands for a
        /// whole 1 or 2 km cell, so it takes the mean colour of the land cells round it: one point sample per vertex
        /// read as dark-green and straw speckle from the overview. Water neighbours are left out so coasts stay land-coloured.
        /// </summary>
        private bool CoverColour(FlightWorldLandCover cover,double lat, double lon, bool coarse, out Color colour)
        {
            colour = default;
            var reach = coarse ? 1.0 : 0.5;
            float r = 0f, g = 0f, b = 0f; var count = 0;
            for (var dz = -1; dz <= 1; dz++)
            for (var dx = -1; dx <= 1; dx++)
            {
                var sLat = lat + dz * reach * cover.StepDegrees;
                var sLon = lon + dx * reach * cover.StepDegrees;
                if (!cover.TryClass(sLat, sLon, out var cls) || cls == AdelaideFarLandCover.Water) continue;
                if (!cover.TryCell(sLat, sLon, out var cx, out var cz)) continue;
                // Cells are ~1 km; the palette's paddocks and variation are sized for 250 m cells.
                AdelaideOuterTerrainGeometry.LandCoverColour(cls, cx * 4, cz * 4, 0f, _seaLinear, _tint);
                r += _tint[0]; g += _tint[1]; b += _tint[2]; count++;
            }
            if (count > 0)
            {
                colour = new Color(r / count, g / count, b / count, 0f);
                return true;
            }
            if (!cover.TryCell(lat, lon, out var wx, out var wz) || !cover.TryClass(lat, lon, out var centre)) return false;
            var alpha = AdelaideOuterTerrainGeometry.LandCoverColour(centre, wx * 4, wz * 4, 0f, _seaLinear, _tint);
            colour = new Color(_tint[0], _tint[1], _tint[2], alpha);
            return true;
        }
        private void OnDestroy()
        {
            foreach(var tile in _tiles.Values) if(tile!=null) Destroy(tile.GetComponent<MeshFilter>().sharedMesh);
            foreach(var tile in _coarse.Values) if(tile!=null) Destroy(tile.GetComponent<MeshFilter>().sharedMesh);
            foreach(var strip in _runways) if(strip.view!=null) Destroy(strip.view.GetComponent<MeshFilter>().sharedMesh);
            if(_airportEnvironment!=null) Destroy(_airportEnvironment.gameObject);
            if(_runwayMaterial!=null) Destroy(_runwayMaterial);
            if(_material!=null) Destroy(_material);
        }
    }
}
