using System;
using Airside.Domain;

namespace Airside.Simulation
{
    /// <summary>
    /// Aerodrome tower / ground logic layered on top of the reservation table.
    /// Keeps runway separation, arrival priority over departures, and ATC-style
    /// phraseology for the ops log and HUD. Deterministic: only uses simulation
    /// time and flight state — never frame timing or Unity types.
    /// </summary>
    public sealed class AerodromeAtc
    {
        /// <summary>Seconds the runway stays "hot" after vacating before the next clearance.</summary>
        public const long RunwaySeparationSeconds = 12;

        /// <summary>Approach remaining time at which a departure must yield for the arrival.</summary>
        public const long ArrivalPriorityWindowSeconds = 8;

        /// <summary>Approach remaining time at which a later arrival is told number two.</summary>
        public const long ApproachNumberTwoWindowSeconds = 18;

        /// <summary>Approach remaining time when ATC asks for a mid-downwind report.</summary>
        public const long MidDownwindReportSeconds = 16;

        /// <summary>Approach wait (seconds past overdue) before tower issues a go-around.</summary>
        public const long GoAroundAfterSeconds = 16;

        /// <summary>Seconds after separation opens during which landing clearances warn of wake.</summary>
        public const long WakeCautionWindowSeconds = 8;

        /// <summary>Separation remaining at/under which tower issues a conditional land-when-vacated.</summary>
        public const long ConditionalLandingWindowSeconds = 5;

        /// <summary>Separation remaining during which tower may issue line up and wait.</summary>
        public const long LineUpAndWaitWindowSeconds = 6;

        /// <summary>Seconds of number-two wait before tower asks for a left orbit.</summary>
        public const long OrbitAfterNumberTwoSeconds = 8;

        private long _runwayAvailableAt;
        private string _lastInstruction = "Kingscote Tower — frequency open";
        private string _lastClearedFlight = string.Empty;
        private AtcClearance _active = AtcClearance.None;

        public string LastInstruction => _lastInstruction;
        public string LastClearedFlight => _lastClearedFlight;
        public AtcClearance ActiveClearance => _active;

        public bool RunwaySeparationOpen(SimulationTime now) =>
            now.ElapsedSeconds >= _runwayAvailableAt;

        public long SeparationRemainingSeconds(SimulationTime now) =>
            Math.Max(0, _runwayAvailableAt - now.ElapsedSeconds);

        public void NotifyRunwayVacated(SimulationTime now, string callsign = null)
        {
            _runwayAvailableAt = now.ElapsedSeconds + RunwaySeparationSeconds;
            _active = AtcClearance.RunwayVacated;
            if (!string.IsNullOrWhiteSpace(callsign))
            {
                _lastClearedFlight = callsign;
                _lastInstruction =
                    $"{callsign}, runway vacated via Alpha, first available, caution wake for following traffic, separation {RunwaySeparationSeconds}s, surface wind calm, QNH 1013, contact Kingscote Ground, expect taxi to stand";
            }
            else
            {
                _lastInstruction = $"Runway 09 vacated — separation {RunwaySeparationSeconds}s, wake caution active, QNH 1013";
            }
        }

        public string IssueReadyForDeparture(string callsign)
        {
            _active = AtcClearance.ReadyForDeparture;
            _lastClearedFlight = callsign ?? string.Empty;
            _lastInstruction =
                $"{callsign}, roger ready, hold short runway 09 at holding point Alpha, surface wind calm, QNH 1013, remain this frequency, expect departure clearance when number one, report when number one";
            return _lastInstruction;
        }

        public bool IsWakeCautionActive(SimulationTime now) =>
            now.ElapsedSeconds > 0
            && now.ElapsedSeconds >= _runwayAvailableAt
            && now.ElapsedSeconds - _runwayAvailableAt <= WakeCautionWindowSeconds;

        public string IssueClearedToLand(string callsign, WeatherKind weather = WeatherKind.Clear) =>
            IssueClearedToLand(callsign, weather, default);

