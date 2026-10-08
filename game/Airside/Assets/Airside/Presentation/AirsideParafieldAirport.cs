using System;
using System.Collections.Generic;
using Airside.Simulation;
using UnityEngine;
using UnityEngine.Rendering;

namespace Airside.Presentation
{
    /// <summary>Mapped independent YPPF, with a project-owned high-wing trainer kit.</summary>
    public sealed class AirsideParafieldAirport : MonoBehaviour
    {
        public const string TrainerArtPath = "Models/Aircraft/mdl_parafield_trainer_v01.gltf";
        public const string ObjectName = "Parafield Airport (YPPF)";
        private ParafieldTraffic _traffic;
        private readonly Transform[] _planes = new Transform[ParafieldTraffic.AircraftCount];
        private readonly Transform[] _propellers = new Transform[ParafieldTraffic.AircraftCount];
        private readonly List<Renderer> _lamps = new();
        private readonly List<Light> _apronLights = new();
        private Material _lampMaterial;
        private double _ground;
        public ParafieldTraffic Traffic => _traffic;
        public static float GroundY => AirsideAdelaideGround.PavementWorldY - AirsideAdelaideSurroundings.PlainBelowPavement
            + AdelaideTerrainHeights.ReliefAbovePlain((float)ParafieldLayout.ElevationMetres) + .12f;

        public static AirsideParafieldAirport Create(Transform parent, ISimulationClock clock)
        {
            var go=new GameObject(ObjectName);
            go.transform.SetParent(parent,false);
            go.transform.localPosition=new Vector3((float)ParafieldLayout.CentreX,GroundY,(float)ParafieldLayout.CentreZ);
            var airport=go.AddComponent<AirsideParafieldAirport>();
            airport._traffic=new ParafieldTraffic(clock,new SeededRandomSource(593));
            airport.Build();
            return airport;
        }

        public void Tick(double preciseSeconds, bool visible, float night)
        {
            _traffic.Update();
            gameObject.SetActive(visible);
            if(!visible) return;
            for(var i=0;i<_planes.Length;i++)
            {
                var pose=_traffic.Pose(i,preciseSeconds);
                var plane=_planes[i];
                plane.localPosition=new Vector3((float)pose.Position.X,(float)(_ground+pose.Height),(float)pose.Position.Z);
                plane.localRotation=Quaternion.Euler((float)-pose.Pitch,(float)pose.Yaw,(float)pose.Bank);
                if(_propellers[i]!=null)
                    _propellers[i].localRotation=Quaternion.Euler(0,0,pose.EngineRunning ? (float)((preciseSeconds*9000)%360) : 0);
            }
            var lit=night>.10f;
            foreach(var lamp in _lamps) lamp.enabled=lit;
            foreach(var light in _apronLights) light.enabled=lit;
            if(_lampMaterial!=null) _lampMaterial.SetColor("_EmissionColor",new Color(1,.84f,.60f)*Mathf.Lerp(0,3,night));
        }

