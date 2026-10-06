using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Airside.Presentation
{
    public sealed partial class PassengerCabinInterior
    {
        // Construction-only buffers. Repeated fittings share a material mesh before
        // the root batches the entire cabin; no per-window GameObjects or colliders.
        private sealed class CabinShellBuffer
        {
            public readonly List<Vector3> Vertices = new();
            public readonly List<Vector3> Normals = new();
            public readonly List<Vector2> Uv = new();
            public readonly List<int> Triangles = new();
            public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3[] smoothNormals=null)
            {
                var normal = Vector3.Cross(b-a,c-a).normalized;
                var points = new[] { a,b,c,d };
                var normalSign=smoothNormals != null && Vector3.Dot(normal,smoothNormals[0])<0 ? -1f : 1f;
                for (var side=0; side<2; side++)
                {
                    var start=Vertices.Count;
                    for(var i=0;i<points.Length;i++)
                    {
                        var point=points[i];
                        var vertexNormal=smoothNormals == null ? normal : smoothNormals[i]*normalSign;
                        Vertices.Add(point); Normals.Add(side == 0 ? vertexNormal : -vertexNormal);
                        // Metre coordinates keep construction details at a consistent scale.
                        Uv.Add(new Vector2(point.z,point.y));
                    }
                    if(side == 0)
                    {
                        Triangles.AddRange(new[]{start,start+1,start+2});
                        if(c!=d) Triangles.AddRange(new[]{start,start+2,start+3});
                    }
                    else
                    {
                        Triangles.AddRange(new[]{start,start+2,start+1});
                        if(c!=d) Triangles.AddRange(new[]{start,start+3,start+2});
                    }
                }
            }
        }

        private void BuildShell(Material floor, Material light)
        {
            var buffers=new Dictionary<Material,CabinShellBuffer>();
            CabinShellBuffer Buffer(Material material)
            {
                if(!buffers.TryGetValue(material,out var buffer))
                { buffer=new CabinShellBuffer(); buffers.Add(material,buffer); }
                return buffer;
            }
            var lining=Buffer(_lining); var frame=Buffer(_frame); var trim=Buffer(_trim);
            var lamps=Buffer(light);
            var end=Profile.CabinLength*.5f;
            var shoulder=Mathf.Min(Profile.CeilingY-.12f,
                Mathf.Max(Profile.WindowHeight*.5f+.12f,Profile.CeilingY*.64f));
            var half=Profile.LiningHalfWidth;
            Box("Cabin carpet floor",new Vector3(0,Profile.FloorY-.04f,0),
                new Vector3(half*2,.08f,Profile.CabinLength),floor,false);

            // Window centres follow the exterior station grid, independently of seats.
            // A whole station cell surrounds each aperture; terminal bands close the
            // remaining wall. The four exact rectangle corner angles are included so
            // no chord cuts a dark triangle out of a cell corner.
            var stations=Mathf.FloorToInt((end-Profile.WindowPitch*.5f)/Profile.WindowPitch);
            var cellHalf=Profile.WindowPitch*.5f;
            var angles=CabinWindowAngles(cellHalf,Profile.FloorY,shoulder);
            foreach(var side in new[]{-1f,1f})
            {
                for(var station=-stations;station<=stations;station++)
                {
                    var z=station*Profile.WindowPitch;
                    BuildFittedWindow(lining,frame,trim,side,z,cellHalf,shoulder,angles);
                }
                var cellEnd=(stations+.5f)*Profile.WindowPitch;
                if(cellEnd<end)
                {
                    CabinWallBand(lining,side,-end,-cellEnd,shoulder);
                    CabinWallBand(lining,side,cellEnd,end,shoulder);
                }
                // A curved shoulder continues all the way across the crown. Its
                // first edge is identical to the station cell's upper wall edge.
                const int crownSegments=16;
                for(var segment=0;segment<crownSegments;segment++)
                {
                    var a=CabinCrownPoint(side,shoulder,segment*Mathf.PI*.5f/crownSegments);
                    var b=CabinCrownPoint(side,shoulder,(segment+1)*Mathf.PI*.5f/crownSegments);
                    lining.Quad(new Vector3(a.x,a.y,-end),new Vector3(b.x,b.y,-end),
                        new Vector3(b.x,b.y,end),new Vector3(a.x,a.y,end),new[]{
                            CabinCrownNormal(side,shoulder,segment*Mathf.PI*.5f/crownSegments),
                            CabinCrownNormal(side,shoulder,(segment+1)*Mathf.PI*.5f/crownSegments),
                            CabinCrownNormal(side,shoulder,(segment+1)*Mathf.PI*.5f/crownSegments),
                            CabinCrownNormal(side,shoulder,segment*Mathf.PI*.5f/crownSegments)});
                }

                var binWidth=Mathf.Clamp(half*.30f,.34f,.78f);
                var binBottom=Mathf.Max(Profile.WindowHeight*.5f+.14f,Profile.CeilingY-.43f);
                var binOuter=CabinWallX(binBottom,shoulder)-.035f;
                var binInner=binOuter-binWidth;
                var binTop=Mathf.Min(Profile.CeilingY-.06f,binBottom+.32f);
                // Fit the top inboard edge to the narrowing crown, then fit every
                // outer section point; a tall box here would pierce the ceiling.
                while(binTop>binBottom+.14f && CabinSectionHalfWidth(binTop,shoulder)<binInner+.12f)
                    binTop-=.005f;
                CabinBin(lining,side,binInner,binOuter,binBottom,binTop,end,shoulder);
                CabinBinDetails(frame,trim,lamps,side,binInner,binOuter,binBottom,binTop,end);
            }

            if(Profile.SeatGroups.Length==3)
            {
                var seatCount=0; foreach(var count in Profile.SeatGroups) seatCount+=count;
                var seatWidth=(half*2-.20f-Profile.AisleWidth*2)/seatCount;
                var centreWidth=Profile.SeatGroups[1]*seatWidth;
                // Work out the real centre-group location, including asymmetric groups.
                var centre=-half+.10f+Profile.SeatGroups[0]*seatWidth+
                    Profile.AisleWidth+centreWidth*.5f;
                var bottom=Profile.CeilingY-.32f; var top=Profile.CeilingY-.055f;
                var width=centreWidth*.46f;
                foreach(var side in new[]{-1f,1f})
                {
                    // Separate pivot faces above the middle seats, clear of both aisles.
                    var inner=side*centre+.035f;
                    var outer=inner+width;
                    CabinBin(lining,side,inner,outer,bottom,top,end);
                    CabinBinDetails(frame,trim,lamps,side,inner,outer,bottom,top,end);
                }
            }

            // End caps follow the same curved cross-section. The distant end walls
            // intentionally remain simple; local furnishings carry the close detail.
            foreach(var z in new[]{-end,end})
            {
                var perimeter=new List<Vector3>();
                const int capSegments=32;
                for(var i=0;i<=10;i++)
                {
                    var y=Mathf.Lerp(Profile.FloorY,shoulder,i/10f);
                    perimeter.Add(CabinWallPoint(1,y,z,shoulder));
                }
                for(var segment=1;segment<=capSegments;segment++)
                {
                    var angle=segment*Mathf.PI/capSegments;
                    var crown=angle<=Mathf.PI*.5f ? CabinCrownPoint(1,shoulder,angle)
                        : CabinCrownPoint(-1,shoulder,Mathf.PI-angle);
                    perimeter.Add(new Vector3(crown.x,crown.y,z));
                }
                for(var i=1;i<=10;i++)
                {
                    var y=Mathf.Lerp(shoulder,Profile.FloorY,i/10f);
                    perimeter.Add(CabinWallPoint(-1,y,z,shoulder));
                }
                var centre=new Vector3(0,(Profile.FloorY+shoulder)*.5f,z);
                for(var i=0;i<perimeter.Count;i++)
                    lining.Quad(centre,perimeter[i],perimeter[(i+1)%perimeter.Count],
                        perimeter[(i+1)%perimeter.Count]);
            }
            foreach(var pair in buffers)
            {
                var data=pair.Value;
                if(data.Vertices.Count==0) continue;
                var mesh=new Mesh{name="Fitted cabin shell "+pair.Key.name,indexFormat=IndexFormat.UInt32};
                mesh.SetVertices(data.Vertices);mesh.SetNormals(data.Normals);mesh.SetUVs(0,data.Uv);
                mesh.SetTriangles(data.Triangles,0);mesh.RecalculateBounds();_meshes.Add(mesh);
                var host=new GameObject(mesh.name);host.transform.SetParent(transform,false);
                host.AddComponent<MeshFilter>().sharedMesh=mesh;
                var renderer=host.AddComponent<MeshRenderer>();renderer.sharedMaterial=pair.Key;
                renderer.shadowCastingMode=ShadowCastingMode.Off;
            }
        }

        private float CabinWallX(float y,float shoulder)
        {
            // HalfWidth is the exterior pane datum. The lining sits inboard by
            // reveal depth so the reveal ends near the original pane plane.
            // This restrained curved section is an authored fit, not a certified hull.
            var scale=y<0 ? y/Profile.FloorY : y/shoulder;
            return Profile.LiningHalfWidth-(y<0 ? .09f : .12f)*scale*scale;
        }
        private Vector3 CabinWallNormal(float side,float y,float shoulder)
        {
            var extent=y<0 ? Profile.FloorY : shoulder;
            var derivative=-2f*(y<0 ? .09f : .12f)*y/(extent*extent);
            return new Vector3(-side,derivative,0).normalized;
        }
        private Vector2 CabinCrownPoint(float side,float shoulder,float angle)
        {
            var bend=.24f/shoulder*(Profile.CeilingY-shoulder);
            return new Vector2(side*(CabinWallX(shoulder,shoulder)*Mathf.Cos(angle)
                -bend*Mathf.Sin(angle)*Mathf.Cos(angle)),
                shoulder+(Profile.CeilingY-shoulder)*Mathf.Sin(angle));
        }
        private Vector3 CabinCrownNormal(float side,float shoulder,float angle)
        {
            var bend=.24f/shoulder*(Profile.CeilingY-shoulder);
            var dx=-CabinWallX(shoulder,shoulder)*Mathf.Sin(angle)-bend*Mathf.Cos(angle*2);
            var dy=(Profile.CeilingY-shoulder)*Mathf.Cos(angle);
            return new Vector3(-side*dy,dx,0).normalized;
        }
        private Vector3 CabinWallPoint(float side,float y,float z,float shoulder,float inset=0)
            => new Vector3(side*(CabinWallX(y,shoulder)-inset),y,z);

        private static List<float> CabinWindowAngles(float halfCell,float floor,float top)
        {
            var angles=new List<float>();
            for(var segment=0;segment<40;segment++) angles.Add(segment*Mathf.PI*2/40);
            foreach(var y in new[]{floor,top}) foreach(var z in new[]{-halfCell,halfCell})
            { var angle=Mathf.Atan2(y,z);angles.Add(angle<0 ? angle+Mathf.PI*2 : angle); }
            angles.Sort();
            for(var i=angles.Count-1;i>0;i--) if(angles[i]-angles[i-1]<.0001f) angles.RemoveAt(i);
            return angles;
        }
        private Vector2 CabinWindowContour(float angle,float padding)
        {
            var c=Mathf.Cos(angle);var s=Mathf.Sin(angle);
            var power=2f/Profile.WindowSquareness;
            return new Vector2(Mathf.Sign(c)*Mathf.Pow(Mathf.Abs(c),power)*(Profile.WindowWidth*.5f+padding),
                Mathf.Sign(s)*Mathf.Pow(Mathf.Abs(s),power)*(Profile.WindowHeight*.5f+padding));
        }
        private void BuildFittedWindow(CabinShellBuffer lining,CabinShellBuffer frame,CabinShellBuffer trim,
            float side,float z,float cellHalf,float shoulder,List<float> angles)
        {
            for(var i=0;i<angles.Count;i++)
            {
                var a=angles[i];var b=angles[(i+1)%angles.Count];
                Vector3 Cell(float angle)
                {
                    var c=Mathf.Cos(angle);var s=Mathf.Sin(angle);
                    var distance=Mathf.Min(cellHalf/Mathf.Max(.00001f,Mathf.Abs(c)),
                        (s<0 ? -Profile.FloorY : shoulder)/Mathf.Max(.00001f,Mathf.Abs(s)));
                    return CabinWallPoint(side,s*distance,z+c*distance,shoulder);
                }
                Vector3 Ring(float angle,float padding,float inset)
                { var p=CabinWindowContour(angle,padding);return CabinWallPoint(side,p.y,z+p.x,shoulder,inset); }
                // Annular topology covers the complete cell, with shared contour edges.
                var cellA=Cell(a);var cellB=Cell(b);var ringA=Ring(a,.043f,0);var ringB=Ring(b,.043f,0);
                lining.Quad(cellA,cellB,ringB,ringA,new[]{CabinWallNormal(side,cellA.y,shoulder),
                    CabinWallNormal(side,cellB.y,shoulder),CabinWallNormal(side,ringB.y,shoulder),
                    CabinWallNormal(side,ringA.y,shoulder)});
                frame.Quad(Ring(a,.043f,0),Ring(b,.043f,0),Ring(b,.017f,.022f),Ring(a,.017f,.022f));
                frame.Quad(Ring(a,.017f,.022f),Ring(b,.017f,.022f),
                    Ring(b,.006f,-Profile.RevealDepth),Ring(a,.006f,-Profile.RevealDepth));
                trim.Quad(Ring(a,.006f,-Profile.RevealDepth),Ring(b,.006f,-Profile.RevealDepth),
                    Ring(b,0,-Profile.RevealDepth-.006f),Ring(a,0,-Profile.RevealDepth-.006f));
            }
            var lower=-Profile.WindowHeight*.5f-.09f;
            var x=side*(CabinWallX(lower,shoulder)-.018f);
            if(Profile.HasElectronicDimming)
            {
                // Recognisable decorative dimmer; no opaque tint pane and no shade track.
                CabinShellCuboid(frame,new Vector3(x,lower,z),new Vector3(.022f,.045f,.068f));
                CabinShellCuboid(trim,new Vector3(x-side*.014f,lower,z),new Vector3(.009f,.025f,.043f));
            }
            else
            {
                // Small stowed shade pull, with slim tracks outside the clear aperture.
                CabinShellCuboid(frame,new Vector3(x,Profile.WindowHeight*.5f+.026f,z),
                    new Vector3(.025f,.025f,Profile.WindowWidth*.33f));
                foreach(var direction in new[]{-1f,1f})
                    CabinShellCuboid(frame,new Vector3(side*(Profile.LiningHalfWidth-.02f),0,
                        z+direction*(Profile.WindowWidth*.5f+.032f)),
                        new Vector3(.014f,Profile.WindowHeight*.72f,.012f));
            }
        }
        private void CabinWallBand(CabinShellBuffer buffer,float side,float start,float end,float shoulder)
        {
            const int segments=10;
            for(var i=0;i<segments;i++)
            {
                var a=Mathf.Lerp(Profile.FloorY,shoulder,(float)i/segments);
                var b=Mathf.Lerp(Profile.FloorY,shoulder,(float)(i+1)/segments);
                buffer.Quad(CabinWallPoint(side,a,start,shoulder),CabinWallPoint(side,b,start,shoulder),
                    CabinWallPoint(side,b,end,shoulder),CabinWallPoint(side,a,end,shoulder),new[]{
                        CabinWallNormal(side,a,shoulder),CabinWallNormal(side,b,shoulder),
                        CabinWallNormal(side,b,shoulder),CabinWallNormal(side,a,shoulder)});
            }
        }
        private float CabinSectionHalfWidth(float y,float shoulder)
        {
            if(y<=shoulder) return CabinWallX(y,shoulder);
            var angle=Mathf.Asin(Mathf.Clamp01((y-shoulder)/(Profile.CeilingY-shoulder)));
            return CabinCrownPoint(1,shoulder,angle).x;
        }
        private void CabinBin(CabinShellBuffer buffer,float side,float inner,float outer,float bottom,float top,float end,float shoulder=0)
        {
            var section=new[]{new Vector2(inner,top),new Vector2(outer-.025f,top),
                new Vector2(outer,bottom+.11f),new Vector2(outer-.055f,bottom+.025f),
                new Vector2(inner+.10f,bottom),new Vector2(inner+.025f,bottom+.05f),
                new Vector2(inner,bottom+.13f)};
            if(shoulder>0)
                for(var i=0;i<section.Length;i++)
                    section[i]=new Vector2(Mathf.Min(section[i].x,
                        CabinSectionHalfWidth(section[i].y,shoulder)-.025f),section[i].y);
            for(var i=0;i<section.Length;i++)
            {
                var a=section[i];var b=section[(i+1)%section.Length];
                buffer.Quad(new Vector3(side*a.x,a.y,-end),new Vector3(side*b.x,b.y,-end),
                    new Vector3(side*b.x,b.y,end),new Vector3(side*a.x,a.y,end));
            }
            foreach(var z in new[]{-end,end}) for(var i=1;i<section.Length-1;i++)
                buffer.Quad(new Vector3(side*section[0].x,section[0].y,z),
                    new Vector3(side*section[i].x,section[i].y,z),
                    new Vector3(side*section[i+1].x,section[i+1].y,z),
                    new Vector3(side*section[i+1].x,section[i+1].y,z));
        }
        private void CabinBinDetails(CabinShellBuffer frame,CabinShellBuffer trim,CabinShellBuffer lamps,
            float side,float inner,float outer,float bottom,float top,float end)
        {
            CabinShellCuboid(lamps,new Vector3(side*(inner+.035f),top+.012f,0),
                new Vector3(.025f,.015f,end*2-.08f));
            // Economy fittings are authored family treatments. Longer drop/pivot lids
            // distinguish twin-aisle cabins; regional shelves keep compact row modules.
            var modulePitch=Profile.Pitch*(WidebodyCabin ? 2f : 1f);
            var count=Mathf.FloorToInt((end-modulePitch*.47f)/modulePitch);
            for(var module=-count;module<=count;module++)
            {
                var z=module*modulePitch;
                CabinShellCuboid(trim,new Vector3(side*(inner-.003f),(bottom+top)*.5f,z+modulePitch*.43f),
                    new Vector3(.006f,(top-bottom)*.67f,.008f));
                if(Mathf.Abs(z)>Profile.Pitch*2.1f) continue;
                if(Profile.HasElectronicDimming)
                {
                    // Two small recess shoulders frame a wide flush pivot-bin catch.
                    CabinShellCuboid(trim,new Vector3(side*(inner+.008f),bottom+.10f,z),
                        new Vector3(.018f,.026f,.090f));
                    CabinShellCuboid(frame,new Vector3(side*(inner-.004f),bottom+.102f,z),
                        new Vector3(.014f,.013f,.063f));
                }
                else if(AirbusCabin && !RegionalCabin)
                {
                    CabinShellCuboid(trim,new Vector3(side*(inner+.008f),bottom+.12f,z),
                        new Vector3(.018f,.023f,.075f));
                    CabinShellCuboid(frame,new Vector3(side*(inner-.005f),bottom+.12f,z),
                        new Vector3(.014f,.013f,.049f));
                }
                else
                    CabinShellCuboid(trim,new Vector3(side*(inner+.008f),bottom+.12f,z),
                        new Vector3(.022f,RegionalCabin ? .034f : .024f,RegionalCabin ? .045f : .060f));
            }
            var rows=Mathf.Min(2,Mathf.FloorToInt((end-.24f)/Profile.Pitch));
            var bankSeats=outer<Profile.LiningHalfWidth*.65f && WidebodyCabin
                ? (Profile.SeatGroups[1]+1)/2
                : Profile.SeatGroups[side<0 ? 0 : Profile.SeatGroups.Length-1];
            var plateWidth=Mathf.Min((outer-inner)-.045f,.065f*bankSeats+.05f);
            var psuX=side*Mathf.Lerp(inner,outer,.48f);
            for(var row=-rows;row<=rows;row++)
            {
                var z=row*Profile.Pitch;
                CabinShellCuboid(frame,new Vector3(psuX,bottom-.015f,z+.05f),
                    new Vector3(plateWidth,.02f,RegionalCabin ? .19f : .25f));
                for(var seat=0;seat<bankSeats;seat++)
                {
                    var deviceX=psuX+(seat-(bankSeats-1)*.5f)*(plateWidth-.045f)/Mathf.Max(1,bankSeats-1);
                    // Circular gaspers/reading lenses replace identical square pairs.
                    // Family-specific nozzle/lens separation remains purely decorative.
                    CabinPsuDisc(trim,new Vector3(deviceX,bottom-.028f,z+.10f),.017f);
                    CabinPsuDisc(frame,new Vector3(deviceX,bottom-.029f,z+.10f),.009f);
                    CabinPsuDisc(lamps,new Vector3(deviceX,bottom-.030f,
                        z+(Profile.HasElectronicDimming ? -.035f : -.015f)),.012f);
                    if(!RegionalCabin)
                        CabinShellCuboid(trim,new Vector3(deviceX,bottom-.029f,z+.038f),
                            new Vector3(.012f,.006f,.017f));
                }
                if(WidebodyCabin)
                    CabinShellCuboid(trim,new Vector3(psuX,bottom-.028f,z+.155f),
                        new Vector3(plateWidth*.58f,.005f,.008f));
            }
        }

        private static void CabinPsuDisc(CabinShellBuffer buffer,Vector3 centre,float radius)
        {
            const int segments=10;
            for(var segment=0;segment<segments;segment++)
            {
                var a=segment*Mathf.PI*2/segments;var b=(segment+1)*Mathf.PI*2/segments;
                var p=centre+new Vector3(Mathf.Cos(a)*radius,0,Mathf.Sin(a)*radius);
                var q=centre+new Vector3(Mathf.Cos(b)*radius,0,Mathf.Sin(b)*radius);
                buffer.Quad(centre,p,q,q);
            }
        }
        private static void CabinShellCuboid(CabinShellBuffer buffer,Vector3 centre,Vector3 size)
        {
            var a=centre-size*.5f;var b=centre+size*.5f;
            buffer.Quad(new Vector3(a.x,a.y,a.z),new Vector3(b.x,a.y,a.z),new Vector3(b.x,b.y,a.z),new Vector3(a.x,b.y,a.z));
            buffer.Quad(new Vector3(a.x,a.y,b.z),new Vector3(b.x,a.y,b.z),new Vector3(b.x,b.y,b.z),new Vector3(a.x,b.y,b.z));
            buffer.Quad(new Vector3(a.x,a.y,a.z),new Vector3(a.x,a.y,b.z),new Vector3(a.x,b.y,b.z),new Vector3(a.x,b.y,a.z));
            buffer.Quad(new Vector3(b.x,a.y,a.z),new Vector3(b.x,a.y,b.z),new Vector3(b.x,b.y,b.z),new Vector3(b.x,b.y,a.z));
            buffer.Quad(new Vector3(a.x,a.y,a.z),new Vector3(b.x,a.y,a.z),new Vector3(b.x,a.y,b.z),new Vector3(a.x,a.y,b.z));
            buffer.Quad(new Vector3(a.x,b.y,a.z),new Vector3(b.x,b.y,a.z),new Vector3(b.x,b.y,b.z),new Vector3(a.x,b.y,b.z));
        }
    }
}