        public string IssueClearedToLand(string callsign, WeatherKind weather, SimulationTime now) =>
            IssueClearedToLand(callsign, weather, now, conditional: IsWakeCautionActive(now));

        public string IssueClearedToLand(
            string callsign, WeatherKind weather, SimulationTime now, bool conditional)
        {
            _active = AtcClearance.ClearedToLand;
            _lastClearedFlight = callsign ?? string.Empty;
            var remark = WeatherRemark(weather);
            if (conditional)
            {
                _lastInstruction =
                    $"{callsign}, number one, cleared to land runway 09, traffic vacating via Alpha, caution wake turbulence, vacate via Alpha when able, report runway vacated, remain this frequency, {remark}";
            }
            else
            {
                _lastInstruction =
                    $"{callsign}, number one, cleared to land runway 09, runway is clear, vacate via Alpha when able, report runway vacated, remain this frequency, {remark}";
            }

            return _lastInstruction;
        }

        public string IssueReportEstablished(string callsign)
        {
            _active = AtcClearance.ReportEstablished;
            _lastClearedFlight = callsign ?? string.Empty;
            _lastInstruction =
                $"{callsign}, report established final runway 09, continue approach, surface wind calm, QNH 1013, number one expected, runway is clear, expect landing clearance, report short final, vacate via Alpha when landed";
            return _lastInstruction;
        }

        public string IssueShortFinal(string callsign)
        {
            _active = AtcClearance.ShortFinal;
            _lastClearedFlight = callsign ?? string.Empty;
            _lastInstruction =
                $"{callsign}, short final runway 09, surface wind calm, QNH 1013, number one, runway is clear, cleared to land expected shortly, vacate via Alpha when able, report runway vacated, acknowledge";
            return _lastInstruction;
        }

        public string IssueClearedForTakeoff(string callsign, WeatherKind weather = WeatherKind.Clear) =>
            IssueClearedForTakeoff(callsign, weather, default);

        public string IssueClearedForTakeoff(string callsign, WeatherKind weather, SimulationTime now)
        {
            var fromLuaw = _active == AtcClearance.LineUpAndWait;
            _active = AtcClearance.ClearedForTakeoff;
            _lastClearedFlight = callsign ?? string.Empty;
            var afterLanding = now.ElapsedSeconds > 0
                && now.ElapsedSeconds >= _runwayAvailableAt
                && now.ElapsedSeconds - _runwayAvailableAt <= WakeCautionWindowSeconds;
            var remark = WeatherRemark(weather);
            if (fromLuaw)
            {
                _lastInstruction = afterLanding
                    ? $"{callsign}, number one, cleared for takeoff runway 09, from line up and wait, after the landing, climb straight ahead to circuit height 1000 ft, no turns below circuit height, report airborne when able, {remark}"
                    : $"{callsign}, number one, cleared for takeoff runway 09, from line up and wait, climb straight ahead to circuit height 1000 ft, no turns below circuit height, report airborne when able, {remark}";
            }
            else
            {
                _lastInstruction = afterLanding
                    ? $"{callsign}, number one, cleared for takeoff runway 09 from Alpha, after the landing, climb straight ahead to circuit height 1000 ft, no turns below circuit height, report airborne when able, {remark}"
                    : $"{callsign}, number one, cleared for takeoff runway 09 from Alpha, climb straight ahead to circuit height 1000 ft, no turns below circuit height, report airborne when able, {remark}";
            }

            return _lastInstruction;
        }

        public string IssueReportAirborne(string callsign)
        {
            // Keep ClearedForTakeoff as the HUD clearance while still on the roll.
            if (_active != AtcClearance.ClearedForTakeoff)
                _active = AtcClearance.ReportAirborne;
            _lastClearedFlight = callsign ?? string.Empty;
            _lastInstruction =
                $"{callsign}, report airborne when able, climb runway heading to circuit height 1000 ft, no turns below circuit height, wind calm, QNH 1013, report frequency change when clear of the circuit";
            return _lastInstruction;
        }

