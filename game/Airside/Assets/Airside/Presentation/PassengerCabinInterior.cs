using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine.Rendering;
using Airside.Domain;
using UnityEngine;

namespace Airside.Presentation
{
    /// <summary>A fitted cabin glance section, sharing exterior visibility lifetime with cockpits.</summary>
    [ExecuteAlways]
    public sealed partial class PassengerCabinInterior : CockpitInterior
    {
        public PassengerCabinProfile Profile { get; private set; }
        private Material _lining, _fabric, _trim, _frame;
        private Light _cabinLight;
        private Mesh _seatCube;
        private readonly Dictionary<Material,List<CombineInstance>> _seatParts=new();
        public static PassengerCabinInterior Build(Transform aircraft, AircraftType type, bool right)
        {
            if (aircraft == null || type == null || !PassengerCabinProfile.TryFor(type.Id, out var p))
                throw new ArgumentException("Passenger cabin profile required");
            var host=new GameObject(type.Id + " passenger cabin");
            host.transform.SetParent(aircraft,false);
            host.transform.localPosition=new Vector3(0,AircraftVisualProfiles.For(type).ModelGroundOffsetMetres+p.WindowY,p.WindowZ);
            var rig=host.AddComponent<PassengerCabinInterior>(); rig.Profile=p; rig.MakeCabin(right); return rig;
        }
        public void SelectSide(bool right)
        {
            Seat.localPosition=new Vector3((right ? 1 : -1)*(Profile.HalfWidth-Profile.SeatEyeInset),0,0);
            Seat.localRotation=Quaternion.Euler(0,right ? 78 : -78,0);
        }
        private void MakeCabin(bool right)
        {
            _lining=CabinSurface("cabin lining",new Color(.80f,.80f,.77f),.24f);
            _fabric=CabinSurface("seat upholstery",new Color(.16f,.25f,.29f),.04f);
            _trim=CabinSurface("rubber and seat frame",new Color(.15f,.17f,.18f),.09f);
            _frame=CabinSurface("moulded window trim",new Color(.92f,.91f,.86f),.38f);
            var floor=CabinSurface("aisle carpet",new Color(.22f,.25f,.25f),.02f);
            var light=Surface("cabin light diffuser",new Color(.81f,.79f,.68f),false);
            AddFabricWeave(_fabric);
            Seat=new GameObject("Passenger eye").transform;
            Seat.SetParent(transform,false);SelectSide(right);
            BuildShell(floor,light);
            BuildSeats();
            BatchSurfaces();
        }

