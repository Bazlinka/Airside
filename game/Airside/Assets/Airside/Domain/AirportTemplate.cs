using System;
using System.Collections.Generic;

namespace Airside.Domain
{
    /// <summary>How much of a figure is checked against a source (docs/data/AUSTRALIAN_AIRPORTS_RUNWAYS_TERMINALS_GATES.md).</summary>
    public enum FactBasis
    {
        /// <summary>Taken from the airport's published figures.</summary>
        Sourced,
        /// <summary>Derived from a published figure (a total split across terminals, a photo caption).</summary>
        Inferred,
        /// <summary>A generic planning placeholder because no figure was found; replace when a source is added.</summary>
        Generic
    }

    /// <summary>Who may use a gate.</summary>
    public enum GateUse
    {
        Domestic,
        International,
        /// <summary>Swing gate: serves either, with the passenger flow switched.</summary>
        Swing
    }

    /// <summary>One end of a runway: its designator, magnetic heading (designator x 10) and the runway's usable length.</summary>
    public readonly struct RunwayEnd
    {
        public RunwayEnd(string airportIata, string runwayId, string designator, double lengthMetres, bool sealedSurface)
        {
            AirportIata = airportIata;
            RunwayId = runwayId;
            Designator = designator;
            LengthMetres = lengthMetres;
            Sealed = sealedSurface;
        }

        public string AirportIata { get; }
        /// <summary>The runway's paired name, "16/34" or "16L/34R".</summary>
        public string RunwayId { get; }
        /// <summary>"16", "34R".</summary>
        public string Designator { get; }
        public double LengthMetres { get; }
        public bool Sealed { get; }

        /// <summary>Magnetic heading in degrees, from the number in the designator (ICAO Annex 14).</summary>
        public int HeadingDegrees
        {
            get
            {
                var digits = 0;
                for (var i = 0; i < Designator.Length && char.IsDigit(Designator[i]); i++)
                    digits = digits * 10 + (Designator[i] - '0');
                return digits * 10;
            }
        }

        public override string ToString() => "RWY " + Designator;
    }

    /// <summary>A runway and its two ends.</summary>
    public sealed class RunwayTemplate
    {
        public RunwayTemplate(string id, double lengthMetres, bool sealedSurface, char softMaxCodeLetter = 'F')
        {
            var slash = id.IndexOf('/');
            if (slash <= 0 || slash == id.Length - 1)
                throw new ArgumentException("A runway id is two designators, like 16/34.", nameof(id));
            Id = id;
            LengthMetres = lengthMetres;
            Sealed = sealedSurface;
            SoftMaxCodeLetter = softMaxCodeLetter;
        }

        public string Id { get; }
        public double LengthMetres { get; }
        public bool Sealed { get; }

        /// <summary>
        /// Largest ICAO code letter the airport steers here when another runway will do (Sydney keeps
        /// widebodies off 07/25 and 16L/34R). A preference, never a bar.
        /// </summary>
        public char SoftMaxCodeLetter { get; }

        public string LowDesignator => Id.Substring(0, Id.IndexOf('/'));
        public string HighDesignator => Id.Substring(Id.IndexOf('/') + 1);

        public RunwayEnd End(string airportIata, bool high) =>
            new(airportIata, Id, high ? HighDesignator : LowDesignator, LengthMetres, Sealed);
    }

    /// <summary>A gate or bay on a terminal's apron.</summary>
    public sealed class GateTemplate
    {
        public GateTemplate(string id, string terminalId, char maxCodeLetter, bool aerobridge, GateUse use, FactBasis basis)
        {
            Id = id;
            TerminalId = terminalId;
            MaxCodeLetter = maxCodeLetter;
            Aerobridge = aerobridge;
            Use = use;
            Basis = basis;
        }

        /// <summary>Unique at its airport: "T2-07".</summary>
        public string Id { get; }
        public string TerminalId { get; }
        /// <summary>Largest ICAO wingspan code letter (A to F) the stand takes (ICAO Annex 14).</summary>
        public char MaxCodeLetter { get; }
        public bool Aerobridge { get; }
        public GateUse Use { get; }
        public FactBasis Basis { get; }
    }

    /// <summary>A passenger terminal (or concourse) and its gates.</summary>
    public sealed class TerminalTemplate
    {
        public TerminalTemplate(string id, string name, IReadOnlyList<GateTemplate> gates, IReadOnlyList<string> carriers = null)
        {
            Id = id;
            Name = name;
            Gates = gates;
            Carriers = carriers ?? Array.Empty<string>();
        }

        /// <summary>Airline codes (Airline.Code) that use this terminal; empty when any airline may.</summary>
        public IReadOnlyList<string> Carriers { get; }

        public bool Serves(string airlineCode)
        {
            if (string.IsNullOrEmpty(airlineCode) || Carriers.Count == 0) return false;
            foreach (var carrier in Carriers)
                if (string.Equals(carrier, airlineCode, StringComparison.Ordinal)) return true;
            return false;
        }

        public string Id { get; }
        public string Name { get; }
        public IReadOnlyList<GateTemplate> Gates { get; }
    }

    /// <summary>
    /// The generic facts an airport needs so any flight can land and park there: runways, terminals and gates.
    /// Not a map. The player's own airport keeps its detailed layout; every other airport is this template.
    /// </summary>
    public sealed class AirportTemplate
    {
        public AirportTemplate(string iata, string icao, string name, string state,
            IReadOnlyList<RunwayTemplate> runways, string calmWindRunwayEnd, IReadOnlyList<TerminalTemplate> terminals,
            string basisNote)
        {
            Iata = iata;
            Icao = icao;
            Name = name;
            State = state;
            Runways = runways;
            CalmWindRunwayEnd = calmWindRunwayEnd;
            Terminals = terminals;
            BasisNote = basisNote;
        }

        public string Iata { get; }
        public string Icao { get; }
        public string Name { get; }
        public string State { get; }
        public IReadOnlyList<RunwayTemplate> Runways { get; }

        /// <summary>
        /// The runway end used in light or variable wind: the longer sealed runway's lower-numbered end unless an
        /// airport's published practice says otherwise. A planning default, not an official preference.
        /// </summary>
        public string CalmWindRunwayEnd { get; }
        public IReadOnlyList<TerminalTemplate> Terminals { get; }

        /// <summary>One line on where the figures came from and what is still generic.</summary>
        public string BasisNote { get; }

        public IEnumerable<GateTemplate> AllGates
        {
            get
            {
                foreach (var terminal in Terminals)
                    foreach (var gate in terminal.Gates)
                        yield return gate;
            }
        }

        public IEnumerable<RunwayEnd> AllRunwayEnds
        {
            get
            {
                foreach (var runway in Runways)
                {
                    yield return runway.End(Iata, false);
                    yield return runway.End(Iata, true);
                }
            }
        }
    }
}