        public string IssueContactGround(string callsign)
        {
            _active = AtcClearance.ContactGround;
            _lastClearedFlight = callsign ?? string.Empty;
            _lastInstruction =
                $"{callsign}, runway vacated, contact Kingscote Ground on this frequency, taxi via Alpha to the apron, hold short of the stand until marshaller, caution vehicles on the apron, QNH 1013, report on stand when parked";
            return _lastInstruction;
        }

        public string IssueOnStand(string callsign, string standLabel)
        {
            _active = AtcClearance.OnStand;
            _lastClearedFlight = callsign ?? string.Empty;
            _lastInstruction =
                $"{callsign}, marshaller in sight, taxi onto {standLabel}, welcome to Kingscote, parking brake set, shutdown approved, chocks in, surface wind calm, QNH 1013, report engines stopped, remain this frequency for departure";
            return _lastInstruction;
        }

        public string IssueTaxiToStand(string callsign, string standLabel, bool expedite = false)
        {
            _active = AtcClearance.TaxiToStand;
            _lastClearedFlight = callsign ?? string.Empty;
            _lastInstruction = expedite
                ? $"{callsign}, vacate via Alpha, expedite, taxi via Alpha to {standLabel}, traffic on final, caution vehicles on the apron, surface wind calm, QNH 1013, report on stand when parked, remain this frequency"
                : $"{callsign}, vacate via Alpha, taxi via Alpha to {standLabel}, hold short of the stand until marshaller, caution vehicles on the apron, surface wind calm, QNH 1013, report on stand when parked, remain this frequency";
            return _lastInstruction;
        }

        public string IssueTaxiToHoldShort(string callsign) =>
            IssueTaxiToHoldShort(callsign, WeatherKind.Clear);

        public string IssueTaxiToHoldShort(string callsign, WeatherKind weather)
        {
            _active = AtcClearance.TaxiToHoldShort;
            _lastClearedFlight = callsign ?? string.Empty;
            var caution = weather switch
            {
                WeatherKind.Rain or WeatherKind.Storm => ", caution wet surface",
                WeatherKind.Fog => ", caution reduced visibility",
                _ => string.Empty
            };
            _lastInstruction =
                $"{callsign}, taxi via Alpha to holding point runway 09, hold short at Alpha, caution vehicles on the apron, surface wind calm, QNH 1013{caution}, report ready for departure";
            return _lastInstruction;
        }

        public string IssueReportRunwayVacated(string callsign)
        {
            // Do not displace ClearedToLand while the aircraft is still on the runway —
            // the ops HUD should keep showing the landing clearance.
            if (_active != AtcClearance.ClearedToLand)
                _active = AtcClearance.ReportVacated;
            _lastClearedFlight = callsign ?? string.Empty;
            _lastInstruction = $"{callsign}, when vacated via Alpha report runway vacated, first available taxiway, caution wake for following traffic, QNH 1013";
            return _lastInstruction;
        }

        public string IssueEngineStartApproved(string callsign)
        {
            _active = AtcClearance.EngineStart;
            _lastClearedFlight = callsign ?? string.Empty;
            _lastInstruction =
                $"{callsign}, engine start approved, park brake set, surface wind calm, QNH 1013, advise ready for pushback, contact Ground when ready for taxi via Alpha, remain this frequency";
            return _lastInstruction;
        }

        public string IssuePushbackApproved(string callsign)
        {
            _active = AtcClearance.PushbackApproved;
            _lastClearedFlight = callsign ?? string.Empty;
            _lastInstruction =
                $"{callsign}, pushback approved, face west toward Alpha, advise ready for taxi via Alpha, caution jet blast behind, caution vehicles on the apron, QNH 1013, report when clear of the stand";
            return _lastInstruction;
        }

