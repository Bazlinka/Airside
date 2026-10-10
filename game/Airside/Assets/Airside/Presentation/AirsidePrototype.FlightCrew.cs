using System;
using System.Collections.Generic;
using Airside.Domain;
using Airside.Simulation;
using UnityEngine;

namespace Airside.Presentation
{
    public sealed partial class AirsidePrototype
    {
        private readonly Dictionary<string, List<RampCrewPerson>> _flightCrew = new(StringComparer.Ordinal);
        private readonly List<CharacterKind> _flightCrewKinds = new();
        private readonly HashSet<string> _flightCrewWanted = new(StringComparer.Ordinal);
        private readonly List<string> _flightCrewRetired = new();
        private void UpdateFlightCrew()
        {
            _flightCrewWanted.Clear();
            foreach (var team in _flightCrew.Values)
                foreach (var person in team) person.Instance.SetActive(false);
            if (!FleetMode || _operations == null || !EnsureCharacters()) return;
            if (_flightCrewKinds.Count == 0)
                LoadCharacterKinds(new[] { "chr_passenger_m_suit", "chr_passenger_f_suit" }, _flightCrewKinds);
            if (_flightCrewKinds.Count == 0) return;
            var shown = 0;
            foreach (var aircraft in _operations.Fleet)
            {
                if (shown >= 16) break;
                if (aircraft.State != FleetState.AtStand || aircraft.Type.IsRotorcraft
                    || aircraft.Scheduled is not { Cancelled: false } flight || string.IsNullOrEmpty(aircraft.Stand.Value)) continue;
                var level = aircraft.Airline.IsPlayer ? aircraft.BaseLevel : PlayerBaseLevel.Starter;
                var total = DeparturePrep.TotalSeconds(aircraft.Type, level);
                var start = aircraft.Airline.IsPlayer ? DeparturePrep.ReadyAtSeconds(aircraft) - total
                    : Math.Max(aircraft.StateStartedAt.ElapsedSeconds + 90, flight.DepartAt.ElapsedSeconds - total - (long)DepartureCountdown.PrepEndsBeforeSeconds);
                var elapsed = _preciseTime - start;
                if (elapsed < 0 || elapsed > total + BoardingFlow.WalkBudgetSeconds) continue;
                shown++;
                _flightCrewWanted.Add(aircraft.Registration);
                var pose = AdelaideGround.StandPose(aircraft.Stand);
                var nose = new Vector3(pose.NoseX, 0, pose.NoseZ);
                var crewCount = aircraft.IsFreighter ? 2 : FlightCrewWork.Count(aircraft.Type);
                var boardStart = total - DeparturePrep.BoardingSecondsFor(aircraft.Type, level)
                    - (crewCount-1)*FlightCrewWork.BoardingSpacingSeconds - 20;
                if (!_flightCrew.TryGetValue(aircraft.Registration, out var team))
                    _flightCrew[aircraft.Registration] = team = new List<RampCrewPerson>();
                // One captain inspects; first officer and cabin crew brief alongside the boarding approach.
                for (var slot = 0; slot < crewCount; slot++)
                {
                    while (team.Count <= slot)
                    {
                        var index = team.Count;
                        var kind = _flightCrewKinds[index % _flightCrewKinds.Count];
                        var instance = Instantiate(kind.Prefab, BoardingRoot());
                        instance.name = index < 2 ? (index == 0 ? "Captain" : "First officer") : "Cabin crew " + (index - 1);
                        instance.transform.localScale = Vector3.one * kind.ScaleToMetre * (index % 2 == 0 ? 1.78f : 1.68f);
                        if (instance.TryGetComponent<Animator>(out var animator)) animator.enabled = false;
                        FlightCrewUniform.Apply(instance, index < 2);
                        team.Add(new RampCrewPerson { Instance = instance, Root = instance.transform, Kind = kind });
                    }
                    var person = team[slot];
                    CrewAction action;
                    if (elapsed < boardStart)
                    {
                        if (BoardingFlow.ModeFor(aircraft) == BoardingMode.Aerobridge)
                        {
                            if (slot != 0) continue; // First officer/cabin briefing is inside the terminal.
                            var inspected = FlightCrewWork.InspectionSeconds(aircraft.Type);
                            if (elapsed >= inspected)
                            {
                                var end = FlightCrewWork.Inspection(aircraft.Type, double.MaxValue);
                                var ground = BoardingPavementY(aircraft);
                                var from = LayoutToWorld(pose, (end.X,end.Z), ground);
                                var door = NearestTerminalDoor(from, ground);
                                var returning = RoutedWalk("flightreturn:"+aircraft.Registration,from,door,door,door);
                                if (!Locate(returning,(float)(elapsed-inspected),FlightCrewWork.WalkPace,true,
                                    out var at,out var facing,out _)) continue;
                                person.Root.position = at;
                                person.Root.rotation = Quaternion.LookRotation(facing.sqrMagnitude > .001f ? facing : nose);
                                person.Instance.SetActive(true);
                                if (person.Kind.Walk != null) person.Kind.Walk.SampleAnimation(person.Instance,
                                    (float)(_preciseTime % person.Kind.Walk.length));
                                continue;
                            }
                        }
                        if (slot == 0) action = FlightCrewWork.Inspection(aircraft.Type, elapsed);
                        else action = FlightCrewWork.Waiting(aircraft.Type, slot);
                        person.Root.position = LayoutToWorld(pose, (action.X, action.Z), BoardingPavementY(aircraft)) + Vector3.up * action.Height;
                        person.Root.rotation = Quaternion.LookRotation(Quaternion.AngleAxis(action.FacingDegrees, Vector3.up) * nose);
                    }
                    else
                    {
                        if (!_fleetViewById.TryGetValue(aircraft.Registration, out var view)
                            || !TryWalkPath(aircraft, view, BoardingFlow.ModeFor(aircraft), out var path)) continue;
                        var since = elapsed - boardStart - slot * FlightCrewWork.BoardingSpacingSeconds;
                        if (since < 0) continue;
                        if (path.StairStart < path.Points.Length && path.StairStart >= 1)
                        {
                            var from = slot == 0 ? FlightCrewWork.Inspection(aircraft.Type, double.MaxValue)
                                : FlightCrewWork.Waiting(aircraft.Type, slot);
                            path = RoutedWalk("flightcrew:" + aircraft.Registration + ":" + slot,
                                LayoutToWorld(pose, (from.X, from.Z), BoardingPavementY(aircraft)),
                                path.Points[path.StairStart - 1], path.Points[path.StairStart], path.Points[path.StairStart + 1]);
                        }
                        if (!Locate(path, (float)since, FlightCrewWork.WalkPace, true, out var position,
                                out var heading, out _)) continue;
                        person.Root.position = position;
                        person.Root.rotation = Quaternion.LookRotation(heading.sqrMagnitude > .001f ? heading : nose);
                        action = new CrewAction(0, 0, 0, 0, true, CarriedItem.None);
                    }
                    person.Instance.SetActive(true);
                    var clip = action.Walking ? person.Kind.Walk : slot == 0 && elapsed < FlightCrewWork.InspectionSeconds(aircraft.Type)
                        ? person.Kind.Interact : person.Kind.Idle;
                    if (clip != null && clip.length > .01f)
                        clip.SampleAnimation(person.Instance, (float)((_preciseTime + slot * .37) % clip.length));
                }
            }
            // Recycle finished/removed teams rather than keeping a character allocation per career aircraft forever.
            var retired = _flightCrewRetired; retired.Clear();
            foreach (var entry in _flightCrew)
            {
                if (_flightCrewWanted.Contains(entry.Key)) continue;
                retired.Add(entry.Key);
            }
            foreach (var key in retired)
            {
                foreach (var p in _flightCrew[key]) Destroy(p.Instance);
                _flightCrew.Remove(key);
            }
        }
    }
}
