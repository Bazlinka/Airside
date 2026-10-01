using System.Collections.Generic;

namespace Airside.Presentation
{
    /// <summary>The standard crew callouts a pilot hears on takeoff, approach and landing, derived from
    /// observed speed, wheel height and vertical speed: "80 KNOTS", "V1", "ROTATE", "POSITIVE RATE",
    /// "GEAR UP"; "1000", "500", "100 ABOVE", "50", "40", "30", "20 RETARD", "10"; then "SPOILERS",
    /// "REVERSE GREEN" (jets) and "80 KNOTS" on the rollout. Each fires once per takeoff or landing.
    /// Text only; independent of Unity and of the simulation.</summary>
    public sealed class CockpitCallouts
    {
        public struct Sample
        {
            public float GroundKnots, RotateKnots, HeightFeet, VerticalFeetPerMinute;
            public bool Jet;
        }

        private readonly HashSet<string> _fired = new();
        private bool _started, _airborne, _landed;
        private float _highestFeet, _sinceTouchdown;
        private float _previousKnots;
        /// <summary>Simulation seconds since the last step.</summary>
        public float DeltaSeconds { get; set; } = 1f / 60f;

        public void Reset()
        {
            _fired.Clear();
            _started = _airborne = _landed = false;
            _highestFeet = _sinceTouchdown = _previousKnots = 0f;
        }

        /// <summary>Returns a callout that has just become due, or null.</summary>
        public string Step(Sample s)
        {
            var onGround = s.HeightFeet < 1.2f;
            if (!_started) { _started = true; _airborne = !onGround; _previousKnots = s.GroundKnots; }
            string call = null;
            string Once(string key, string text) { if (call != null || !_fired.Add(key)) return null; call = text; return text; }

            if (_airborne && onGround)
            {                                  // touchdown: forget takeoff calls and the approach
                _airborne = false; _landed = true; _sinceTouchdown = 0f; _fired.Clear(); _highestFeet = 0f;
            }
            else if (!_airborne && !onGround)
            {                                  // lift-off: forget landing calls
                _airborne = true; _landed = false; _fired.RemoveWhere(k => k.StartsWith("L-")); _highestFeet = 0f;
            }

            if (_airborne)
            {
                if (s.HeightFeet > _highestFeet) _highestFeet = s.HeightFeet;
                if (s.VerticalFeetPerMinute > 200f && _highestFeet < 1500f && _fired.Contains("T-ROT"))
                {
                    if (s.HeightFeet >= 12f) Once("T-PR", "POSITIVE RATE");
                    if (s.HeightFeet >= 60f && _fired.Contains("T-PR")) Once("T-GU", "GEAR UP");
                }
                if (s.VerticalFeetPerMinute < -200f && _highestFeet > 1100f)
                {
                    var h = s.HeightFeet;
                    if (h <= 1000f) Once("A-1000", "1,000");
                    if (h <= 500f && _fired.Contains("A-1000")) Once("A-500", "500");
                    if (h <= 100f && _fired.Contains("A-500")) Once("A-100", "100 ABOVE");
                    if (h <= 50f) Once("L-50", "50");
                    if (h <= 40f && _fired.Contains("L-50")) Once("L-40", "40");
                    if (h <= 30f && _fired.Contains("L-40")) Once("L-30", "30");
                    if (h <= 20f && _fired.Contains("L-30")) Once("L-20", s.Jet ? "20 RETARD" : "20");
                    if (h <= 10f && _fired.Contains("L-20")) Once("L-10", "10");
                }
            }
            else
            {
                _sinceTouchdown += DeltaSeconds;
                if (!_landed)
                {
                    if (s.GroundKnots >= 80f && _previousKnots < 80f) Once("T-80", "80 KNOTS");
                    if (s.RotateKnots > 0f)
                    {
                        if (s.GroundKnots >= s.RotateKnots - 4f) Once("T-V1", "V1");
                        if (s.GroundKnots >= s.RotateKnots && _fired.Contains("T-V1")) Once("T-ROT", "ROTATE");
                    }
                }
                else
                {
                    if (s.Jet && _sinceTouchdown > 0.4f) Once("R-SPOILERS", "SPOILERS");
                    if (s.Jet && _sinceTouchdown > 1.2f && _fired.Contains("R-SPOILERS")) Once("R-REVERSE", "REVERSE GREEN");
                    if (s.GroundKnots <= 80f && _previousKnots > 80f) Once("R-80", "80 KNOTS");
                }
            }
            _previousKnots = s.GroundKnots;
            return call;
        }
    }
}