        public string IssueTrafficAdvisory(string callsign, string traffic)
        {
            _active = AtcClearance.TrafficAdvisory;
            _lastClearedFlight = callsign ?? string.Empty;
            _lastInstruction = string.IsNullOrWhiteSpace(traffic)
                ? $"{callsign}, traffic advisory, hold short runway 09 at holding point Alpha, surface wind calm, QNH 1013, number two for departure, report ready when clear, expect further clearance"
                : $"{callsign}, traffic {traffic}, hold short runway 09 at holding point Alpha, surface wind calm, QNH 1013, number two for departure, report ready when clear, expect further clearance";
            return _lastInstruction;
        }

        public string IssueHoldShortRunway(string callsign, string reason, string traffic = null)
        {
            _active = AtcClearance.HoldShortRunway;
            _lastClearedFlight = callsign ?? string.Empty;
            var trafficPhrase = string.IsNullOrWhiteSpace(traffic)
                ? string.Empty
                : $", traffic {traffic}";
            _lastInstruction = string.IsNullOrWhiteSpace(reason)
                ? $"{callsign}, hold short runway 09 at holding point Alpha{trafficPhrase}, surface wind calm, QNH 1013, report ready when clear"
                : $"{callsign}, hold short runway 09 at holding point Alpha — {reason}{trafficPhrase}, surface wind calm, QNH 1013, report ready when clear";
            return _lastInstruction;
        }

        public string IssueContinueApproach(string callsign, string traffic = null, bool wakeCaution = false)
        {
            _active = AtcClearance.ContinueApproach;
            _lastClearedFlight = callsign ?? string.Empty;
            var trafficPhrase = string.IsNullOrWhiteSpace(traffic)
                ? string.Empty
                : $", traffic {traffic}";
            var wakePhrase = wakeCaution ? ", caution wake turbulence" : string.Empty;
            _lastInstruction =
                $"{callsign}, continue approach runway 09{trafficPhrase}{wakePhrase}, surface wind calm, QNH 1013, report short final, number one expected, expect landing clearance, vacate via Alpha when landed";
            return _lastInstruction;
        }

        public string IssueJoinLeftDownwind(string callsign)
        {
            _active = AtcClearance.JoinLeftDownwind;
            _lastClearedFlight = callsign ?? string.Empty;
            _lastInstruction =
                $"{callsign}, Kingscote Tower, join left hand downwind runway 09, make left circuit, circuit height 1000 ft, wind calm, QNH 1013, report mid-downwind then base, monitor this frequency, squawk VFR";
            return _lastInstruction;
        }

        public string IssueReportMidDownwind(string callsign)
        {
            _active = AtcClearance.ReportMidDownwind;
            _lastClearedFlight = callsign ?? string.Empty;
            _lastInstruction =
                $"{callsign}, report mid-downwind runway 09, surface wind calm, QNH 1013, number one expected, continue, expect base report, remain this frequency, look for traffic in the circuit";
            return _lastInstruction;
        }

        public string IssueReportBase(string callsign)
        {
            _active = AtcClearance.ReportBase;
            _lastClearedFlight = callsign ?? string.Empty;
            _lastInstruction =
                $"{callsign}, report turning base runway 09, surface wind calm, QNH 1013, number one expected, continue approach, expect further clearance on final, vacate via Alpha when landed";
            return _lastInstruction;
        }

        public string IssueReportFinal(string callsign)
        {
            _active = AtcClearance.ReportFinal;
            _lastClearedFlight = callsign ?? string.Empty;
            _lastInstruction =
                $"{callsign}, report turning final runway 09, surface wind calm, QNH 1013, number one expected, continue approach, runway is clear, expect landing clearance shortly, vacate via Alpha when landed";
            return _lastInstruction;
        }

        public string IssueFrequencyChangeApproved(string callsign)
        {
            _active = AtcClearance.FrequencyChange;
            _lastClearedFlight = callsign ?? string.Empty;
            _lastInstruction =
                $"{callsign}, frequency change approved, radar service terminated, leave the circuit when able, QNH 1013, contact Adelaide Centre on 125.3, squawk VFR, remain clear of cloud, good day from Kingscote Tower";
            return _lastInstruction;
        }

