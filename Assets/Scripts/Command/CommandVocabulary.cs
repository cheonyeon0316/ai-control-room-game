using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace ControlRoom
{
    /// <summary>Authored vocabulary, not a source of object existence or mission truth.</summary>
    public static class CommandVocabulary
    {
        static readonly Dictionary<string, string[]> Locations = new Dictionary<string, string[]>
        {
            { "LABORATORY", new[] { "laboratory", "lab", "연구실", "실험실" } },
            { "SERVER_ROOM", new[] { "server room", "server_room", "서버실", "b-05" } },
            { "MAIN_HALL", new[] { "main hall", "main_hall", "hall", "메인 홀", "메인홀", "중앙 홀", "복도", "중앙홀" } },
            { "STORAGE", new[] { "storage room", "storage", "창고" } },
            { "EXIT", new[] { "exit", "탈출구", "출구" } },
            { "ENTRANCE", new[] { "entrance", "입구", "출입구" } }
        };

        public static string FindLocation(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return "";
            text = text.ToLowerInvariant();
            string found = "";
            int latest = -1;
            foreach (var entry in Locations)
            {
                int canonical = text.LastIndexOf(entry.Key.ToLowerInvariant(), StringComparison.Ordinal);
                if (canonical > latest) { latest = canonical; found = entry.Key; }
                foreach (var alias in entry.Value)
                {
                    int at = text.LastIndexOf(alias, StringComparison.Ordinal);
                    if (at > latest) { latest = at; found = entry.Key; }
                }
            }
            return found;
        }

        public static string NormalizeLocation(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return "";
            string upper = value.Trim().ToUpperInvariant();
            if (Locations.ContainsKey(upper)) return upper;
            string lower = value.Trim().ToLowerInvariant();
            foreach (var entry in Locations)
                foreach (var alias in entry.Value)
                    if (lower == alias) return entry.Key;
            return "";
        }

        public static string FindObject(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return "";
            string lower = text.ToLowerInvariant();
            var explicitId = Regex.Match(text, @"\b(?:LAB|STORAGE|SERVER|HALL|ENTRANCE)_(?:DOOR|CABINET|DESK|TERMINAL)\b", RegexOptions.IgnoreCase);
            if (explicitId.Success) return explicitId.Value.ToUpperInvariant() == "SERVER_TERMINAL" ? "TERMINAL" : explicitId.Value.ToUpperInvariant();
            string room = FindLocation(text);
            if (Regex.IsMatch(lower, @"usb|유에스비")) return "USB";
            if (Regex.IsMatch(lower, @"key\s?card|keycard|키카드|출입증")) return "KEYCARD";
            if (Regex.IsMatch(lower, @"storage device|저장장치|저장 장치")) return "STORAGE_DEVICE";
            if (Regex.IsMatch(lower, @"cabinet|캐비닛|캐비넷|보관함|사물함")) return RoomObject(room, "CABINET");
            if (Regex.IsMatch(lower, @"desk|책상")) return RoomObject(room, "DESK");
            if (Regex.IsMatch(lower, @"terminal|단말기|터미널")) return RoomObject(room, "TERMINAL");
            if (Regex.IsMatch(lower, @"door|문(?:을|이|으로|에|\s|$)|문을|문열")) return RoomObject(room, "DOOR");
            if (Regex.IsMatch(lower, @"물건|물체|object|item")) return "OBJECT";
            return "";
        }

        static string RoomObject(string room, string kind)
        {
            if (room == "SERVER_ROOM" && kind == "TERMINAL") return "TERMINAL";
            string prefix = room == "LABORATORY" ? "LAB" : room == "SERVER_ROOM" ? "SERVER" :
                room == "MAIN_HALL" ? "HALL" : room == "STORAGE" ? "STORAGE" : room == "ENTRANCE" ? "ENTRANCE" : "";
            return string.IsNullOrEmpty(prefix) ? kind : prefix + "_" + kind;
        }

        public static bool IsGenericObject(string value)
        {
            return value == "DOOR" || value == "DESK" || value == "CABINET" || value == "TERMINAL" ||
                value == "STORAGE_DEVICE" || value == "OBJECT" || value == "KEYCARD";
        }

        public static bool MatchesObject(string requested, ObjectRecord record)
        {
            if (record == null || string.IsNullOrEmpty(requested)) return false;
            string id = (record.Id ?? "").ToUpperInvariant();
            string kind = (record.Kind ?? "").ToUpperInvariant();
            requested = requested.ToUpperInvariant();
            if (requested == id) return true;
            if (requested == "OBJECT") return true;
            if (requested == "STORAGE_DEVICE") return kind == "STORAGE_DEVICE" || kind == "USB";
            return requested == kind || (IsGenericObject(requested) && id.EndsWith("_" + requested, StringComparison.Ordinal));
        }

        public static bool MatchesRoom(ObjectRecord record, string room)
        {
            if (record == null) return false;
            if (string.Equals(record.LocationId, room, StringComparison.OrdinalIgnoreCase)) return true;
            string prefix = room == "LABORATORY" ? "LAB_" : room == "SERVER_ROOM" ? "SERVER_" :
                room == "MAIN_HALL" ? "HALL_" : room == "STORAGE" ? "STORAGE_" : room == "ENTRANCE" ? "ENTRANCE_" : "";
            return prefix.Length > 0 && (record.Id ?? "").StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
        }

        public static Command Clone(Command source)
        {
            if (source == null) return null;
            var copy = new Command
            {
                target = source.target, action = source.action, objectId = source.objectId ?? "", locationId = source.locationId ?? "",
                outputFormat = source.outputFormat ?? "summary", confidence = source.confidence, prompt = source.prompt ?? "",
                conditions = source.conditions == null ? new List<string>() : new List<string>(source.conditions),
                restrictions = source.restrictions == null ? new List<string>() : new List<string>(source.restrictions)
            };
            if (source.sequence != null)
                foreach (var step in source.sequence)
                    copy.sequence.Add(step == null ? null : new CommandStep(step.action, step.locationId ?? "", step.objectId ?? "", step.duration));
            return copy;
        }
    }
}
