using System;
using System.Linq;
using Airside.Domain;
using Airside.Simulation;

namespace Airside.Persistence
{
    public sealed class AwaySummary
    {
        public long AwaySeconds { get; internal set; }
        public int FlightsCompleted { get; internal set; }
        public long CashChange { get; internal set; }
        public long DelayCost { get; internal set; }
        public long RouteIncome { get; internal set; }
        public long OperatingCost { get; internal set; }
        public int ReputationChange { get; internal set; }
        public bool ClockMovedBackwards { get; internal set; }
        public bool RecoveredPreviousSave { get; internal set; }
        public bool HasReport => AwaySeconds >= 5 || RecoveredPreviousSave || ClockMovedBackwards;
    }

    public sealed class PersistentAirportSession
    {
        public const string PriorityCrewCommand = "priority-crew";
        public const string AcceptRouteCommand = "accept-route";
        public const string DeclineRouteCommand = "decline-route";
        public const string HireCrewCommand = "hire-crew";
        public const string ReleaseCrewCommand = "release-crew";
        public const string StartResearchCommand = "start-research";
        public const string StartPassengerResearchCommand = "start-research-passenger-services";
        public const string BuildStandCommand = "build-stand";
        public const long MaximumCatchUpSeconds = 30L * 24L * 60L * 60L;

        private readonly AirsideSaveRepository _repository;
        private readonly AirsideSaveData _save;

        private PersistentAirportSession(AirsideSaveRepository repository, AirsideSaveData save)
        {
            _repository = repository;
            _save = save;
            Clock = new ManualSimulationClock(new SimulationTime(0));
            Simulation = new AirportSimulation(
                Clock,
                new SeededRandomSource(save.randomSeed),
                new ReservationTable(),
                AirportLocation.FromId(save.locationId));
        }

        public ManualSimulationClock Clock { get; }
        public AirportSimulation Simulation { get; }
        public AwaySummary LastAwaySummary { get; private set; }

        public static PersistentAirportSession LoadOrCreate(string path, long currentUnixSeconds, uint newGameSeed)
        {
            var repository = new AirsideSaveRepository(path);
            var loaded = repository.TryLoad(out var save, out var recoveredPrevious);
            if (!loaded)
            {
                // AirsideSaveData.Validate() treats a zero seed as corruption (never a
                // legitimate stored value), so a caller-supplied 0 must not reach disk —
                // remap it the same way SeededRandomSource already tolerates a zero seed.
                save = new AirsideSaveData
                {
                    savedUnixSeconds = currentUnixSeconds,
                    randomSeed = newGameSeed == 0 ? 1u : newGameSeed
                };
            }

            var session = new PersistentAirportSession(repository, save);
            session.RestoreAndCatchUp(currentUnixSeconds, recoveredPrevious);
            return session;
        }

        public void AdvanceTo(long simulationSecond)
        {
            if (simulationSecond < Clock.Now.ElapsedSeconds)
                throw new ArgumentOutOfRangeException(nameof(simulationSecond));
            Clock.Set(new SimulationTime(simulationSecond));
            Simulation.Update();
        }

        public bool EnablePriorityCrew()
        {
            if (!Simulation.EnablePriorityCrew())
                return false;

            _save.commands.Add(new AirsideCommandRecord
            {
                commandId = $"priority-{_save.revision + 1}-{Clock.Now.ElapsedSeconds}",
                commandType = PriorityCrewCommand,
                simulationSecond = Clock.Now.ElapsedSeconds
            });
            return true;
        }

        public bool AcceptRoute()
        {
            if (!Simulation.AcceptPendingRoute())
                return false;

            _save.commands.Add(new AirsideCommandRecord
            {
                commandId = $"route-{_save.revision + 1}-{Clock.Now.ElapsedSeconds}",
                commandType = AcceptRouteCommand,
                simulationSecond = Clock.Now.ElapsedSeconds
            });
            return true;
        }

        public bool DeclineRoute()
        {
            if (!Simulation.DeclinePendingRoute())
                return false;

            _save.commands.Add(new AirsideCommandRecord
            {
                commandId = $"decline-{_save.revision + 1}-{Clock.Now.ElapsedSeconds}",
                commandType = DeclineRouteCommand,
                simulationSecond = Clock.Now.ElapsedSeconds
            });
            return true;
        }

        public bool HireGroundCrew() => RecordCommand(Simulation.HireGroundCrew(), HireCrewCommand, "hire");

        public bool ReleaseGroundCrew() => RecordCommand(Simulation.ReleaseGroundCrew(), ReleaseCrewCommand, "release");

        public bool StartOperationsResearch() => RecordCommand(Simulation.StartOperationsResearch(), StartResearchCommand, "research");

