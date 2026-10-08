using System;

namespace Airside.Presentation
{
    /// <summary>
    /// Bank angle of a coordinated turn: tan(bank) = V * yawRate / g. Using the real ground speed and heading
    /// rate (both per simulated second) makes a fast jet in a gentle turn bank as much as a slow turboprop in
    /// a tight one, and keeps the answer the same at any clock speed. Negative bank is a right turn, the
    /// codebase's convention. Pure so the headless harness can check it.
    /// </summary>
    public static class CoordinatedTurn
    {
        public const float Gravity = 9.80665f;
        /// <summary>Airline-style limit; nothing here flies steeper than a normal 25 degree turn.</summary>
        public const float MaxBankDegrees = 25f;
        /// <summary>A single sample faster than this is a jump (origin shift, teleport), not flight.</summary>
        public const float MaxPlausibleMetresPerSecond = 400f;

        public static float BankDegrees(float groundSpeedMetresPerSecond, float yawRateDegreesPerSecond, float limitDegrees = MaxBankDegrees)
        {
            var speed = Math.Min(Math.Max(groundSpeedMetresPerSecond, 0f), MaxPlausibleMetresPerSecond);
            var bank = (float)(Math.Atan(speed * yawRateDegreesPerSecond * Math.PI / 180.0 / Gravity) * 180.0 / Math.PI);
            return -Math.Min(Math.Max(bank, -limitDegrees), limitDegrees);
        }

        /// <summary>Heading in degrees (clockwise from +Z) of a horizontal direction.</summary>
        public static float YawDegrees(float dirX, float dirZ) => (float)(Math.Atan2(dirX, dirZ) * 180.0 / Math.PI);

        public static float DeltaAngle(float from, float to)
        {
            var d = (to - from) % 360f;
            if (d > 180f) d -= 360f;
            if (d < -180f) d += 360f;
            return d;
        }
    }
}
