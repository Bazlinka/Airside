using System;

namespace Airside.Presentation
{
    /// <summary>Road-wheel roles and axle frame, shared by runtime collection and headless checks.</summary>
    public static class ServiceVehicleWheelGeometry
    {
        public static bool IsRoadWheel(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;
            name = name.ToLowerInvariant();
            if (name == "gpu wheel l" || name == "gpu wheel r") return true;
            foreach (var end in new[] { "fl", "fr", "ml", "mr", "rl", "rr" })
                if (name == "wheel_" + end || name.EndsWith(" wheel_" + end, StringComparison.Ordinal)
                    || name.EndsWith(" wheel " + end, StringComparison.Ordinal)
                    || name == "wheel_hub_" + end || name.EndsWith(" wheel_hub_" + end, StringComparison.Ordinal))
                    return true;
            for (var cart = 1; cart <= 3; cart++)
                foreach (var side in new[] { "l", "r" })
                    if (name == "cart_wheel_" + cart + side
                        || name.EndsWith(" cart_wheel_" + cart + side, StringComparison.Ordinal)) return true;
            return false;
        }

        /// <summary>Thin dimension is the axle; authored kits use Z, prefab mesh frames can differ.</summary>
        public static int AxleAxis(float width, float height, float depth) =>
            depth <= width && depth <= height ? 2 : width <= height ? 0 : 1;

        public static float Centre(float low, float high) => (low + high) * 0.5f;
    }
}