        public bool StartPassengerServicesResearch() =>
            RecordCommand(Simulation.StartPassengerServicesResearch(), StartPassengerResearchCommand, "research-passenger");

        public bool BuildThirdStand() => RecordCommand(Simulation.BuildThirdStand(), BuildStandCommand, "stand");

        private bool RecordCommand(bool applied, string commandType, string prefix)
        {
            if (!applied)
                return false;

            _save.commands.Add(new AirsideCommandRecord
            {
                commandId = $"{prefix}-{_save.revision + 1}-{Clock.Now.ElapsedSeconds}",
                commandType = commandType,
                simulationSecond = Clock.Now.ElapsedSeconds
            });
            return true;
        }

        public void Save(long currentUnixSeconds)
        {
            _save.revision++;
            _save.simulatedSeconds = Clock.Now.ElapsedSeconds;
            _save.savedUnixSeconds = Math.Max(0, currentUnixSeconds);
            _repository.Write(_save);
        }

        private void RestoreAndCatchUp(long currentUnixSeconds, bool recoveredPrevious)
        {
            var orderedCommands = _save.commands
                .OrderBy(command => command.simulationSecond)
                .ThenBy(command => command.commandId, StringComparer.Ordinal)
                .ToArray();
            var commandIndex = 0;

            ReplayTo(_save.simulatedSeconds, orderedCommands, ref commandIndex);
            var cyclesBefore = Simulation.CompletedCycles;
            var cashBefore = Simulation.Economy.Cash;
            var delayCostBefore = Simulation.Economy.TotalDelayCost;
            var routeIncomeBefore = Simulation.Economy.TotalRouteIncome;
            var operatingCostBefore = Simulation.Economy.TotalOperatingCost;
            var reputationBefore = Simulation.Reputation.Score;

            var rawAwaySeconds = currentUnixSeconds - _save.savedUnixSeconds;
            var clockMovedBackwards = rawAwaySeconds < 0;
            var awaySeconds = Math.Min(MaximumCatchUpSeconds, Math.Max(0, rawAwaySeconds));
            ReplayTo(checked(_save.simulatedSeconds + awaySeconds), orderedCommands, ref commandIndex);

            LastAwaySummary = new AwaySummary
            {
                AwaySeconds = awaySeconds,
                FlightsCompleted = Simulation.CompletedCycles - cyclesBefore,
                CashChange = Simulation.Economy.Cash - cashBefore,
                DelayCost = Simulation.Economy.TotalDelayCost - delayCostBefore,
                RouteIncome = Simulation.Economy.TotalRouteIncome - routeIncomeBefore,
                OperatingCost = Simulation.Economy.TotalOperatingCost - operatingCostBefore,
                ReputationChange = Simulation.Reputation.Score - reputationBefore,
                ClockMovedBackwards = clockMovedBackwards,
                RecoveredPreviousSave = recoveredPrevious
            };
        }

        private void ReplayTo(long targetSecond, AirsideCommandRecord[] commands, ref int commandIndex)
        {
            // Commands issued before the first tick sit at second 0. The advance-then-apply
            // loop below never visits that second, so drain anything already due first —
            // commandIndex only moves forward, so a second call cannot re-apply them.
            while (commandIndex < commands.Length && commands[commandIndex].simulationSecond <= Clock.Now.ElapsedSeconds)
            {
                Apply(commands[commandIndex]);
                commandIndex++;
            }

            while (Clock.Now.ElapsedSeconds < targetSecond)
            {
                AdvanceTo(Clock.Now.ElapsedSeconds + 1);
                while (commandIndex < commands.Length && commands[commandIndex].simulationSecond == Clock.Now.ElapsedSeconds)
                {
                    Apply(commands[commandIndex]);
                    commandIndex++;
                }
            }
        }

        private void Apply(AirsideCommandRecord command)
        {
            if (command.commandType == PriorityCrewCommand)
                Simulation.EnablePriorityCrew();
            else if (command.commandType == AcceptRouteCommand)
                Simulation.AcceptPendingRoute();
            else if (command.commandType == DeclineRouteCommand)
                Simulation.DeclinePendingRoute();
            else if (command.commandType == HireCrewCommand)
                Simulation.HireGroundCrew();
            else if (command.commandType == ReleaseCrewCommand)
                Simulation.ReleaseGroundCrew();
            else if (command.commandType == StartResearchCommand)
                Simulation.StartOperationsResearch();
            else if (command.commandType == StartPassengerResearchCommand)
                Simulation.StartPassengerServicesResearch();
            else if (command.commandType == BuildStandCommand)
                Simulation.BuildThirdStand();
        }
    }
}
