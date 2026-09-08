using System;
using System.Collections.Generic;
using System.Globalization;

namespace Airside.Presentation
{
    /// <summary>
    /// A minimal JSON reader for the project's glTF kits.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="ArtGltfLoader"/> used to read kits with regular expressions and a
    /// positional assumption: exactly two accessors per mesh, POSITION then indices,
    /// packed back to back in that order. That held only while the kits carried a
    /// single attribute. Once the generator started emitting NORMAL, TEXCOORD_0 and
    /// TANGENT the counts no longer alternated and every byte offset after the first
    /// mesh was wrong, so the format had to be read properly rather than guessed at.
    /// </para>
    /// <para>
    /// This is deliberately not a general JSON library: it covers what glTF 2.0 uses
    /// (objects, arrays, strings, numbers, booleans, null) and nothing more. Unity's
    /// <c>JsonUtility</c> is not usable here because glTF's <c>attributes</c> is a map
    /// with optional keys, and a missing key would be indistinguishable from a real
    /// accessor index of 0.
    /// </para>
    /// </remarks>
    internal sealed class GltfJson
    {
        private readonly string _text;
        private int _pos;

        private GltfJson(string text)
        {
            _text = text ?? string.Empty;
        }

        /// <summary>Parses a document, returning null when the text is not valid JSON.</summary>
        public static object Parse(string text)
        {
            if (string.IsNullOrEmpty(text))
                return null;
            try
            {
                var reader = new GltfJson(text);
                reader.SkipWhitespace();
                var value = reader.ReadValue();
                reader.SkipWhitespace();
                return reader._pos == reader._text.Length ? value : null;
            }
            catch (FormatException)
            {
                return null;
            }
            catch (IndexOutOfRangeException)
            {
                return null;
            }
        }

        // --- Typed lookup helpers -------------------------------------------------
        // Callers work in terms of "the object at index i of this array" and "this
        // field as an int", so every access site does not have to repeat the casts.

        public static Dictionary<string, object> AsObject(object value) =>
            value as Dictionary<string, object>;

        public static List<object> AsArray(object value) => value as List<object>;

        public static List<object> Array(Dictionary<string, object> owner, string key) =>
            owner != null && owner.TryGetValue(key, out var value) ? AsArray(value) : null;

        public static Dictionary<string, object> ObjectAt(List<object> array, int index) =>
            array != null && index >= 0 && index < array.Count ? AsObject(array[index]) : null;

        public static string String(Dictionary<string, object> owner, string key) =>
            owner != null && owner.TryGetValue(key, out var value) ? value as string : null;

        /// <summary>Reads an integer field, returning <paramref name="fallback"/> when absent.</summary>
        public static int Int(Dictionary<string, object> owner, string key, int fallback)
        {
            if (owner == null || !owner.TryGetValue(key, out var value) || !(value is double number))
                return fallback;
            return (int)number;
        }

        // --- Reader ---------------------------------------------------------------

        private object ReadValue()
        {
            SkipWhitespace();
            var c = Peek();
            switch (c)
            {
                case '{':
                    return ReadObject();
                case '[':
                    return ReadArray();
                case '"':
                    return ReadString();
                case 't':
                    Expect("true");
                    return true;
                case 'f':
                    Expect("false");
                    return false;
                case 'n':
                    Expect("null");
                    return null;
                default:
                    return ReadNumber();
            }
        }

        private Dictionary<string, object> ReadObject()
        {
            var result = new Dictionary<string, object>(StringComparer.Ordinal);
            _pos++; // '{'
            SkipWhitespace();
            if (Peek() == '}')
            {
                _pos++;
                return result;
            }

            while (true)
            {
                SkipWhitespace();
                var key = ReadString();
                SkipWhitespace();
                if (Peek() != ':')
                    throw new FormatException("expected ':'");
                _pos++;
                result[key] = ReadValue();
                SkipWhitespace();
                var c = Peek();
                _pos++;
                if (c == '}')
                    return result;
                if (c != ',')
                    throw new FormatException("expected ',' or '}'");
            }
        }

        private List<object> ReadArray()
        {
            var result = new List<object>();
            _pos++; // '['
            SkipWhitespace();
            if (Peek() == ']')
            {
                _pos++;
                return result;
            }

            while (true)
            {
                result.Add(ReadValue());
                SkipWhitespace();
                var c = Peek();
                _pos++;
                if (c == ']')
                    return result;
                if (c != ',')
                    throw new FormatException("expected ',' or ']'");
            }
        }

        private string ReadString()
        {
            if (Peek() != '"')
                throw new FormatException("expected string");
            _pos++;
            var start = _pos;

            // Fast path: the generator writes plain ASCII names with no escapes, so
            // scan for the closing quote and slice without building a StringBuilder.
            while (_pos < _text.Length)
            {
                var c = _text[_pos];
                if (c == '\\')
                    return ReadEscapedString(start);
                if (c == '"')
                {
                    var value = _text.Substring(start, _pos - start);
                    _pos++;
                    return value;
                }
                _pos++;
            }

            throw new FormatException("unterminated string");
        }

        private string ReadEscapedString(int start)
        {
            var builder = new System.Text.StringBuilder(_text, start, _pos - start, 32);
            while (_pos < _text.Length)
            {
                var c = _text[_pos++];
                if (c == '"')
                    return builder.ToString();
                if (c != '\\')
                {
                    builder.Append(c);
                    continue;
                }

                var escape = _text[_pos++];
                switch (escape)
                {
                    case '"': builder.Append('"'); break;
                    case '\\': builder.Append('\\'); break;
                    case '/': builder.Append('/'); break;
                    case 'b': builder.Append('\b'); break;
                    case 'f': builder.Append('\f'); break;
                    case 'n': builder.Append('\n'); break;
                    case 'r': builder.Append('\r'); break;
                    case 't': builder.Append('\t'); break;
                    case 'u':
                        builder.Append((char)ushort.Parse(
                            _text.Substring(_pos, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture));
                        _pos += 4;
                        break;
                    default:
                        throw new FormatException("bad escape");
                }
            }

            throw new FormatException("unterminated string");
        }

        private double ReadNumber()
        {
            var start = _pos;
            if (Peek() == '-' || Peek() == '+')
                _pos++;
            while (_pos < _text.Length)
            {
                var c = _text[_pos];
                if ((c >= '0' && c <= '9') || c == '.' || c == 'e' || c == 'E' || c == '-' || c == '+')
                    _pos++;
                else
                    break;
            }

            if (_pos == start)
                throw new FormatException("expected number");
            // Invariant culture matters: the generator writes '.' decimals regardless
            // of the machine locale the game runs on.
            return double.Parse(
                _text.Substring(start, _pos - start),
                NumberStyles.Float,
                CultureInfo.InvariantCulture);
        }

        private char Peek()
        {
            if (_pos >= _text.Length)
                throw new FormatException("unexpected end of input");
            return _text[_pos];
        }

        private void Expect(string literal)
        {
            if (_pos + literal.Length > _text.Length ||
                string.CompareOrdinal(_text, _pos, literal, 0, literal.Length) != 0)
                throw new FormatException("expected " + literal);
            _pos += literal.Length;
        }

        private void SkipWhitespace()
        {
            while (_pos < _text.Length)
            {
                var c = _text[_pos];
                if (c == ' ' || c == '\t' || c == '\n' || c == '\r')
                    _pos++;
                else
                    break;
            }
        }
    }
}