        public string IssueLineUpAndWait(string callsign, bool behindLanding = false, string traffic = null)
        {
            _active = AtcClearance.LineUpAndWait;
            _lastClearedFlight = callsign ?? string.Empty;
            var trafficPhrase = string.IsNullOrWhiteSpace(traffic)
                ? string.Empty
                : $", traffic {traffic}";
            if (behindLanding)
            {
                _lastInstruction =
                    $"{callsign}, behind the landing, from holding point Alpha line up and wait runway 09{trafficPhrase}, surface wind calm, QNH 1013, hold position on the runway, report ready for departure, acknowledge";
            }
            else
            {
                _lastInstruction =
                    $"{callsign}, from holding point Alpha line up and wait runway 09{trafficPhrase}, surface wind calm, QNH 1013, hold position on the runway, report ready for departure when number one, acknowledge";
            }
            return _lastInstruction;
        }

        public string IssueNumberTwoForLanding(string callsign, string traffic = null)
        {
            _active = AtcClearance.NumberTwoLanding;
            _lastClearedFlight = callsign ?? string.Empty;
            var trafficPhrase = string.IsNullOrWhiteSpace(traffic)
                ? "traffic ahead on final"
                : $"traffic ahead {traffic}";
            _lastInstruction =
                $"{callsign}, number two, continue approach runway 09, {trafficPhrase}, wind calm, QNH 1013, report short final, expect further clearance, vacate via Alpha when landed";
            return _lastInstruction;
        }

        public string IssueMinimumApproachSpeed(string callsign, string traffic = null)
        {
            _active = AtcClearance.MinimumApproachSpeed;
            _lastClearedFlight = callsign ?? string.Empty;
            var trafficPhrase = string.IsNullOrWhiteSpace(traffic) ? "traffic ahead" : $"traffic {traffic}";
            _lastInstruction =
                $"{callsign}, number two, reduce to minimum approach speed, {trafficPhrase}, continue approach runway 09, wind calm, QNH 1013, report base then short final, expect further clearance";
            return _lastInstruction;
        }

        public string IssueHoldShortAlpha(string callsign, string traffic = null)
        {
            _active = AtcClearance.HoldShortAlpha;
            _lastClearedFlight = callsign ?? string.Empty;
            var trafficPhrase = string.IsNullOrWhiteSpace(traffic)
                ? "opposing traffic on Alpha"
                : traffic;
            _lastInstruction =
                $"{callsign}, hold short Alpha at the holding point, give way to {trafficPhrase}, surface wind calm, QNH 1013, caution vehicles on the apron, report when clear to continue via Alpha";
            return _lastInstruction;
        }

        public string IssueContinueTaxi(string callsign, bool afterGiveWay = false, bool inbound = false)
        {
            _active = AtcClearance.ContinueTaxi;
            _lastClearedFlight = callsign ?? string.Empty;
            if (inbound)
            {
                _lastInstruction = afterGiveWay
                    ? $"{callsign}, traffic clear, continue taxi via Alpha to the apron, hold short of the stand until marshaller, caution vehicles on the apron, QNH 1013, report on stand when parked"
                    : $"{callsign}, continue taxi via Alpha to the apron, hold short of the stand until marshaller, caution vehicles on the apron, QNH 1013, report on stand when parked";
            }
            else
            {
                _lastInstruction = afterGiveWay
                    ? $"{callsign}, traffic clear, continue taxi via Alpha, hold short runway 09 at holding point Alpha, QNH 1013, report ready when number one"
                    : $"{callsign}, continue taxi via Alpha, hold short runway 09 at holding point Alpha, QNH 1013, report ready when number one";
            }

            return _lastInstruction;
        }

