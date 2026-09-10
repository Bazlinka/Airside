using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Airside.Persistence
{
    /// <summary>
    /// JsonUtility-compatible read/write for <see cref="AirsideSaveData"/> without a
    /// UnityEngine dependency, so Persistence stays headless-testable and player-buildable.
    /// </summary>
    internal static class AirsideSaveJsonCodec
    {
        public static string Serialize(AirsideSaveData save, bool pretty)
        {
            var sb = new StringBuilder(256);
            if (pretty)
                sb.Append("{\n");
            else
                sb.Append('{');

            WriteField(sb, "schemaVersion", save.schemaVersion, pretty, first: true);
            WriteField(sb, "revision", save.revision, pretty);
            WriteField(sb, "simulatedSeconds", save.simulatedSeconds, pretty);
            WriteField(sb, "savedUnixSeconds", save.savedUnixSeconds, pretty);
            WriteField(sb, "randomSeed", save.randomSeed, pretty);
            WriteField(sb, "locationId", save.locationId ?? string.Empty, pretty);
            WriteCommands(sb, save.commands, pretty);

            if (pretty)
                sb.Append("\n}");
            else
                sb.Append('}');
            return sb.ToString();
        }

        public static AirsideSaveData Deserialize(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                throw new FormatException("Save JSON is empty.");

            var save = new AirsideSaveData
            {
                schemaVersion = ReadInt(json, "schemaVersion"),
                revision = ReadLong(json, "revision"),
                simulatedSeconds = ReadLong(json, "simulatedSeconds"),
                savedUnixSeconds = ReadLong(json, "savedUnixSeconds"),
                randomSeed = ReadUInt(json, "randomSeed"),
                locationId = ReadString(json, "locationId") ?? string.Empty,
                commands = ReadCommands(json)
            };
            return save;
        }

        private static void WriteField(StringBuilder sb, string name, long value, bool pretty, bool first = false)
        {
            if (!first)
                sb.Append(pretty ? ",\n" : ",");
            if (pretty)
                sb.Append("    ");
            sb.Append('"').Append(name).Append("\": ").Append(value.ToString(CultureInfo.InvariantCulture));
        }

        private static void WriteField(StringBuilder sb, string name, int value, bool pretty, bool first = false)
        {
            WriteField(sb, name, (long)value, pretty, first);
        }

        private static void WriteField(StringBuilder sb, string name, uint value, bool pretty, bool first = false)
        {
            WriteField(sb, name, (long)value, pretty, first);
        }

        private static void WriteField(StringBuilder sb, string name, string value, bool pretty, bool first = false)
        {
            if (!first)
                sb.Append(pretty ? ",\n" : ",");
            if (pretty)
                sb.Append("    ");
            sb.Append('"').Append(name).Append("\": \"").Append(Escape(value)).Append('"');
        }

        private static void WriteCommands(StringBuilder sb, List<AirsideCommandRecord> commands, bool pretty)
        {
            sb.Append(pretty ? ",\n    \"commands\": [" : ",\"commands\":[");
            if (commands != null)
            {
                for (var i = 0; i < commands.Count; i++)
                {
                    var command = commands[i];
                    if (command == null)
                        continue;
                    if (i > 0)
                        sb.Append(pretty ? ",\n        " : ",");
                    if (pretty)
                        sb.Append("\n        ");
                    sb.Append('{');
                    sb.Append(pretty ? "\n            " : string.Empty);
                    sb.Append("\"commandId\": \"").Append(Escape(command.commandId)).Append('"');
                    sb.Append(pretty ? ",\n            " : ",");
                    sb.Append("\"commandType\": \"").Append(Escape(command.commandType)).Append('"');
                    sb.Append(pretty ? ",\n            " : ",");
                    sb.Append("\"simulationSecond\": ")
                        .Append(command.simulationSecond.ToString(CultureInfo.InvariantCulture));
                    sb.Append(pretty ? "\n        }" : "}");
                }
            }

            sb.Append(pretty ? "\n    ]" : "]");
        }

        private static string Escape(string value) =>
            string.IsNullOrEmpty(value) ? string.Empty : value.Replace("\\", "\\\\").Replace("\"", "\\\"");

        private static int ReadInt(string json, string field) =>
            (int)ReadLong(json, field);

        private static uint ReadUInt(string json, string field) =>
            (uint)ReadLong(json, field);

        private static long ReadLong(string json, string field)
        {
            var token = ReadToken(json, field);
            if (!long.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
                throw new FormatException($"Save field '{field}' is not a valid integer.");
            return value;
        }

        private static string ReadString(string json, string field)
        {
            var key = Quote(field);
            var index = json.IndexOf(key, StringComparison.Ordinal);
            if (index < 0)
                return null;
            index = json.IndexOf(':', index + key.Length);
            if (index < 0)
                throw new FormatException($"Save field '{field}' is malformed.");
            index++;
            while (index < json.Length && char.IsWhiteSpace(json[index]))
                index++;
            if (index >= json.Length || json[index] != '"')
                throw new FormatException($"Save field '{field}' is not a string.");
            index++;
            var sb = new StringBuilder();
            while (index < json.Length)
            {
                var c = json[index++];
                if (c == '\\' && index < json.Length)
                {
                    var next = json[index++];
                    sb.Append(next == '"' || next == '\\' ? next : next);
                    continue;
                }

                if (c == '"')
                    return sb.ToString();
                sb.Append(c);
            }

            throw new FormatException($"Save field '{field}' string is unterminated.");
        }

        private static List<AirsideCommandRecord> ReadCommands(string json)
        {
            var key = "\"commands\"";
            var index = json.IndexOf(key, StringComparison.Ordinal);
            if (index < 0)
                return new List<AirsideCommandRecord>();
            index = json.IndexOf('[', index);
            if (index < 0)
                return new List<AirsideCommandRecord>();

            var commands = new List<AirsideCommandRecord>();
            index++;
            while (index < json.Length)
            {
                while (index < json.Length && json[index] != '{' && json[index] != ']')
                    index++;
                if (index >= json.Length || json[index] == ']')
                    break;

                var end = FindMatchingBrace(json, index);
                var block = json.Substring(index, end - index + 1);
                commands.Add(new AirsideCommandRecord
                {
                    commandId = ReadString(block, "commandId"),
                    commandType = ReadString(block, "commandType"),
                    simulationSecond = ReadLong(block, "simulationSecond")
                });
                index = end + 1;
            }

            return commands;
        }

        private static int FindMatchingBrace(string json, int openIndex)
        {
            var depth = 0;
            for (var i = openIndex; i < json.Length; i++)
            {
                if (json[i] == '{')
                    depth++;
                else if (json[i] == '}' && --depth == 0)
                    return i;
            }

            throw new FormatException("Save command object is unterminated.");
        }

        private static string ReadToken(string json, string field)
        {
            var key = Quote(field);
            var index = json.IndexOf(key, StringComparison.Ordinal);
            if (index < 0)
                throw new FormatException($"Save field '{field}' is missing.");
            index = json.IndexOf(':', index + key.Length);
            if (index < 0)
                throw new FormatException($"Save field '{field}' is malformed.");
            index++;
            while (index < json.Length && char.IsWhiteSpace(json[index]))
                index++;
            var start = index;
            while (index < json.Length && json[index] != ',' && json[index] != '}' && json[index] != ']')
                index++;
            return json.Substring(start, index - start).Trim();
        }

        private static string Quote(string field) => "\"" + field + "\"";
    }
}
