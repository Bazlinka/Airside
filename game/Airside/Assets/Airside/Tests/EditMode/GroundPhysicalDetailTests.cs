using System;
using System.Linq;
using Airside.Presentation;
using NUnit.Framework;

namespace Airside.Tests
{
    public sealed class GroundPhysicalDetailTests
    {
        [Test]
        public void VergeSites_AreNonEmptyBoundedAndClearOfGroundRoads()
        {
            var sites=AdelaideVergeDetail.Sites();
            Assert.That(sites.Count,Is.InRange(1,AdelaideVergeDetail.MaxClusters));
            foreach(var site in sites)
                Assert.That(AdelaideVergeDetail.ClearSite(site.X,site.Z),Is.True);
            var sink=new RoadMeshSink();
            Assert.That(AdelaideVergeDetail.Build(sink,new RoadBuildOptions()),Is.EqualTo(sites.Count));
            Assert.That(sink.TriangleCount,Is.EqualTo(sites.Count*AdelaideVergeDetail.TrianglesPerCluster));
            // Verify the actual rendered footprint fits the radius used for clearance,
            // including leaned blade tips rather than checking only placement centres.
            foreach(var pair in sink.Tiles)
            for(var i=0;i<pair.Value.Positions.Count;i+=3)
            {
                var x=pair.Value.Positions[i]; var z=pair.Value.Positions[i+2];
                var nearest=sites.Min(s=>(s.X-x)*(s.X-x)+(s.Z-z)*(s.Z-z));
                Assert.That(nearest,Is.LessThanOrEqualTo(AdelaideVergeDetail.ClusterRadius*AdelaideVergeDetail.ClusterRadius));
            }
        }

        [Test]
        public void DrainMetal_FitsThePitAndStaysFlush()
        {
            foreach(var pit in ApronSurfaceWear.All().Where(p=>p.Drainage))
            {
                const float y=.043f;
                var boxes=ApronDrainGeometry.Boxes(pit,y);
                Assert.That(boxes.Count,Is.EqualTo(4+ApronDrainGeometry.GrateBars));
                var angle=pit.YawDegrees*Math.PI/180;
                var cos=(float)Math.Cos(angle);var sin=(float)Math.Sin(angle);
                foreach(var box in boxes)
                {
                    Assert.That(box.Top-y,Is.InRange(0f,.005f));
                    for(var a=-1;a<=1;a+=2)
                    for(var b=-1;b<=1;b+=2)
                    {
                        var x=box.X+box.DirX*box.Length*.5f*a-box.DirZ*box.Depth*.5f*b-pit.CentreX;
                        var z=box.Z+box.DirZ*box.Length*.5f*a+box.DirX*box.Depth*.5f*b-pit.CentreZ;
                        Assert.That(Math.Abs(x*cos+z*sin),Is.LessThanOrEqualTo(pit.HalfX+.001f));
                        Assert.That(Math.Abs(-x*sin+z*cos),Is.LessThanOrEqualTo(pit.HalfZ+.001f));
                    }
                }
            }
        }

        [Test]
        public void ThinSolidFaces_HaveOpposedGeometricNormalsAndNoDegenerates()
        {
            var sink=new RoadMeshSink();
            sink.SolidTriangle(0,0,0,1,0,0,0,1,.1f,new RoadColor(1,1,1,1),twoSided:true);
            Assert.That(sink.TriangleCount,Is.EqualTo(2));
            var tile=sink.Tiles.First().Value;
            for(var axis=0;axis<3;axis++)
                Assert.That(tile.Normals[axis],Is.EqualTo(-tile.Normals[9+axis]).Within(.0001f));
            sink.SolidTriangle(0,0,0,0,0,0,0,0,0,new RoadColor(1,1,1,1));
            Assert.That(sink.TriangleCount,Is.EqualTo(2));
        }
    }
}