        public string IssueGiveWayTaxiing(string callsign, string traffic = null)
        {
            _active = AtcClearance.GiveWayTaxiing;
            _lastClearedFlight = callsign ?? string.Empty;
            var trafficPhrase = string.IsNullOrWhiteSpace(traffic)
                ? "taxiing traffic on Alpha"
                : traffic;
            _lastInstruction =
                $"{callsign}, hold position — give way to {trafficPhrase}, surface wind calm, QNH 1013, caution vehicles on the apron, report when clear to continue via Alpha";
            return _lastInstruction;
        }

        public string IssueOrbitLeft(string callsign, string traffic = null)
        {
            _active = AtcClearance.OrbitLeft;
            _lastClearedFlight = callsign ?? string.Empty;
            var trafficPhrase = string.IsNullOrWhiteSpace(traffic)
                ? string.Empty
                : $", traffic {traffic}";
            _lastInstruction =
                $"{callsign}, number two, make left orbit at circuit height 1000 ft{trafficPhrase}, surface wind calm, QNH 1013, report mid-downwind again for sequencing, acknowledge";
            return _lastInstruction;
        }

        public string IssueRadarContact(string callsign)
        {
            _active = AtcClearance.RadarContact;
            _lastClearedFlight = callsign ?? string.Empty;
            // Used after departure — climb-out identification, not circuit rejoin.
            _lastInstruction =
                $"{callsign}, radar contact, identified, climb runway heading to circuit height 1000 ft, remain clear of cloud, leave the circuit when able, QNH 1013, squawk VFR, report frequency change when clear";
            return _lastInstruction;
        }

        public string IssueNumberTwoForDeparture(string callsign, string traffic = null)
        {
            _active = AtcClearance.NumberTwoDeparture;
            _lastClearedFlight = callsign ?? string.Empty;
            var trafficPhrase = string.IsNullOrWhiteSpace(traffic) ? "traffic on final" : $"traffic {traffic}";
            _lastInstruction =
                $"{callsign}, number two for departure, hold short runway 09 at holding point Alpha, {trafficPhrase}, surface wind calm, QNH 1013, report ready when clear, expect further clearance, remain this frequency";
            return _lastInstruction;
        }

        public string IssueGoAround(string callsign, string reason)
        {
            _active = AtcClearance.GoAround;
            _lastClearedFlight = callsign ?? string.Empty;
            _lastInstruction = string.IsNullOrWhiteSpace(reason)
                ? $"{callsign}, go around, climb runway heading to circuit height 1000 ft, no turns below circuit height, make left circuit, wind calm, QNH 1013, report airborne on the go-around then mid-downwind then base, expect another approach, acknowledge"
                : $"{callsign}, go around — {reason}, climb runway heading to circuit height 1000 ft, no turns below circuit height, make left circuit, wind calm, QNH 1013, report airborne on the go-around then mid-downwind then base, expect another approach, acknowledge";
            return _lastInstruction;
        }

        public string IssueExpectLandingClearance(string callsign, long separationSeconds)
        {
            _active = AtcClearance.ExpectLanding;
            _lastClearedFlight = callsign ?? string.Empty;
            _lastInstruction = separationSeconds > 0
                ? $"{callsign}, continue approach runway 09, expect landing clearance in {separationSeconds}s, wind calm, QNH 1013, report short final, number one expected, vacate via Alpha when landed"
                : $"{callsign}, continue approach runway 09, expect landing clearance, wind calm, QNH 1013, report short final, number one expected, vacate via Alpha when landed";
            return _lastInstruction;
        }

        /// <summary>
        /// Short-final conditional when the runway is about to open — land when vacated.
        /// </summary>
        public string IssueConditionalLanding(string callsign, long separationSeconds)
        {
            _active = AtcClearance.ConditionalLanding;
            _lastClearedFlight = callsign ?? string.Empty;
            _lastInstruction = separationSeconds > 0
                ? $"{callsign}, continue approach, cleared to land when the runway is vacated, expect {separationSeconds}s, surface wind calm, QNH 1013, number one, vacate via Alpha when able, report runway vacated, acknowledge"
                : $"{callsign}, continue approach, cleared to land when the runway is vacated, surface wind calm, QNH 1013, number one, vacate via Alpha when able, report runway vacated, acknowledge";
            return _lastInstruction;
        }