        private void Build()
        {
            var asphalt=new Batch(); var white=new Batch(); var yellow=new Batch();
            var walls=new Batch(); var roofs=new Batch(); var doors=new Batch();
            foreach(var taxiway in ParafieldLayout.Taxiways)
                for(var i=1;i<taxiway.Length;i++)
                { asphalt.Strip(taxiway[i-1],taxiway[i],10,.01f); yellow.Strip(taxiway[i-1],taxiway[i],.14,.26f); }
            foreach(var apron in ParafieldLayout.Aprons) asphalt.Polygon(apron,.035f);
            foreach(var runway in ParafieldLayout.Runways)
            {
                asphalt.Strip(runway.A,runway.B,runway.Width,.045f);
                var length=runway.Length;
                var dx=(runway.B.X-runway.A.X)/length; var dz=(runway.B.Z-runway.A.Z)/length;
                ParafieldPoint At(double distance,double across=0) => runway.A.Offset(dx*distance-dz*across,dz*distance+dx*across);
                for(var d=75.0;d<length-75;d+=60) white.Strip(At(d),At(Math.Min(d+25,length-75)),.45,.325f);
                white.Strip(At(0,-8.4),At(length,-8.4),.22,.325f);
                white.Strip(At(0,8.4),At(length,8.4),.22,.325f);
                for(var stripe=-3;stripe<=3;stripe++)
                {
                    if(stripe==0) continue;
                    white.Strip(At(5,stripe*2),At(23,stripe*2),1,.325f);
                    white.Strip(At(length-23,stripe*2),At(length-5,stripe*2),1,.325f);
                }
                var names=runway.Name.Split('/');
                Label(names[0],At(40),Math.Atan2(dx,dz)*Mathf.Rad2Deg,3.5f);
                Label(names[1],At(length-40),Math.Atan2(-dx,-dz)*Mathf.Rad2Deg,3.5f);
            }
            foreach(var footprint in ParafieldLayout.Buildings)
            {
                if(footprint.Length<3) continue;
                walls.Extrude(footprint,0,7);
                roofs.Polygon(footprint,7.08f);
                // Restrained facade detailing, fitted to each mapped footprint edge.
                for(var i=0;i<footprint.Length;i++)
                {
                    var a=footprint[i];var b=footprint[(i+1)%footprint.Length];
                    if(a.Distance(b)<12) continue;
                    var l=ParafieldPoint.Lerp(a,b,.18);var r=ParafieldPoint.Lerp(a,b,.82);
                    doors.Vertical(l,r,.2f,4.8f);
                }
            }
            asphalt.Finish(transform,"Parafield pavement",new Color(.22f,.25f,.25f),AirsideMaterialLibrary.SurfaceKind.Asphalt);
            white.Finish(transform,"Parafield runway paint",new Color(.90f,.90f,.83f),AirsideMaterialLibrary.SurfaceKind.PaintedLine);
            yellow.Finish(transform,"Parafield taxi centrelines",new Color(.84f,.66f,.25f),AirsideMaterialLibrary.SurfaceKind.PaintedLine);
            walls.Finish(transform,"Parafield hangar walls",new Color(.64f,.67f,.64f));
            roofs.Finish(transform,"Parafield hangar roofs",new Color(.45f,.53f,.52f));
            doors.Finish(transform,"Parafield hangar doors",new Color(.35f,.41f,.41f));
            BuildLights();
            for(var i=0;i<_planes.Length;i++)
            {
                var index=i;
                if(!ArtPresentationLoader.TryInstantiate(TrainerArtPath,transform,out _planes[i],
                    part=>"Parafield trainer "+index+" "+part,part=>PartColour(part,index)))
                    _planes[i]=FallbackTrainer(index);
                _planes[i].name="Parafield trainer "+(i+1);
                var prop=new GameObject("Parafield propeller").transform;
                prop.SetParent(_planes[i],false); prop.localPosition=new Vector3(0,1.19f,3.78f);
                foreach(var child in _planes[i].GetComponentsInChildren<Transform>())
                    if(child.name.Contains("prop_blade_")) child.SetParent(prop,true);
                _propellers[i]=prop;
            }
            Debug.Log("[Airside Parafield] Built 4 runways, "+ParafieldLayout.Taxiways.Length+" taxiways, "+_planes.Length+" trainers");
        }

        private static Color? PartColour(string part,int index)
        {
            if(part.Contains("glass")) return new Color(.13f,.23f,.29f,.82f);
            if(part.Contains("wheel") || part.Contains("prop_blade")) return new Color(.12f,.15f,.16f);
            if(part.Contains("accent")) return index%2==0 ? new Color(.22f,.44f,.54f) : new Color(.40f,.54f,.41f);
            if(part.Contains("nav_light_left")) return new Color(.85f,.05f,.04f);
            if(part.Contains("nav_light_right")) return new Color(.04f,.70f,.12f);
            return new Color(.90f,.91f,.85f);
        }

        private Transform FallbackTrainer(int index)
        {
            var root=new GameObject("Parafield fallback trainer").transform; root.SetParent(transform,false);
            var fuselage=GameObject.CreatePrimitive(PrimitiveType.Capsule); fuselage.transform.SetParent(root,false);
            fuselage.transform.localPosition=new Vector3(0,1.3f,0);fuselage.transform.localRotation=Quaternion.Euler(90,0,0);
            fuselage.transform.localScale=new Vector3(1.1f,4,1.1f);
            var wing=GameObject.CreatePrimitive(PrimitiveType.Cube);wing.transform.SetParent(root,false);
            wing.transform.localPosition=new Vector3(0,2,.3f);wing.transform.localScale=new Vector3(11,.14f,1.5f);
            foreach(var r in root.GetComponentsInChildren<Renderer>())
            { r.sharedMaterial=Material(PartColour("fuselage",index).Value); AirsideRuntimeQuality.StripVisualCollider(r.gameObject); }
            return root;
        }

