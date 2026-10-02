using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace ControlRoom
{
    /// <summary>Strict command-only schema. Unknown fields, duplicate keys and invented enums fail closed.</summary>
    public static class CommandJsonCodec
    {
        static readonly HashSet<string> RootFields = new HashSet<string> { "target", "action", "object", "objectId", "location", "locationId", "duration", "conditions", "sequence", "restrictions", "outputFormat", "confidence", "prompt" };
        static readonly HashSet<string> StepFields = new HashSet<string> { "action", "object", "objectId", "location", "locationId", "duration" };

        public static ParseResult Parse(string json)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(json) || json.Length > 65536) throw new FormatException("JSON length is invalid.");
                var root = ReadJson(json) as Dictionary<string, object>;
                if (root == null) throw new FormatException("Command must be a JSON object.");
                CheckFields(root, RootFields);
                var command = new Command
                {
                    target = EnumValue<CommandTarget>(RequiredString(root, "target")),
                    objectId = Alias(root, "objectId", "object"), locationId = Alias(root, "locationId", "location"),
                    outputFormat = OptionalString(root, "outputFormat", "summary"), prompt = OptionalString(root, "prompt", ""),
                    confidence = Number(root, "confidence", 1f, 0f, 1f),
                    conditions = Strings(root, "conditions"), restrictions = Strings(root, "restrictions")
                };
                foreach (string restriction in command.restrictions)
                    if (!CommandValidator.IsRestriction(restriction)) throw new FormatException("Unsupported restriction: " + restriction);
                object sequenceValue;
                if (root.TryGetValue("sequence", out sequenceValue))
                {
                    var sequence = sequenceValue as List<object>;
                    if (sequence == null || sequence.Count > 32) throw new FormatException("sequence must be an array of at most 32 objects.");
                    foreach (object value in sequence)
                    {
                        var step = value as Dictionary<string, object>;
                        if (step == null) throw new FormatException("Every sequence step must be an object.");
                        CheckFields(step, StepFields);
                        command.sequence.Add(new CommandStep(EnumValue<CommandAction>(RequiredString(step, "action")),
                            Alias(step, "locationId", "location"), Alias(step, "objectId", "object"), Number(step, "duration", 3f, 0.001f, 600f)));
                    }
                }
                if (root.ContainsKey("action")) command.action = EnumValue<CommandAction>(RequiredString(root, "action"));
                else if (command.sequence.Count > 0) command.action = command.sequence[0].action;
                else throw new FormatException("action or a nonempty sequence is required.");
                if (command.sequence.Count > 0)
                {
                    if (root.ContainsKey("action") && command.action != command.sequence[0].action)
                        throw new FormatException("action conflicts with the first sequence step.");
                    if ((!string.IsNullOrEmpty(command.objectId) && command.objectId != command.sequence[0].objectId) ||
                        (!string.IsNullOrEmpty(command.locationId) && command.locationId != command.sequence[0].locationId))
                        throw new FormatException("Top-level object or location conflicts with the first sequence step.");
                    command.objectId = command.sequence[0].objectId;
                    command.locationId = command.sequence[0].locationId;
                }
                else if (root.ContainsKey("duration"))
                    command.sequence.Add(new CommandStep(command.action, command.locationId, command.objectId, Number(root, "duration", 3f, 0.001f, 600f)));
                foreach (CommandStep step in command.Steps)
                {
                    if (command.target == CommandTarget.FIELD_AGENT && step.action == CommandAction.ANALYZE)
                        throw new FormatException("FIELD_AGENT cannot ANALYZE.");
                    if (command.target == CommandTarget.ANALYSIS_SYSTEM && step.action != CommandAction.ANALYZE)
                        throw new FormatException("ANALYSIS_SYSTEM accepts only ANALYZE.");
                }
                return new ParseResult { command = command };
            }
            catch (Exception exception) when (exception is FormatException || exception is ArgumentException || exception is OverflowException)
            {
                return new ParseResult { error = "COMMAND PROCESSING FAILED: " + exception.Message };
            }
        }

        public static string Serialize(Command command)
        {
            if (command == null) throw new ArgumentNullException(nameof(command));
            var json = new StringBuilder();
            json.Append("{\"target\":").Append(Quote(command.target.ToString())).Append(",\"action\":").Append(Quote(command.action.ToString()));
            json.Append(",\"objectId\":").Append(Quote(command.objectId)).Append(",\"locationId\":").Append(Quote(command.locationId));
            json.Append(",\"conditions\":").Append(StringArray(command.conditions)).Append(",\"restrictions\":").Append(StringArray(command.restrictions));
            json.Append(",\"sequence\":[");
            for (int i = 0; i < command.sequence.Count; i++)
            {
                CommandStep step = command.sequence[i];
                if (i > 0) json.Append(',');
                json.Append("{\"action\":").Append(Quote(step.action.ToString())).Append(",\"objectId\":").Append(Quote(step.objectId));
                json.Append(",\"locationId\":").Append(Quote(step.locationId)).Append(",\"duration\":").Append(step.duration.ToString("R", CultureInfo.InvariantCulture)).Append('}');
            }
            json.Append("],\"outputFormat\":").Append(Quote(command.outputFormat)).Append(",\"confidence\":").Append(command.confidence.ToString("R", CultureInfo.InvariantCulture));
            json.Append(",\"prompt\":").Append(Quote(command.prompt)).Append('}');
            return json.ToString();
        }

        public static string Quote(string value)
        {
            var result = new StringBuilder("\"");
            foreach (char character in value ?? "")
            {
                switch (character)
                {
                    case '"': result.Append("\\\""); break;
                    case '\\': result.Append("\\\\"); break;
                    case '\n': result.Append("\\n"); break;
                    case '\r': result.Append("\\r"); break;
                    case '\t': result.Append("\\t"); break;
                    default: if (character < 32) result.Append("\\u").Append(((int)character).ToString("x4")); else result.Append(character); break;
                }
            }
            return result.Append('"').ToString();
        }

        static string StringArray(List<string> values)
        { return "[" + string.Join(",", (values ?? new List<string>()).ConvertAll(Quote)) + "]"; }
        static void CheckFields(Dictionary<string, object> fields, HashSet<string> allowed)
        { foreach (string field in fields.Keys) if (!allowed.Contains(field)) throw new FormatException("Unknown command field: " + field); }
        static string RequiredString(Dictionary<string, object> fields, string key)
        {
            object value;
            if (!fields.TryGetValue(key, out value) || !(value is string) || string.IsNullOrWhiteSpace((string)value)) throw new FormatException(key + " must be a nonempty string.");
            return (string)value;
        }
        static string OptionalString(Dictionary<string, object> fields, string key, string fallback)
        {
            object value;
            if (!fields.TryGetValue(key, out value)) return fallback;
            if (!(value is string)) throw new FormatException(key + " must be a string.");
            return (string)value;
        }
        static string Alias(Dictionary<string, object> fields, string current, string legacy)
        {
            string value = OptionalString(fields, current, "");
            string oldValue = OptionalString(fields, legacy, "");
            if (fields.ContainsKey(current) && fields.ContainsKey(legacy) && value != oldValue) throw new FormatException(current + " and " + legacy + " conflict.");
            return fields.ContainsKey(current) ? value : oldValue;
        }
        static T EnumValue<T>(string value) where T : struct
        {
            T parsed;
            // Enum.TryParse accepts numeric values: forbid them before checking named enum membership.
            if (!System.Text.RegularExpressions.Regex.IsMatch(value, @"^[A-Za-z_]+$") || !Enum.TryParse(value, true, out parsed) || !Enum.IsDefined(typeof(T), parsed))
                throw new FormatException("Unsupported " + typeof(T).Name + ": " + value);
            return parsed;
        }
        static float Number(Dictionary<string, object> fields, string key, float fallback, float min, float max)
        {
            object value;
            if (!fields.TryGetValue(key, out value)) return fallback;
            if (!(value is double)) throw new FormatException(key + " must be a number.");
            double number = (double)value;
            if (double.IsNaN(number) || double.IsInfinity(number) || number < min || number > max) throw new FormatException(key + " is out of range.");
            return (float)number;
        }
        static List<string> Strings(Dictionary<string, object> fields, string key)
        {
            object value;
            var result = new List<string>();
            if (!fields.TryGetValue(key, out value)) return result;
            var list = value as List<object>;
            if (list == null || list.Count > 32) throw new FormatException(key + " must be an array of at most 32 strings.");
            foreach (object item in list)
            {
                if (!(item is string) || string.IsNullOrWhiteSpace((string)item) || ((string)item).Length > 256) throw new FormatException(key + " entries must be nonempty strings.");
                result.Add((string)item);
            }
            return result;
        }

        internal static object ReadJson(string json) { return new JsonReader(json).Read(); }

        sealed class JsonReader
        {
            readonly string text;
            int cursor;
            public JsonReader(string source) { text = source; }
            public object Read()
            {
                object result = Value(0);
                SkipSpace();
                if (cursor != text.Length) throw new FormatException("Trailing JSON content.");
                return result;
            }
            object Value(int depth)
            {
                if (depth > 16) throw new FormatException("JSON nesting exceeds the limit.");
                SkipSpace();
                if (cursor >= text.Length) throw new FormatException("Unexpected end of JSON.");
                char character = text[cursor];
                if (character == '{') return Object(depth + 1);
                if (character == '[') return Array(depth + 1);
                if (character == '"') return String();
                if (character == '-' || char.IsDigit(character)) return Numeric();
                if (Literal("true")) return true;
                if (Literal("false")) return false;
                if (Literal("null")) return null;
                throw new FormatException("Invalid JSON value.");
            }
            Dictionary<string, object> Object(int depth)
            {
                cursor++;
                var fields = new Dictionary<string, object>();
                SkipSpace();
                if (Consume('}')) return fields;
                while (true)
                {
                    SkipSpace();
                    string key = String();
                    SkipSpace();
                    Require(':');
                    if (fields.ContainsKey(key)) throw new FormatException("Duplicate JSON field: " + key);
                    fields.Add(key, Value(depth));
                    SkipSpace();
                    if (Consume('}')) return fields;
                    Require(',');
                }
            }
            List<object> Array(int depth)
            {
                cursor++;
                var values = new List<object>();
                SkipSpace();
                if (Consume(']')) return values;
                while (true)
                {
                    if (values.Count > 2048) throw new FormatException("JSON array exceeds the limit.");
                    values.Add(Value(depth));
                    SkipSpace();
                    if (Consume(']')) return values;
                    Require(',');
                }
            }
            string String()
            {
                Require('"');
                var value = new StringBuilder();
                while (cursor < text.Length)
                {
                    char character = text[cursor++];
                    if (character == '"') return value.ToString();
                    if (character < 32) throw new FormatException("Unescaped JSON control character.");
                    if (character != '\\') { value.Append(character); continue; }
                    if (cursor >= text.Length) throw new FormatException("Incomplete JSON escape.");
                    char escaped = text[cursor++];
                    switch (escaped)
                    {
                        case '"': value.Append('"'); break;
                        case '\\': value.Append('\\'); break;
                        case '/': value.Append('/'); break;
                        case 'b': value.Append('\b'); break;
                        case 'f': value.Append('\f'); break;
                        case 'n': value.Append('\n'); break;
                        case 'r': value.Append('\r'); break;
                        case 't': value.Append('\t'); break;
                        case 'u':
                            if (cursor + 4 > text.Length) throw new FormatException("Incomplete Unicode escape.");
                            value.Append((char)int.Parse(text.Substring(cursor, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture));
                            cursor += 4;
                            break;
                        default: throw new FormatException("Invalid JSON escape.");
                    }
                }
                throw new FormatException("Unterminated JSON string.");
            }
            double Numeric()
            {
                int start = cursor;
                Consume('-');
                if (Consume('0')) { if (cursor < text.Length && char.IsDigit(text[cursor])) throw new FormatException("Leading zero in JSON number."); }
                else { int digits = cursor; while (cursor < text.Length && char.IsDigit(text[cursor])) cursor++; if (digits == cursor) throw new FormatException("Invalid JSON number."); }
                if (Consume('.')) { int digits = cursor; while (cursor < text.Length && char.IsDigit(text[cursor])) cursor++; if (digits == cursor) throw new FormatException("Invalid JSON fraction."); }
                if (Consume('e') || Consume('E'))
                { if (!Consume('+')) Consume('-'); int digits = cursor; while (cursor < text.Length && char.IsDigit(text[cursor])) cursor++; if (digits == cursor) throw new FormatException("Invalid JSON exponent."); }
                return double.Parse(text.Substring(start, cursor - start), NumberStyles.Float, CultureInfo.InvariantCulture);
            }
            bool Literal(string value)
            {
                if (cursor + value.Length > text.Length || string.CompareOrdinal(text, cursor, value, 0, value.Length) != 0) return false;
                cursor += value.Length;
                return true;
            }
            void SkipSpace() { while (cursor < text.Length && (text[cursor] == ' ' || text[cursor] == '\t' || text[cursor] == '\r' || text[cursor] == '\n')) cursor++; }
            bool Consume(char character) { if (cursor >= text.Length || text[cursor] != character) return false; cursor++; return true; }
            void Require(char character) { if (!Consume(character)) throw new FormatException("Expected JSON '" + character + "'."); }
        }
    }
}