        public string IssueGroundHold(string callsign, string reason)
        {
            _active = AtcClearance.GroundHold;
            _lastClearedFlight = callsign ?? string.Empty;
            _lastInstruction = string.IsNullOrWhiteSpace(reason)
                ? $"{callsign}, hold position on Alpha, surface wind calm, QNH 1013, caution vehicles on the apron, report when clear to continue via Alpha"
                : $"{callsign}, hold position on Alpha — {reason}, surface wind calm, QNH 1013, caution vehicles on the apron, report when clear to continue via Alpha";
            return _lastInstruction;
        }

        public string IssueHoldApron(string callsign, string traffic = null)
        {
            _active = AtcClearance.HoldApron;
            _lastClearedFlight = callsign ?? string.Empty;
            var trafficPhrase = string.IsNullOrWhiteSpace(traffic)
                ? "apron traffic"
                : traffic;
            _lastInstruction =
                $"{callsign}, hold on the apron — give way to {trafficPhrase}, surface wind calm, QNH 1013, caution vehicles on the apron, report when clear to continue to the stand";
            return _lastInstruction;
        }

        public string DescribeHold(string callsign, StableId blockedResource, SimulationTime now)
        {
            if (blockedResource.Equals(AirportSimulation.Runway))
            {
                var sep = SeparationRemainingSeconds(now);
                if (sep > 0)
                    return IssueHoldShortRunway(callsign, $"runway separation {sep}s");
                return IssueHoldShortRunway(callsign, "runway occupied");
            }

            if (blockedResource.Equals(AirportTaxiNetwork.Corridor)
                || blockedResource.Equals(AirportTaxiNetwork.AlphaOne)
                || blockedResource.Equals(AirportTaxiNetwork.AlphaTwo))
            {
                return IssueHoldShortAlpha(callsign);
            }

            if (blockedResource.Equals(AirportSimulation.ApronLane))
                return IssueHoldApron(callsign);

            _active = AtcClearance.GroundHold;
            _lastClearedFlight = callsign ?? string.Empty;
            _lastInstruction = $"{callsign}, hold for {blockedResource.Value}, wind calm, QNH 1013, report when clear to continue";
            return _lastInstruction;
        }

        private static string WeatherRemark(WeatherKind weather) => weather switch
        {
            WeatherKind.Rain => "runway wet, surface wind calm, QNH 1013",
            WeatherKind.Fog => "visibility reduced, surface wind calm, QNH 1013",
            WeatherKind.Storm => "caution thunderstorms vicinity, surface wind calm, QNH 1013",
            WeatherKind.Overcast => "ceiling overcast, surface wind calm, QNH 1013",
            WeatherKind.Cloudy => "surface wind calm, QNH 1013",
            _ => "surface wind calm, QNH 1013"
        };
    }

    public enum AtcClearance
    {
        None,
        ContinueApproach,
        JoinLeftDownwind,
        ReportMidDownwind,
        ReportBase,
        ReportFinal,
        ClearedToLand,
        TaxiToStand,
        PushbackApproved,
        TaxiToHoldShort,
        HoldShortRunway,
        ClearedForTakeoff,
        LineUpAndWait,
        NumberTwoLanding,
        MinimumApproachSpeed,
        HoldShortAlpha,
        ContinueTaxi,
        GiveWayTaxiing,
        OrbitLeft,
        RadarContact,
        NumberTwoDeparture,
        GoAround,
        ReadyForDeparture,
        RunwayVacated,
        ExpectLanding,
        ConditionalLanding,
        GroundHold,
        ReportEstablished,
        ShortFinal,
        FrequencyChange,
        TrafficAdvisory,
        ReportVacated,
        ContactGround,
        OnStand,
        EngineStart,
        ReportAirborne,
        HoldApron
    }
}