        private void BuildLights()
        {
            _lampMaterial=Material(new Color(1,.84f,.60f));_lampMaterial.EnableKeyword("_EMISSION");
            var runway=ParafieldLayout.MainRunway;
            var dx=(runway.B.X-runway.A.X)/runway.Length;var dz=(runway.B.Z-runway.A.Z)/runway.Length;
            for(var d=0.0;d<=runway.Length;d+=60)
            for(var side=-1;side<=1;side+=2)
            {
                var p=runway.A.Offset(dx*d-dz*10*side,dz*d+dx*10*side);
                var lamp=GameObject.CreatePrimitive(PrimitiveType.Cube);lamp.name="Parafield runway edge light";
                lamp.transform.SetParent(transform,false);lamp.transform.localPosition=new Vector3((float)p.X,.25f,(float)p.Z);
                lamp.transform.localScale=new Vector3(.35f,.25f,.35f);
                AirsideRuntimeQuality.StripVisualCollider(lamp);
                var renderer=lamp.GetComponent<Renderer>();renderer.sharedMaterial=_lampMaterial;
                renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.enabled=false;_lamps.Add(renderer);
            }
            foreach(var p in ParafieldLayout.Parking)
            {
                var light=new GameObject("Parafield apron light");light.transform.SetParent(transform,false);
                light.transform.localPosition=new Vector3((float)p.X,9,(float)p.Z);
                var source=light.AddComponent<Light>();source.type=LightType.Spot;source.range=45;source.spotAngle=100;
                source.color=new Color(1,.88f,.70f);source.intensity=1.4f;source.shadows=LightShadows.None;
                source.enabled=false;_apronLights.Add(source);
                light.transform.localRotation=Quaternion.Euler(90,0,0);
            }
        }

        private void Label(string text,ParafieldPoint point,double yaw,float size)
        {
            var go=new GameObject("Parafield runway "+text);go.transform.SetParent(transform,false);
            go.transform.localPosition=new Vector3((float)point.X,.345f,(float)point.Z);
            go.transform.localRotation=Quaternion.Euler(90,(float)yaw,0);
            var label=go.AddComponent<TextMesh>();label.text=text;label.anchor=TextAnchor.MiddleCenter;
            label.characterSize=size;label.fontSize=48;label.color=new Color(.91f,.91f,.85f);
        }

        private static Material Material(Color colour)
        {
            var shader=Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var mat=new Material(shader);mat.color=colour;mat.SetFloat("_Smoothness",.22f);
            mat.SetFloat("_Cull",0);return mat;
        }

        private sealed class Batch
        {
            private readonly List<Vector3> _vertices=new();private readonly List<int> _triangles=new();
            private static Vector3 V(ParafieldPoint p,float y)=>new((float)p.X,y,(float)p.Z);
            public void Strip(ParafieldPoint a,ParafieldPoint b,double width,float y)
            {
                var length=a.Distance(b);if(length<.001)return;
                var x=-(b.Z-a.Z)/length*width/2;var z=(b.X-a.X)/length*width/2;
                Quad(V(a.Offset(x,z),y),V(b.Offset(x,z),y),V(b.Offset(-x,-z),y),V(a.Offset(-x,-z),y));
            }
            public void Vertical(ParafieldPoint a,ParafieldPoint b,float bottom,float top) =>
                Quad(V(a,bottom),V(a,top),V(b,top),V(b,bottom));
            public void Extrude(ParafieldPoint[] points,float bottom,float top)
            { for(var i=0;i<points.Length;i++) Vertical(points[i],points[(i+1)%points.Length],bottom,top); }
            public void Polygon(ParafieldPoint[] points,float y)
            {
                var xz=new List<float>();foreach(var p in points) { xz.Add((float)p.X);xz.Add((float)p.Z); }
                foreach(var triangle in AdelaideCarParkGeometry.Triangulate(xz))
                {
                    var a=V(points[triangle[0]],y);var b=V(points[triangle[1]],y);var c=V(points[triangle[2]],y);
                    if(Vector3.Cross(b-a,c-a).y<0) (b,c)=(c,b);
                    var i=_vertices.Count;_vertices.Add(a);_vertices.Add(b);_vertices.Add(c);
                    _triangles.Add(i);_triangles.Add(i+1);_triangles.Add(i+2);
                }
            }
            private void Quad(Vector3 a,Vector3 b,Vector3 c,Vector3 d)
            {
                var i=_vertices.Count;_vertices.AddRange(new[] {a,b,c,d});
                _triangles.AddRange(new[] {i,i+1,i+2,i,i+2,i+3});
            }
            public void Finish(Transform root,string name,Color colour,AirsideMaterialLibrary.SurfaceKind kind=AirsideMaterialLibrary.SurfaceKind.Default)
            {
                if(_vertices.Count==0)return;
                var mesh=new Mesh {name=name,indexFormat=IndexFormat.UInt32};
                mesh.SetVertices(_vertices);mesh.SetTriangles(_triangles,0);mesh.RecalculateNormals();mesh.RecalculateBounds();
                var go=new GameObject(name);go.transform.SetParent(root,false);go.AddComponent<MeshFilter>().sharedMesh=mesh;
                var renderer=go.AddComponent<MeshRenderer>();
                renderer.sharedMaterial=kind==AirsideMaterialLibrary.SurfaceKind.Default ? Material(colour)
                    : AirsideMaterialLibrary.CreateShared(colour,kind);
                renderer.shadowCastingMode=ShadowCastingMode.Off;
            }
        }
    }
}
