# Flight performance verification — 8 October 2026

Native Unity 6000.3.23f1 focused EditMode run: **166 passed, zero failed**.
Completed at 01:15:40 UTC. Filter: FlightPerformanceNativeTests;
FlightPerformanceRealismTests; FlightViewInformationTests;
DepartureFlightTransitionTests; ArrivalMapTrackTests; FlightWorldTests.

Coverage: all thirteen fixed-wing types, arrival joins at 130/650/1500km and
practical range, altitude/rate and distance/speed derivatives, level cruise,
250 CAS below 10,000ft, type-aware regional Vr/roll, ISA conversions,
accelerated telemetry/origin shifts and Bell helper altitude. The authoritative
Bell VTOL path remains separate. Research includes all fourteen fleet types.

The full headless run was stopped on the user's request to merge promptly.
The NUnit compatibility compile completed; this is not a completed full-suite pass.
No new Mac build, packaged camera journey or graphics-on performance run was
completed. These remain explicitly unverified. The source is a representative
ISA/zero-wind planning model, not certified dispatch performance.
