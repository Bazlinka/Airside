using System;
using System.Collections.Generic;
using System.IO;
using Airside.Simulation;
using UnityEngine;
using UnityEngine.Rendering;

namespace Airside.Presentation
{
    /// <summary>Offline OSM airport geometry, built in small chunks. Footprints retain their mapped positions.</summary>
    public sealed class AirsideFlightAirportEnvironment : MonoBehaviour
    {
        [Serializable] public sealed class Point { public double lat, lon; }
        [Serializable] public sealed class Feature { public string kind; public float width, height; public Point[] points; }
        [Serializable] public sealed class Map { public string code; public Feature[] features; }
        public const int MaximumFeatures=4096;
        private Map _map;
        private int _cursor;
        private double _centreX, _centreZ;
        private FlightWorldHeights _heights;
        private Material _material;
        private FlightWorldLandCover _cover;
        private SettlementLights.Batch _lights;
        private readonly List<Mesh> _meshes = new();
        public static AirsideFlightAirportEnvironment Create(RegionalRunway runway, Material material, FlightWorldHeights heights, FlightWorldLandCover cover)
        {
            var path=ArtRuntimePaths.ResolveExisting("Terrain/airport_"+runway.Code.ToLowerInvariant()+"_v01.json");
            if(path==null) return null;
            try
            {
                var map=JsonUtility.FromJson<Map>(File.ReadAllText(path));
                if(map?.features==null) return null;
                var result=new GameObject("Mapped airport "+runway.Code).AddComponent<AirsideFlightAirportEnvironment>();
                result._map=map;result._material=new Material(material) {name="Mapped airport solid surfaces"};
                result._material.SetFloat("_VertexSurface",1f);result._heights=heights;result._cover=cover;
                result._centreX=(runway.Ax+runway.Bx)/2;result._centreZ=(runway.Az+runway.Bz)/2;
                YpadFrame.ToLatLon(result._centreX,result._centreZ,out var latitude,out var longitude);
                var longitudeScale=Math.Cos(latitude*Math.PI/180);
                int Priority(Feature feature) => feature?.kind=="building" ? 2 : feature?.kind=="road" ? 1 : 0;
                double Distance(Feature feature)
                {
                    if(feature?.points==null || feature.points.Length==0) return double.MaxValue;
                    var point=feature.points[0];var north=point.lat-latitude;var east=(point.lon-longitude)*longitudeScale;
                    return north*north+east*east;
                }
                Array.Sort(map.features,(a,b)=>
                {
                    var priority=Priority(a).CompareTo(Priority(b));
                    return priority!=0 ? priority : Distance(a).CompareTo(Distance(b));
                });
                if(map.features.Length>MaximumFeatures) Array.Resize(ref map.features,MaximumFeatures);
                return result;
            }
            catch (Exception e) when(e is IOException || e is ArgumentException)
            { Debug.LogWarning("[Airside terrain] Airport map unavailable: "+runway.Code); return null; }
        }
        public void Tick(double originX,double originZ,bool visible)
        {
            transform.position=new Vector3((float)(_centreX-originX),0,(float)(_centreZ-originZ));
            gameObject.SetActive(visible);
            if(_cursor>=_map.features.Length) return;
            _lights=new SettlementLights.Batch();
            var vertices=new List<Vector3>();var colours=new List<Color>();var triangles=new List<int>();
            // Fixed upper bound per frame. No colliders, point lights, resource claims or individual buildings' GameObjects.
            var end=Math.Min(_map.features.Length,_cursor+24);
            for(;_cursor<end;_cursor++) Add(_map.features[_cursor],vertices,colours,triangles);
            _lights.Attach(transform,"Mapped town lights "+_cursor,40000,55000);
            if(vertices.Count==0) return;
            var mesh=new Mesh {name="Airport mapped geometry "+_cursor,indexFormat=IndexFormat.UInt32};
            mesh.SetVertices(vertices);mesh.SetColors(colours);mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();mesh.RecalculateBounds();
            _meshes.Add(mesh);
            var child=new GameObject(mesh.name);child.transform.SetParent(transform,false);
            child.AddComponent<MeshFilter>().sharedMesh=mesh;
            var renderer=child.AddComponent<MeshRenderer>();renderer.sharedMaterial=_material;
            renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
        }
        private Vector3 Position(Point point)
        {
            YpadFrame.ToWorld(point.lat,point.lon,out var x,out var z);
            var height=0.0;_heights?.TryHeight(point.lat,point.lon,out height);
            height=RegionalRunways.Ground(x,z,height-AdelaideTerrainHeights.PlainAboveSeaMetres);
            return new Vector3((float)(x-_centreX),(float)(AirsideFlightPath.GroundY+height+.12),(float)(z-_centreZ));
        }
        private void Add(Feature feature,List<Vector3> v,List<Color> c,List<int> t)
        {
            if(feature?.points==null || feature.points.Length<2 || feature.points.Length>512) return;
            var building=feature.kind=="building" || feature.kind=="terminal" || feature.kind=="hangar";
            var paved=feature.kind=="apron";
            // Real runway endpoints/widths are already represented by the OurAirports strip catalogue.
            if(feature.kind=="runway") return;
            var colour=(building ? new Color(.57f,.60f,.59f) : paved ? new Color(.45f,.47f,.46f)
                : feature.kind=="road" ? new Color(.34f,.37f,.35f) : new Color(.27f,.29f,.28f)).linear;
            if(!building && !paved)
            {
                if(feature.kind=="road")
                {
                    var road=new List<Vector3>();
                    foreach(var point in feature.points) road.Add(Position(point));
                    // Major rural roads are dark; require built-up cover before representing a street.
                    var next=19f;
                    for(var i=1;i<road.Count;i++)
                    {
                        var a=road[i-1];var b=road[i];var flat=b-a;flat.y=0;var length=flat.magnitude;
                        if(length<.01f) continue;
                        for(;next<length;next+=38)
                        {
                            var at=Vector3.Lerp(a,b,next/length);
                            YpadFrame.ToLatLon(at.x+_centreX,at.z+_centreZ,out var lat,out var lon);
                            if(_cover!=null && _cover.TryClass(lat,lon,out var cls) && cls==AdelaideFarLandCover.Built)
                                _lights.Street(at,flat/length,Mathf.Clamp(feature.width,3,60));
                        }
                        next-=length;
                    }
                }
                for(var i=1;i<feature.points.Length;i++)
                {
                    var a=Position(feature.points[i-1]);var b=Position(feature.points[i]);
                    var delta=b-a;delta.y=0;if(delta.sqrMagnitude<.01f) continue;
                    var side=Vector3.Cross(Vector3.up,delta.normalized)*Mathf.Clamp(feature.width,3,60)/2;
                    Quad(a-side,b-side,b+side,a+side,colour,v,c,t);
                    if(feature.kind=="taxiway")
                    {
                        var line=side.normalized*.22f;a.y+=.04f;b.y+=.04f;
                        Quad(a-line,b-line,b+line,a+line,new Color(.77f,.64f,.26f).linear,v,c,t);
                    }
                }
                return;
            }
            var count=feature.points.Length;
            if(Math.Abs(feature.points[0].lat-feature.points[count-1].lat)<.0000001
                && Math.Abs(feature.points[0].lon-feature.points[count-1].lon)<.0000001) count--;
            if(count<3) return;
            var polygon=new List<Vector3>(count);var averageY=0f;
            for(var i=0;i<count;i++){var point=Position(feature.points[i]);polygon.Add(point);averageY+=point.y;}
            averageY/=count;
            var signedArea=0f;
            for(var i=0;i<count;i++) {var a=polygon[i];var b=polygon[(i+1)%count];signedArea+=a.x*b.z-b.x*a.z;}
            if(signedArea<0) polygon.Reverse();
            var roofY=averageY+(building ? Mathf.Clamp(feature.height,3,45) : .1f);
            var roofColour=building ? Color.Lerp(colour,Color.white,.14f) : colour;
            for(var i=0;i<count;i++)
            {
                var a=polygon[i];var b=polygon[(i+1)%count];a.y=averageY;b.y=averageY;
                var ar=a;var br=b;ar.y=br.y=roofY;
                if(building) Quad(a,b,br,ar,colour,v,c,t);
                if(feature.kind=="building") _lights.Windows(a,b,roofY-averageY,false);
                polygon[i]=ar;
            }
            Roof(polygon,roofColour,v,c,t);
        }
        private static void Quad(Vector3 a,Vector3 b,Vector3 d,Vector3 e,Color colour,List<Vector3> v,List<Color> c,List<int> t)
        {
            var start=v.Count;v.Add(a);v.Add(b);v.Add(d);v.Add(e);for(var i=0;i<4;i++) c.Add(colour);
            t.Add(start);t.Add(start+2);t.Add(start+1);t.Add(start);t.Add(start+3);t.Add(start+2);
        }
        private static float Cross(Vector3 a,Vector3 b,Vector3 c) => (b.x-a.x)*(c.z-a.z)-(b.z-a.z)*(c.x-a.x);
        private static void Roof(List<Vector3> polygon,Color colour,List<Vector3> v,List<Color> c,List<int> t)
        {
            // Ear clipping respects concave terminal/apron footprints; malformed polygons retain their walls.
            var ids=new List<int>();var area=0f;
            for(var i=0;i<polygon.Count;i++){ids.Add(i);var a=polygon[i];var b=polygon[(i+1)%polygon.Count];area+=a.x*b.z-b.x*a.z;}
            if(area<0) ids.Reverse();
            var start=v.Count;v.AddRange(polygon);for(var i=0;i<polygon.Count;i++) c.Add(colour);
            var budget=polygon.Count*polygon.Count;
            while(ids.Count>2 && budget-->0)
            {
                var clipped=false;
                for(var i=0;i<ids.Count;i++)
                {
                    var ia=ids[(i+ids.Count-1)%ids.Count];var ib=ids[i];var ic=ids[(i+1)%ids.Count];
                    var a=polygon[ia];var b=polygon[ib];var d=polygon[ic];if(Cross(a,b,d)<=.001f) continue;
                    var contains=false;
                    foreach(var id in ids)
                    {
                        if(id==ia || id==ib || id==ic) continue;var p=polygon[id];
                        if(Cross(a,b,p)>=0 && Cross(b,d,p)>=0 && Cross(d,a,p)>=0){contains=true;break;}
                    }
                    if(contains) continue;
                    t.Add(start+ia);t.Add(start+ic);t.Add(start+ib);ids.RemoveAt(i);clipped=true;break;
                }
                if(!clipped) break;
            }
        }
        private void OnDestroy(){foreach(var mesh in _meshes) if(mesh!=null) Destroy(mesh);if(_material!=null) Destroy(_material);}
    }
}
