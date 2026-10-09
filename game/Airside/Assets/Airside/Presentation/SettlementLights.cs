using System;
using System.Collections;
using Airside.Simulation;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Airside.Presentation
{
    /// <summary>Original batched, terrain-depth-tested settlement emission. No Unity point lights or saved state.</summary>
    public sealed class SettlementLights : MonoBehaviour
    {
        // Explicit OSM lit=no ways in the existing 8 Oct snapshot override inferred urban lighting.
        private static readonly HashSet<long> UnlitRoads = new() { 16765994L,50534441L,57371707L,230893938L,745337966L,745337980L,857942938L,905276521L,941812647L,1050942530L,1063233218L,1063233219L,1481698136L,1481698137L,1482517854L,1482517855L };
        private Material _material;
        private Mesh _mesh;
        public sealed class Batch
        {
            private readonly List<Vector3> _vertices = new();
            private readonly List<Vector3> _modes = new();
            private readonly List<Vector2> _uv = new();
            private readonly List<Color> _colours = new();
            private readonly List<int> _indices = new();
            public int Count => _vertices.Count;
            private void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Color colour, Vector3 mode)
            {
                var start = Count;
                _vertices.Add(a); _vertices.Add(b); _vertices.Add(c); _vertices.Add(d);
                _uv.Add(new Vector2(-1,-1)); _uv.Add(new Vector2(1,-1));
                _uv.Add(new Vector2(1,1)); _uv.Add(new Vector2(-1,1));
                for (var i=0;i<4;i++) { _colours.Add(colour); _modes.Add(mode); }
                _indices.Add(start); _indices.Add(start+1); _indices.Add(start+2);
                _indices.Add(start); _indices.Add(start+2); _indices.Add(start+3);
            }
            public void Glow(Vector3 position, float radius, Color colour, float maximumRadius = 8)
                => Quad(position, position, position, position, colour, new Vector3(radius,1,maximumRadius));
            public void Pool(Vector3 position, float radius, Color colour)
            {
                var x = Vector3.right*radius; var z = Vector3.forward*radius;
                Quad(position-x-z,position+x-z,position+x+z,position-x+z,colour,new Vector3(0,0,0));
            }
            public void Windows(Vector3 a, Vector3 b, float height, bool house, Vector3? outwardNormal = null)
            {
                var delta=b-a; delta.y=0;
                var length=delta.magnitude;
                if (length<3 || height<2.4f) return;
                var along=delta/length;
                // Polygon winding differs between imported sources: both sides are rendered;
                // a 2 cm offset avoids z-fighting without detaching the window from its wall.
                var outward=(outwardNormal ?? new Vector3(along.z,0,-along.x)).normalized*.02f;
                var columns=Math.Min(20,(int)(length/3.2f));
                var floors=house ? 1 : Math.Min(8,(int)(height/3.2f));
                for(var floor=0;floor<floors;floor++)
                for(var col=0;col<columns;col++)
                {
                    var centre=a+along*(length*(col+.5f)/columns)+Vector3.up*(1.4f+floor*3.2f);
                    // Stable geographic occupancy; no flicker, random source or frame dependence.
                    var hash=Hash(centre.x,centre.z,floor);
                    if(hash%100>= (house ? 58 : 38)) continue;
                    var warm=Color.Lerp(new Color(1,.56f,.24f),new Color(1,.85f,.57f),(hash%17)/16f).linear;
                    var half=along*.48f; var up=Vector3.up*.55f;
                    Quad(centre-half-up+outward,centre+half-up+outward,
                        centre+half+up+outward,centre-half+up+outward,warm*.85f,new Vector3(0,-1,0));
                }
            }
            public void Street(Vector3 ground, Vector3 roadDirection, float width, bool pole = true)
            {
                var side=new Vector3(-roadDirection.z,0,roadDirection.x).normalized;
                var foot=ground+side*(width*.5f+.65f);
                var head=foot+Vector3.up*7.5f-side*.8f;
                var warm=new Color(1,.70f,.33f).linear;
                Glow(head,.36f,warm*.75f,5);
                Pool(ground+Vector3.up*.10f,5.5f,warm*.065f);
                if (!pole) return;
                var metal=new Color(.19f,.21f,.22f).linear;
                // Crossed slender strips give a pole silhouette at close range, in the same batch.
                foreach(var axis in new[]{Vector3.right,Vector3.forward})
                {
                    var half=axis*.065f; var top=foot+Vector3.up*7.5f;
                    Quad(foot-half,foot+half,top+half,top-half,metal,new Vector3(0,2,0));
                }
                var arm=Vector3.up*.06f;
                var start=foot+Vector3.up*7.5f;
                Quad(start-arm,start+arm,head+arm,head-arm,metal,new Vector3(0,2,0));
            }
            private static uint Hash(float x,float z,int floor)
            {
                unchecked
                {
                    var h=(uint)Mathf.FloorToInt(x*2)*73856093u^(uint)Mathf.FloorToInt(z*2)*19349663u^(uint)floor*83492791u;
                    h^=h>>16;h*=2246822519u;return h^(h>>13);
                }
            }
            public SettlementLights Attach(Transform parent, string name)
            {
                if(Count==0 || parent==null) return null;
                var shader=Resources.Load<Shader>("Airside/Shaders/SettlementLights");
                if(shader==null) { Debug.LogWarning("[Airside] Settlement light shader unavailable");return null; }
                var go=new GameObject(name);go.transform.SetParent(parent,false);
                var owner=go.AddComponent<SettlementLights>();
                owner._mesh=new Mesh {name=name,indexFormat=IndexFormat.UInt32};
                owner._mesh.SetVertices(_vertices);owner._mesh.SetNormals(_modes);owner._mesh.SetUVs(0,_uv);
                owner._mesh.SetColors(_colours);owner._mesh.SetTriangles(_indices,0);owner._mesh.RecalculateBounds();
                var bounds=owner._mesh.bounds;bounds.Expand(80);owner._mesh.bounds=bounds;
                owner._material=new Material(shader) {name="mat_"+name};
                go.AddComponent<MeshFilter>().sharedMesh=owner._mesh;
                var renderer=go.AddComponent<MeshRenderer>();renderer.sharedMaterial=owner._material;
                renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
                return owner;
            }
        }
        public static IEnumerator AdelaideStreets(Transform parent, Func<float,float,float> groundHeight)
        {
            var batches=new Dictionary<Vector2Int,Batch>();
            var points=AdelaideRoadNetwork.Points;
            var cover=AirsideAdelaideOuterTerrain.LoadLandCover();
            var timer=System.Diagnostics.Stopwatch.StartNew();
            var lamps=AdelaideCarParks.Lamps;
            // The airport builder already renders mapped lamps within 2.2 km of its car-park centre.
            for(var i=0;i<lamps.Length;i+=2)
            {
                var x=lamps[i];var z=lamps[i+1];
                var dx=x-AdelaideCarParkGeometry.CentreX;var dz=z-AdelaideCarParkGeometry.CentreZ;
                if(dx*dx+dz*dz<=2200f*2200f) continue;
                var tile=new Vector2Int(Mathf.FloorToInt(x/1500),Mathf.FloorToInt(z/1500));
                if(!batches.TryGetValue(tile,out var batch)) batches[tile]=batch=new Batch();
                // Existing lamp props use this exact head offset and height.
                var head=new Vector3(x+.5f,groundHeight(x,z)+AdelaideCarParkGeometry.LampHeight,z);
                batch.Glow(head,.36f,new Color(1,.70f,.33f).linear*.75f,5);
                batch.Pool(new Vector3(x,groundHeight(x,z)+.10f,z),5.5f,new Color(1,.77f,.43f).linear*.065f);
            }
            foreach(var road in AdelaideRoadNetwork.Roads)
            {
                if(UnlitRoads.Contains(road.OsmId) || road.Layer!=0 || road.IsAirside || road.Surface==AdelaideRoadNetwork.SurfaceKind.Unpaved
                    || road.Class==AdelaideRoadNetwork.RoadClass.Track || road.Class==AdelaideRoadNetwork.RoadClass.Service
                    || (road.Flags & (AdelaideRoadNetwork.RoadFlags.Tunnel|AdelaideRoadNetwork.RoadFlags.Restricted))!=0)
                    continue;
                var next=19f;
                for(var i=1;i<road.PointCount;i++)
                {
                    var at=(road.PointStart+i)*2;
                    var a=new Vector3(points[at-2],0,points[at-1]);
                    var b=new Vector3(points[at],0,points[at+1]);
                    var length=Vector3.Distance(a,b);if(length<.01f) continue;
                    for(;next<length;next+=38)
                    {
                        var point=Vector3.Lerp(a,b,next/length);
                        if(AdelaideWindbreakPlacement.InsideOutline(AdelaideBoundary.Outline,point.x,point.z)) continue;
                        // Rural highways are not uniformly lit. Missing mapping never invents lamps.
                        if(cover==null || cover.ClassAt(Mathf.RoundToInt((point.x-cover.Origin)/cover.Spacing),
                            Mathf.RoundToInt((point.z-cover.Origin)/cover.Spacing))!=AdelaideFarLandCover.Built) continue;
                        var mapped=false;
                        for(var lamp=0;lamp<lamps.Length;lamp+=2)
                            if((point.x-lamps[lamp])*(point.x-lamps[lamp])+(point.z-lamps[lamp+1])*(point.z-lamps[lamp+1])<35*35)
                            { mapped=true;break; }
                        if(mapped) continue;
                        var tile=new Vector2Int(Mathf.FloorToInt(point.x/1500),Mathf.FloorToInt(point.z/1500));
                        if(!batches.TryGetValue(tile,out var batch)) batches[tile]=batch=new Batch();
                        point.y=groundHeight(point.x,point.z);
                        batch.Street(point,(b-a)/length,road.Width);
                    }
                    next-=length;
                }
                if(timer.Elapsed.TotalMilliseconds>2.5) { yield return null;timer.Restart(); }
            }
            foreach(var pair in batches)
            {
                if(parent==null) yield break;
                pair.Value.Attach(parent,$"City streetlights {pair.Key.x},{pair.Key.y}");
                yield return null;
            }
        }

        public static void DistantAdelaide(Transform parent, AdelaideFarLandCover cover, AdelaideTerrainHeights terrain)
        {
            if(cover==null || terrain==null) return;
            var batches=new Dictionary<Vector2Int,Batch>();
            // Every light is accepted only at a mapped built-up sample. This is district
            // context, not a fabricated road grid or a claim about individual dwellings.
            for(var z=0;z<cover.Count;z++) for(var x=0;x<cover.Count;x++)
            {
                if(cover.ClassAt(x,z)!=AdelaideFarLandCover.Built) continue;
                var wx=cover.Origin+x*cover.Spacing;var wz=cover.Origin+z*cover.Spacing;
                if(Mathf.Abs(wx)<12000 && Mathf.Abs(wz)<12000) continue;
                var tile=new Vector2Int(Mathf.FloorToInt(wx/8000),Mathf.FloorToInt(wz/8000));
                if(!batches.TryGetValue(tile,out var batch)) batches[tile]=batch=new Batch();
                var ground=AirsideAdelaideGround.PavementWorldY-AirsideAdelaideSurroundings.PlainBelowPavement
                    +AdelaideTerrainHeights.ReliefAbovePlain(terrain.Sample(x,z));
                batch.Glow(new Vector3(wx,ground+5,wz),2,new Color(1,.70f,.36f).linear*.7f,20);
            }
            foreach(var pair in batches) pair.Value.Attach(parent,$"Distant settlements {pair.Key.x},{pair.Key.y}");
        }

        private void OnDestroy()
        {
            if(_mesh!=null) Destroy(_mesh);
            if(_material!=null) Destroy(_material);
        }
    }
}
