using System;
using System.Collections.Generic;
using UnityEngine;

namespace Airside.Presentation
{
    /// <summary>
    /// ADR 0187 — the barrier tape is temporary. It goes up along the route passengers are actually walking (the same
    /// path they follow, round the aircraft to the stair foot) while they are on it, and comes down a few seconds after
    /// the last one has passed, so the apron is clear the rest of the time.
    /// </summary>
    public sealed partial class AirsidePrototype
    {
        /// <summary>Seconds the tape stays up after the last passenger has gone by.</summary>
        private const double WalkwayTapeLingerSeconds = 12.0;

        private sealed class WalkwayTape
        {
            public GameObject Root;
            public Vector3 Start, End;
            public int Count;
            public double UpUntil;
        }

        private readonly Dictionary<string, WalkwayTape> _walkwayTapes = new(StringComparer.Ordinal);
        private readonly List<float> _tapeScratch = new();

        private void KeepWalkwayTape(string registration, WalkPath path)
        {
            var last = Math.Min(path.StairStart, path.Points.Length - 1);
            if (last < 1)
                return;
            if (!_walkwayTapes.TryGetValue(registration, out var tape))
                _walkwayTapes[registration] = tape = new WalkwayTape();
            tape.UpUntil = _preciseTime + WalkwayTapeLingerSeconds;

            var start = path.Points[0];
            var end = path.Points[last];
            if (tape.Root != null && tape.Count == last && (tape.Start - start).sqrMagnitude < 0.25f
                && (tape.End - end).sqrMagnitude < 0.25f)
            {
                if (!tape.Root.activeSelf)
                    tape.Root.SetActive(true);
                return;
            }

            if (tape.Root != null)
                Destroy(tape.Root);
            _tapeScratch.Clear();
            for (var i = 0; i <= last; i++)
            {
                _tapeScratch.Add(path.Points[i].x);
                _tapeScratch.Add(path.Points[i].y);
                _tapeScratch.Add(path.Points[i].z);
            }

            var sink = new RoadMeshSink();
            AdelaideWalkwayGeometry.BuildAlong(sink, _tapeScratch);
            tape.Root = AirsideAdelaideRoadNetworkMesh.BuildProps(BoardingRoot(), $"Boarding tape {registration}", sink);
            tape.Start = start;
            tape.End = end;
            tape.Count = last;
        }

        private void UpdateWalkwayTape()
        {
            foreach (var tape in _walkwayTapes.Values)
                if (tape.Root != null && tape.Root.activeSelf && _preciseTime > tape.UpUntil)
                    tape.Root.SetActive(false);
        }
    }
}
