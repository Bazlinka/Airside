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
        public bool ClockMovedBackwards { get; internal set; }
        public bool RecoveredPreviousSave { get; internal set; }
        public bool HasReport => AwaySeconds >= 5 || RecoveredPreviousSave || ClockMovedBackwards;
    }

    public sealed class PersistentAirportSession
    {
        public const string PriorityCrewCommand = "priority-crew";
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
                save = new AirsideSaveData
                {
                    savedUnixSeconds = currentUnixSeconds,
                    randomSeed = newGameSeed
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
                ClockMovedBackwards = clockMovedBackwards,
                RecoveredPreviousSave = recoveredPrevious
            };
        }

        private void ReplayTo(long targetSecond, AirsideCommandRecord[] commands, ref int commandIndex)
        {
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
        }
    }
}
