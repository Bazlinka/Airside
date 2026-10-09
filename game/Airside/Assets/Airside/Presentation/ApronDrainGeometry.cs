using System;
using System.Collections.Generic;

namespace Airside.Presentation
{
    /// <summary>Flush rim and grate bars fitted wholly inside existing drainage pits.</summary>
    public static class ApronDrainGeometry
    {
        public const float RimWidth = .045f;
        public const float MetalHeight = .012f;
        public const int GrateBars = 6;

        public static IReadOnlyList<DetailBox> Boxes(ApronWearMark pit,float pitY)
        {
            var result=new List<DetailBox>();
            if (!pit.Drainage) return result;
            var angle=pit.YawDegrees*(float)Math.PI/180f;
            var ux=(float)Math.Cos(angle); var uz=(float)Math.Sin(angle);
            var vx=-uz; var vz=ux;
            var y=pitY-.002f;
            void Box(float along,float across,float length,float depth)
            {
                result.Add(new DetailBox(BuildingPart.Trim,pit.CentreX+ux*along+vx*across,y,
                    pit.CentreZ+uz*along+vz*across,length,MetalHeight,depth,ux,uz));
            }
            Box(0,-pit.HalfZ+RimWidth*.5f,pit.HalfX*2,RimWidth);
            Box(0,pit.HalfZ-RimWidth*.5f,pit.HalfX*2,RimWidth);
            Box(-pit.HalfX+RimWidth*.5f,0,RimWidth,pit.HalfZ*2-RimWidth*2);
            Box(pit.HalfX-RimWidth*.5f,0,RimWidth,pit.HalfZ*2-RimWidth*2);
            var half=pit.HalfZ-RimWidth;
            for (var bar=0;bar<GrateBars;bar++)
                Box(0,-half+(bar+1)*(half*2)/(GrateBars+1),pit.HalfX*2-RimWidth*2,.025f);
            return result;
        }
    }
}
