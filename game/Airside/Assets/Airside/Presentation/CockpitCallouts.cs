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

        private static (string Key, float Height, string Text)[] Heights(bool jet) => new[]
        {
            ("A-1000", 1000f, "1,000"), ("A-500", 500f, "500"), ("A-100", 100f, "100 ABOVE"),
            ("L-50", 50f, "50"), ("L-40", 40f, "40"), ("L-30", 30f, "30"),
            ("L-20", 20f, jet ? "20 RETARD" : "20"), ("L-10", 10f, "10"),
        };

        private float _lowestSinceTouchdown;

        /// <summary>Returns a callout that has just become due, or null.</summary>
        public string Step(Sample s)
        {
            var onGround = s.HeightFeet < 1.2f;
            if (!_started) { _started = true; _airborne = !onGround; _previousKnots = s.GroundKnots; }
            string call = null;
            string Once(string key, string text) { if (call != null || !_fired.Add(key)) return null; call = text; return text; }

            if (_airborne && onGround)
            {                                  // touchdown: forget takeoff calls and the approach
                _airborne = false; _landed = true; _sinceTouchdown = 0f; _lowestSinceTouchdown = s.GroundKnots; _fired.Clear(); _highestFeet = 0f;
            }
            else if (!_airborne && !onGround)
            {                                  // lift-off: forget landing calls
                _airborne = true; _landed = false; _fired.RemoveWhere(k => k.StartsWith("L-")); _highestFeet = 0f;
            }

            if (_airborne)
            {
                if (s.HeightFeet > _highestFeet) _highestFeet = s.HeightFeet;
                if (s.VerticalFeetPerMinute > 300f)
                {                              // climbing: a go-around (or lift-off) resets the approach calls
                    _fired.RemoveWhere(k => k.StartsWith("A-") || k.StartsWith("L-"));
                    if (_highestFeet < 1500f && _fired.Contains("T-ROT"))
                    {
                        if (s.HeightFeet >= 12f) Once("T-PR", "POSITIVE RATE");
                        if (s.HeightFeet >= 60f && _fired.Contains("T-PR")) Once("T-GU", "GEAR UP");
                    }
                }
                else if (s.VerticalFeetPerMinute < 100f && _highestFeet > 1100f)
                {
                    // Descending (a flare can slow the sink below 100 ft/min, so no sink-rate floor).
                    // Everything already passed is marked; only the lowest newly crossed height is called.
                    foreach (var (key, height, text) in Heights(s.Jet))
                    {
                        if (s.HeightFeet > height || !_fired.Add(key)) continue;
                        call = text;
                    }
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
                    if (s.GroundKnots < _lowestSinceTouchdown) _lowestSinceTouchdown = s.GroundKnots;
                    if (s.GroundKnots > _lowestSinceTouchdown + 10f && s.GroundKnots > 40f)
                    {                              // accelerating again: a touch-and-go, so this is a takeoff roll
                        _landed = false; _fired.Clear();
                    }
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