        private Material CabinSurface(string name,Color colour,float smoothness)
        {
            var material=Surface(name,colour);
            if(material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness",smoothness);
            return material;
        }

        // Original procedural cloth, subtle enough to preserve the miniature palette.
        private void AddFabricWeave(Material material)
        {
            const int size=32;
            var texture=new Texture2D(size,size,TextureFormat.RGBA32,false)
                {name="Cabin woven fabric",wrapMode=TextureWrapMode.Repeat,filterMode=FilterMode.Bilinear};
            var pixels=new Color[size*size];
            for(var y=0;y<size;y++)
                for(var x=0;x<size;x++)
                {
                    var value=((x+y)%2==0 ? .96f : 1f)-((x%4==0 || y%4==0) ? .025f : 0f);
                    pixels[y*size+x]=new Color(value,value,value,1f);
                }
            texture.SetPixels(pixels);texture.Apply(false,true);_textures.Add(texture);
            if(material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap",texture);
            material.mainTexture=texture;material.mainTextureScale=new Vector2(5f,5f);
        }

        private void BuildSeats()
        {
            var seats=0;foreach(var n in Profile.SeatGroups) seats+=n;
            var seatWidth=(Profile.LiningHalfWidth*2-.20f-Profile.AisleWidth*(Profile.SeatGroups.Length-1))/seats;
            var lastRow=Mathf.FloorToInt((Profile.CabinLength*.5f-.45f)/Profile.Pitch);
            for(var row=-lastRow;row<=lastRow;row++)
            {
                var x=-Profile.LiningHalfWidth+.10f;
                for(var group=0;group<Profile.SeatGroups.Length;group++)
                {
                    for(var seat=0;seat<Profile.SeatGroups[group];seat++)
                    {
                        MakeSeat(x+seatWidth*.5f,row*Profile.Pitch,seatWidth,Mathf.Abs(row)<=2);
                        x+=seatWidth;
                    }
                    x+=Profile.AisleWidth;
                }
            }
            foreach(var pair in _seatParts)
            {
                var mesh=new Mesh{name="Cabin seats "+pair.Key.name,indexFormat=IndexFormat.UInt32};
                mesh.CombineMeshes(pair.Value.ToArray(),true,true);mesh.RecalculateBounds();_meshes.Add(mesh);
                var host=new GameObject(mesh.name);host.transform.SetParent(transform,false);
                host.AddComponent<MeshFilter>().sharedMesh=mesh;
                host.AddComponent<MeshRenderer>().sharedMaterial=pair.Key;
            }
            _seatParts.Clear();
        }

        // Accumulate fittings without allocating a GameObject and collider for every belt/armrest.
        private void SeatPart(string name,Vector3 position,Vector3 size,Material material,
            bool bevelled=true,Quaternion? rotation=null)
        {
            var mesh=bevelled ? AirsidePrototype.CockpitBoxMesh(size) : null;
            if(mesh==null)
            {
                if(_seatCube==null)
                {
                    var template=GameObject.CreatePrimitive(PrimitiveType.Cube);template.SetActive(false);
                    _seatCube=template.GetComponent<MeshFilter>().sharedMesh;Dispose(template);
                }
                mesh=_seatCube; // Built-in shared mesh, not owned by this interior.
            }
            if(!_seatParts.TryGetValue(material,out var parts))
            {parts=new List<CombineInstance>();_seatParts.Add(material,parts);}
            parts.Add(new CombineInstance{mesh=mesh,transform=Matrix4x4.TRS(position,rotation??Quaternion.identity,size)});
        }

        private void MakeSeat(float x,float z,float width,bool detailed)
        {
            var cushionY=Profile.FloorY+.33f;
            var backY=cushionY+.43f;
            SeatPart("Rounded seat cushion",new Vector3(x,cushionY,z-.03f),new Vector3(width-.035f,.12f,.43f),_fabric,detailed);
            SeatPart("Reclined seat back",new Vector3(x,backY,z-.30f),new Vector3(width-.035f,.80f,.115f),
                _fabric,detailed,Quaternion.Euler(-8f,0f,0f));
            if(!detailed) return; // Cheap continuation; only the five nearest rows carry fittings.
            SeatPart("Seat headrest bolster",new Vector3(x,backY+.36f,z-.35f),new Vector3(width-.09f,.20f,.15f),_fabric);
            SeatPart("Stowed tray shell",new Vector3(x,backY-.04f,z-.39f),new Vector3(width-.11f,.26f,.026f),_lining);
            SeatPart("Tray latch",new Vector3(x,backY+.10f,z-.407f),new Vector3(.035f,.024f,.013f),_trim);
            SeatPart("Seat pocket seam",new Vector3(x,backY-.24f,z-.374f),new Vector3(width-.13f,.015f,.018f),_trim);
            foreach(var side in new[]{-1f,1f})
            {
                SeatPart("Armrest cap",new Vector3(x+side*(width*.5f-.03f),cushionY+.27f,z-.02f),new Vector3(.045f,.065f,.41f),_trim);
                SeatPart("Seat support",new Vector3(x+side*width*.27f,Profile.FloorY+.16f,z-.10f),new Vector3(.035f,.28f,.035f),_trim);
            }
            SeatPart("Resting lap belt",new Vector3(x,cushionY+.067f,z-.03f),new Vector3(width-.09f,.012f,.036f),_trim);
            SeatPart("Belt buckle",new Vector3(x+.04f,cushionY+.077f,z-.03f),new Vector3(.037f,.014f,.045f),_frame);
        }

        // Cabins have a ceiling fill, rather than inheriting the pilot's panel light.
        public override void SetEnvironment(float daylight,float precipitation,float seconds)
        {
            if(_cabinLight==null)
            {
                var host=new GameObject("Cabin ceiling fill");host.transform.SetParent(transform,false);
                host.transform.localPosition=new Vector3(0,Profile.CeilingY-.24f,0);
                _cabinLight=host.AddComponent<Light>();_cabinLight.type=LightType.Point;
                _cabinLight.range=Mathf.Max(3.4f,Profile.HalfWidth*2.4f);
                _cabinLight.color=new Color(1f,.93f,.82f);_cabinLight.shadows=LightShadows.None;
            }
            var night=1f-Mathf.SmoothStep(.15f,.55f,daylight);
            _cabinLight.intensity=.08f+.82f*night;
        }
        // One draw per material, rather than hundreds of cabin/window primitives.
        private void BatchSurfaces()
        {
            var filters=GetComponentsInChildren<MeshFilter>();
            foreach(var material in _materials)
            {
                var group=filters.Where(f=>f != null && f.GetComponent<MeshRenderer>().sharedMaterial == material).ToArray();
                if(group.Length == 0) continue;
                var combine=group.Select(f=>new CombineInstance{mesh=f.sharedMesh,
                    transform=transform.worldToLocalMatrix*f.transform.localToWorldMatrix}).ToArray();
                var mesh=new Mesh{name="Passenger cabin "+material.name,indexFormat=IndexFormat.UInt32};
                mesh.CombineMeshes(combine,true,true);mesh.RecalculateBounds();_meshes.Add(mesh);
                var host=new GameObject(material.name);host.transform.SetParent(transform,false);
                host.AddComponent<MeshFilter>().sharedMesh=mesh;
                var renderer=host.AddComponent<MeshRenderer>();renderer.sharedMaterial=material;
                renderer.shadowCastingMode=ShadowCastingMode.Off;
                foreach(var filter in group)
                {
                    // Shell buffers are owned here; cached primitive meshes are shared.
                    var original=filter.sharedMesh;
                    if(_meshes.Remove(original)) Dispose(original);
                    filter.gameObject.SetActive(false);Dispose(filter.gameObject);
                }
            }
        }
    }
}
