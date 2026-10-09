using System;
using System.Collections.Generic;
using System.Globalization;
using Airside.Simulation;

namespace Airside.Presentation
{
    public readonly struct DesktopNotice
    {
        public DesktopNotice(string key, string title, string body)
        { Key = key; Title = title; Body = body; }
        public string Key { get; }
        public string Title { get; }
        public string Body { get; }
    }

    /// <summary>Important player events only; unrelated to routine toast severity or AI traffic.</summary>
    public static class DesktopNotificationPolicy
    {
        public static DesktopNotice? Arrival(FleetEvent e)
        {
            if (!e.IsPlayer || e.State != FleetState.AwaitingStand) return null;
            return new DesktopNotice("arrival:" + e.Registration + ":" + e.At.ElapsedSeconds.ToString("R", CultureInfo.InvariantCulture),
                "Arrival needs a stand", e.Registration + " has landed at Adelaide. Return to Airside to choose a stand.");
        }

        public static DesktopNotice? Settlement(FlightSettlement s)
        {
            if (!s.ContractFulfilled && s.Delay is not { IsLate: true }) return null;
            return new DesktopNotice("settlement:" + s.SettlementId.Key,
                s.ContractFulfilled ? "Contract completed" : "Flight delay", DelayText.SettlementToast(s, 0));
        }

        public static DesktopNotice? Career(CareerEvent e)
        {
            var title = e.Kind switch
            {
                CareerEventKind.ContractExpired => "Contract expired",
                CareerEventKind.GoalComplete => "Career goal completed",
                CareerEventKind.TierReached => "New operating tier",
                CareerEventKind.Finale => "Airline established",
                CareerEventKind.Challenge => "Challenge completed",
                CareerEventKind.Milestone or CareerEventKind.AircraftMilestone => "Milestone reached",
                _ => null
            };
            return title == null ? null : new DesktopNotice("career:" + e.Kind + ":" + e.Registration + ":" + e.Text, title, e.Text);
        }
    }

    /// <summary>Coalesce background bursts, bound memory and avoid repeat banners. Caller supplies real time.</summary>
    public sealed class DesktopNotificationBuffer
    {
        public const float IntervalSeconds = 10f;
        private readonly List<DesktopNotice> _pending = new();
        private readonly Dictionary<string, float> _seen = new(StringComparer.Ordinal);
        private float _nextDelivery;
        private int _overflow;

        public void Enqueue(DesktopNotice notice, float now)
        {
            if (string.IsNullOrEmpty(notice.Key) || string.IsNullOrEmpty(notice.Body)) return;
            if (_seen.TryGetValue(notice.Key, out var at) && now - at < 60f) return;
            if (_seen.Count >= 128) _seen.Clear();
            _seen[notice.Key] = now;
            if (_pending.Count < 8) _pending.Add(notice); else _overflow++;
        }

        public DesktopNotice? Take(float now)
        {
            if (_pending.Count == 0 || now < _nextDelivery) return null;
            _nextDelivery = now + IntervalSeconds;
            var count = _pending.Count + _overflow;
            var result = _pending[0];
            if (count > 1)
            {
                var lines = new List<string>();
                for (var i = 0; i < Math.Min(3, _pending.Count); i++)
                    lines.Add(_pending[i].Title + ": " + _pending[i].Body);
                if (count > lines.Count) lines.Add((count - lines.Count) + " more updates. Open Airside to review your airline.");
                result = new DesktopNotice("batch:" + now.ToString("R", CultureInfo.InvariantCulture), count + " airline updates", string.Join("\n", lines));
            }
            ClearPending();
            return result;
        }

        public void ClearPending() { _pending.Clear(); _overflow = 0; }
    }
}
