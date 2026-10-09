using System;
using System.Collections.Generic;
using Airside.Simulation;

namespace Airside.Presentation
{
    /// <summary>Small original grass clumps around existing mapped roadside planting.</summary>
    public static class AdelaideVergeDetail
    {
        public const int MaxClusters = 160;
        public const int TrianglesPerCluster = 24;
        public const float ClusterRadius = 1.6f;
        public const float RoadMargin = 2f;
        private static Site[] _cached;

        public readonly struct Site
        {
            public readonly float X, Z, Seed;
            public Site(float x, float z, float seed) { X=x; Z=z; Seed=seed; }
        }

        public static IReadOnlyList<Site> Sites()
        {
            if (_cached != null) return _cached;
            var result = new List<Site>();
            // These are existing mapped landside avenue/windbreak locations, not
            // a new random scatter across the airport or residential blocks.
            foreach (var tree in AdelaideAvenuePlacement.Sites()) Add(tree.X,tree.Z,tree.YawRad,result);
            foreach (var tree in AdelaideWindbreakPlacement.Sites()) Add(tree.X,tree.Z,tree.YawRad,result);
            _cached=result.ToArray();
            return _cached;
        }

        private static void Add(float x,float z,float angle,List<Site> result)
        {
            if (result.Count >= MaxClusters) return;
            var seed=Frac(x*.137f+z*.071f);
            angle += seed*6.2831853f;
            x += (float)Math.Cos(angle)*2.2f; z += (float)Math.Sin(angle)*2.2f;
            if (!ClearSite(x,z)) return;
            foreach (var old in result)
                if ((old.X-x)*(old.X-x)+(old.Z-z)*(old.Z-z)<16f) return;
            result.Add(new Site(x,z,seed));
        }

        public static bool ClearSite(float x,float z)
        {
            var outline=AdelaideBoundary.Outline;
            if (AdelaideWindbreakPlacement.InsideOutline(outline,x,z)
                || AdelaideWindbreakPlacement.DistanceToOutline(outline,x,z)<ClusterRadius+3f
                || AirsideAdelaidePavement.DistanceToPavement(x,z)<ClusterRadius+5f) return false;
            var cover=AdelaideLandCover.Sample(x,z);
            if (cover!=AdelaideLandCover.Kind.None && cover!=AdelaideLandCover.Kind.Park
                && cover!=AdelaideLandCover.Kind.Scrub && cover!=AdelaideLandCover.Kind.Sand) return false;
            // Check every nearby mapped road, not just the parent avenue. Raised
            // bridges do not occupy this ground; ground roads include service lanes.
            foreach (var road in AdelaideRoadNetwork.Roads)
            {
                if (road.Layer!=0) continue;
                var clearance=road.Width*.5f+ClusterRadius+RoadMargin;
                for (var k=0;k+1<road.PointCount;k++)
                {
                    var i=(road.PointStart+k)*2;
                    var ax=AdelaideRoadNetwork.Points[i]; var az=AdelaideRoadNetwork.Points[i+1];
                    var bx=AdelaideRoadNetwork.Points[i+2]; var bz=AdelaideRoadNetwork.Points[i+3];
                    if (x<Math.Min(ax,bx)-clearance || x>Math.Max(ax,bx)+clearance
                        || z<Math.Min(az,bz)-clearance || z>Math.Max(az,bz)+clearance) continue;
                    var dx=bx-ax; var dz=bz-az; var len=dx*dx+dz*dz;
                    var t=len<1e-6f ? 0f : Math.Max(0f,Math.Min(1f,((x-ax)*dx+(z-az)*dz)/len));
                    dx=ax+dx*t-x; dz=az+dz*t-z;
                    if (dx*dx+dz*dz<clearance*clearance) return false;
                }
            }
            return true;
        }

        public static int Build(RoadMeshSink sink,RoadBuildOptions options)
        {
            var sites=Sites();
            foreach (var site in sites)
            for (var tuft=0;tuft<4;tuft++)
            {
                var seed=Frac(site.Seed*7.31f+tuft*.317f);
                var angle=seed*6.2831853f;
                var radius=.35f+seed*.75f;
                var x=site.X+(float)Math.Cos(angle)*radius;
                var z=site.Z+(float)Math.Sin(angle)*radius;
                var y=options.Height(x,z)+.015f;
                var colour=RoadColor.Srgb(.39f+seed*.19f,.42f+seed*.12f,.23f+seed*.09f,1f);
                for (var leaf=0;leaf<3;leaf++)
                {
                    var yaw=angle+leaf*2.094395f;
                    var ux=(float)Math.Cos(yaw); var uz=(float)Math.Sin(yaw);
                    var height=.18f+Frac(seed*3.7f+leaf*.29f)*.24f;
                    var width=.045f+seed*.035f;
                    sink.SolidTriangle(x-ux*width,y,z-uz*width,x+ux*width,y,z+uz*width,
                        x-uz*.12f,y+height,z+ux*.12f,colour,twoSided:true);
                }
            }
            return sites.Count;
        }

        private static float Frac(float value) => value-(float)Math.Floor(value);
    }
}
