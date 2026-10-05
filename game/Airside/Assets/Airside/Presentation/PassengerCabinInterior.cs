using System;
using System.Linq;
using UnityEngine.Rendering;
using Airside.Domain;
using UnityEngine;

namespace Airside.Presentation
{
    /// <summary>A fitted five-row cabin section, shared exterior visibility lifetime with cockpits.</summary>
    [ExecuteAlways]
    public sealed class PassengerCabinInterior : CockpitInterior
    {
        public PassengerCabinProfile Profile { get; private set; }
        private Material _lining, _fabric, _trim, _frame;
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
            Seat.localPosition=new Vector3((right ? 1 : -1)*(Profile.HalfWidth-.43f),0,0);
            Seat.localRotation=Quaternion.Euler(0,right ? 78 : -78,0);
        }
        private void MakeCabin(bool right)
        {
            _lining=Surface("cabin lining",new Color(.80f,.80f,.75f));
            _fabric=Surface("seat upholstery",new Color(.16f,.25f,.29f));
            _trim=Surface("cabin trim",new Color(.19f,.21f,.22f));
            _frame=Surface("window reveal",new Color(.94f,.92f,.84f));
            var floor=Surface("aisle carpet",new Color(.22f,.25f,.25f));
            var light=Surface("cabin lights",new Color(.91f,.89f,.72f),false);
            Seat=new GameObject("Passenger eye").transform;Seat.SetParent(transform,false);SelectSide(right);
            var half=Profile.HalfWidth; var length=Profile.Pitch*5;
            Box("Cabin floor",new Vector3(0,-1.02f,0),new Vector3(half*2,.10f,length),floor,false);
            Box("Cabin ceiling",new Vector3(0,.99f,0),new Vector3(half*2,.10f,length),_lining,false);
            foreach(var end in new[]{-1f,1f})
                Box("Cabin section bulkhead",new Vector3(0,0,end*length*.5f),new Vector3(half*2,2.0f,.08f),_lining,false);
            foreach(var side in new[]{-1f,1f})
            {
                Box("Overhead luggage bins",new Vector3(side*(half-.27f),.69f,0),new Vector3(.53f,.31f,length),_lining);
                Box("Aisle light strip",new Vector3(side*(half-.55f),.865f,0),new Vector3(.035f,.015f,length-.15f),light);
                for(int row=-2;row<=2;row++) Window(side,row*Profile.Pitch);
            }
            var seats=0;foreach(var n in Profile.SeatGroups) seats+=n;
            var aisle=.44f;var seatWidth=(half*2-.20f-aisle*(Profile.SeatGroups.Length-1))/seats;
            for(int row=-2;row<=2;row++)
            {
                var x=-half+.10f;
                for(int group=0;group<Profile.SeatGroups.Length;group++)
                {
                    for(int seat=0;seat<Profile.SeatGroups[group];seat++)
                    {
                        var cx=x+seatWidth*.5f;var z=row*Profile.Pitch;
                        Box("Passenger seat cushion",new Vector3(cx,-.69f,z-.03f),new Vector3(seatWidth-.035f,.12f,.43f),_fabric);
                        Box("Passenger seat back",new Vector3(cx,-.24f,z-.30f),new Vector3(seatWidth-.035f,.84f,.10f),_fabric);
                        Box("Seat headrest",new Vector3(cx,.18f,z-.32f),new Vector3(seatWidth-.09f,.19f,.12f),_fabric);
                        Box("Folded tray table",new Vector3(cx,-.23f,z-.36f),new Vector3(seatWidth-.10f,.25f,.025f),_lining);
                        foreach(var arm in new[]{-1f,1f})
                            Box("Seat armrest",new Vector3(cx+arm*(seatWidth*.5f-.03f),-.41f,z-.02f),new Vector3(.045f,.06f,.42f),_trim);
                        x+=seatWidth;
                    }
                    x+=aisle;
                }
            }
            if(Profile.SeatGroups.Length == 3)
                foreach(var side in new[]{-1f,1f})
                    Box("Centre overhead luggage bins",new Vector3(side*half*.36f,.77f,0),new Vector3(.62f,.23f,length),_lining);
            BatchSurfaces();
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
                host.AddComponent<MeshFilter>().sharedMesh=mesh;host.AddComponent<MeshRenderer>().sharedMaterial=material;
                foreach(var filter in group){filter.gameObject.SetActive(false);Dispose(filter.gameObject);}
            }
        }
        private void Window(float side,float z)
        {
            var x=side*Profile.HalfWidth; const float wz=.16f, hy=.22f;
            var pitch=Profile.Pitch;
            Box("Sidewall below window",new Vector3(x,-.61f,z),new Vector3(.06f,.78f,pitch),_lining,false);
            Box("Sidewall above window",new Vector3(x,.61f,z),new Vector3(.06f,.78f,pitch),_lining,false);
            foreach(var direction in new[]{-1f,1f})
                Box("Window sidewall pillar",new Vector3(x,0,z+direction*(pitch*.25f+wz*.5f)),new Vector3(.06f,hy*2,pitch*.5f-wz),_lining,false);
            // Rounded aperture with an opaque reveal. The exterior fuselage is hidden,
            // while wings/engines/props remain visible through this actual mesh opening.
            for(int i=0;i<32;i++)
            {
                var a=i*Mathf.PI*2/32;var b=(i+1)*Mathf.PI*2/32;
                var pa=new Vector3(x,Mathf.Sin(a)*hy,z+Mathf.Cos(a)*wz);
                var pb=new Vector3(x,Mathf.Sin(b)*hy,z+Mathf.Cos(b)*wz);
                // Each quadrant fans from its enclosing rectangular corner to the arc.
                var mid=(a+b)*.5f;var corner=new Vector3(x,Mathf.Sign(Mathf.Sin(mid))*hy,z+Mathf.Sign(Mathf.Cos(mid))*wz);
                Face("Rounded window corner",new[]{corner,pa,pb},_lining);
                Face("Window reveal",new[]{pa,pb,pb-new Vector3(side*.045f,0,0),pa-new Vector3(side*.045f,0,0)},_frame);
                var ox=x-side*.047f;
                var ra=new Vector3(ox,Mathf.Sin(a)*(hy+.018f),z+Mathf.Cos(a)*(wz+.018f));
                var rb=new Vector3(ox,Mathf.Sin(b)*(hy+.018f),z+Mathf.Cos(b)*(wz+.018f));
                Face("Window rim",new[]{pa-new Vector3(side*.047f,0,0),pb-new Vector3(side*.047f,0,0),rb,ra},_frame);
            }
        }
    }
}
