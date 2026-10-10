using System;

namespace Airside.Simulation
{
    /// <summary>
    /// The jet fuel price (Economy v2). A pure, seeded function of the Adelaide day, so nothing is saved, every replay
    /// and away catch-up agrees, and booking and cancelling a flight on the same departure day see the same number.
    /// The price drifts smoothly between random weekly-ish knots around <see cref="FlightCostModel.BaselineFuelPerKg"/>;
    /// Demanding widens the swing (roadmap assumption 6). Tuned (tier D) design constants.
    /// </summary>
    public static class FuelPrice
    {
        /// <summary>Days between random knots; the price is interpolated between them.</summary>
        public const int KnotDays = 9;

        /// <summary>Largest swing from the baseline, as a fraction.</summary>
        public static double Swing(CareerDifficulty difficulty) => difficulty == CareerDifficulty.Demanding ? 0.40 : 0.15;

        /// <summary>A$ per kg of jet fuel on this Adelaide day.</summary>
        public static double PerKg(long day, CareerDifficulty difficulty)
        {
            var knot = FloorDiv(day, KnotDays);
            var t = (day - knot * KnotDays) / (double)KnotDays;
            t = t * t * (3 - 2 * t);
            var level = Knot(knot) * (1 - t) + Knot(knot + 1) * t;
            return FlightCostModel.BaselineFuelPerKg * (1 + Swing(difficulty) * level);
        }

        /// <summary>The price relative to the baseline (1.0 = baseline).</summary>
        public static double Ratio(long day, CareerDifficulty difficulty) =>
            PerKg(day, difficulty) / FlightCostModel.BaselineFuelPerKg;

        private static long FloorDiv(long a, long b) => a >= 0 ? a / b : -((-a + b - 1) / b);

        private static double Knot(long k)
        {
            var rng = new SeededRandomSource((uint)((k * 2_246_822_519L % 4_294_967_291L + 4_294_967_291L) % 4_294_967_291L + 7));
            return (rng.NextInt(0, 2001) - 1000) / 1000.0;
        }
    }
}
