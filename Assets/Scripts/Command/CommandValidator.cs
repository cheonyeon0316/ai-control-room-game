using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace ControlRoom
{
    public sealed class CommandValidator
    {
        public ValidationResult Validate(Command source, WorldContext context)
        {
            if (source == null) return Fail(null, "INVALID_COMMAND", "명령이 없습니다.");
            Command command = CommandVocabulary.Clone(source);
            if (!Enum.IsDefined(typeof(CommandTarget), command.target)) return Fail(command, "INVALID_TARGET", "담당자가 존재하지 않습니다.");
            if (!Enum.IsDefined(typeof(CommandAction), command.action)) return Fail(command, "INVALID_ACTION", "지원하지 않는 행동입니다.");
            if (context == null) return Fail(command, "INVALID_CONTEXT", "현장 정보를 사용할 수 없습니다.");
            if (command.sequence.Any(step => step == null)) return Fail(command, "INVALID_ACTION", "비어 있는 행동은 실행할 수 없습니다.");
            if (command.sequence.Count > 32) return Fail(command, "INVALID_COMMAND", "한 번에 32개 이하의 행동을 지시해 주세요.");
            if (!IsFinite(command.confidence) || command.confidence < 0 || command.confidence > 1) return Fail(command, "INVALID_CONFIDENCE", "명령 신뢰도 범위가 잘못되었습니다.");
            foreach (string restriction in command.restrictions)
                if (!IsRestriction(restriction)) return Fail(command, "UNSUPPORTED_RESTRICTION", "지원하지 않는 제한 조건입니다: " + restriction);
            if (command.target == CommandTarget.FIELD_AGENT)
                foreach (string condition in command.conditions)
                    if (condition != "GUARD_CLEAR") return Fail(command, "UNSUPPORTED_CONDITION", "현장 요원이 확인할 수 없는 실행 조건입니다: " + condition);
            var steps = command.sequence.Count == 0
                ? new List<CommandStep> { new CommandStep(command.action, command.locationId, command.objectId) }
                : command.sequence;
            string projectedLocation = context.locationId ?? "";
            foreach (CommandStep step in steps)
            {
                if (!Enum.IsDefined(typeof(CommandAction), step.action)) return Fail(command, "INVALID_ACTION", "지원하지 않는 행동입니다.");
                if (!string.IsNullOrEmpty(step.locationId))
                {
                    string normalized = CommandVocabulary.NormalizeLocation(step.locationId);
                    if (normalized.Length == 0 || context.knownLocations == null || !context.knownLocations.Contains(normalized))
                        return Fail(command, "UNKNOWN_LOCATION", "알려진 장소가 아닙니다: " + step.locationId);
                    step.locationId = normalized;
                }
                if (command.target == CommandTarget.ANALYSIS_SYSTEM)
                {
                    if (step.action != CommandAction.ANALYZE) return Fail(command, "INVALID_TARGET_ACTION", "분석 시스템에는 분석 업무를 요청해 주세요.");
                    continue;
                }
                if (step.action == CommandAction.ANALYZE) return Fail(command, "INVALID_TARGET_ACTION", "분석 업무는 분석 시스템을 선택해 주세요.");
                if (!IsFinite(step.duration) || step.duration <= 0 || step.duration > 600) return Fail(command, "INVALID_DURATION", "행동 시간은 0초 초과, 600초 이하이어야 합니다.");
                if (step.action == CommandAction.PICKUP && command.restrictions.Contains("DO_NOT_PICKUP")) return Fail(command, "RESTRICTION_CONFLICT", "가져오지 말라는 제한과 획득 행동이 충돌합니다.");
                if (step.action == CommandAction.OPEN && command.restrictions.Contains("DO_NOT_OPEN")) return Fail(command, "RESTRICTION_CONFLICT", "열지 말라는 제한과 열기 행동이 충돌합니다.");
                if (step.action == CommandAction.MOVE)
                {
                    if (string.IsNullOrEmpty(step.locationId)) return Fail(command, "MISSING_LOCATION", "어느 장소로 이동할까요?");
                    projectedLocation = step.locationId == "EXIT" ? "ENTRANCE" : step.locationId;
                    continue;
                }
                bool needsObject = step.action == CommandAction.INSPECT || step.action == CommandAction.OPEN || step.action == CommandAction.PICKUP;
                if (needsObject && string.IsNullOrEmpty(step.objectId)) return Fail(command, "MISSING_OBJECT", "어떤 물체를 말씀하시는 건가요?");
                if (string.IsNullOrEmpty(step.objectId)) continue;
                string requested = step.objectId.ToUpperInvariant();
                if (requested == "SERVER_TERMINAL") requested = "TERMINAL";
                var allMatching = (context.objects ?? new List<ObjectRecord>()).Where(record => CommandVocabulary.MatchesObject(requested, record)).ToList();
                if (allMatching.Count == 0) return Fail(command, "OBJECT_NOT_FOUND", "해당 물체를 찾을 수 없습니다: " + step.objectId);
                bool generic = CommandVocabulary.IsGenericObject(requested);
                var matching = allMatching;
                if (generic)
                {
                    string desiredRoom = string.IsNullOrEmpty(step.locationId) ? projectedLocation : step.locationId;
                    matching = matching.Where(record => CommandVocabulary.MatchesRoom(record, desiredRoom)).ToList();
                }
                if (matching.Count == 0) return Fail(command, "OBJECT_NOT_IN_LOCATION", "해당 장소에서 물체를 확인할 수 없습니다.");
                var known = matching.Where(record => record.IsKnown).ToList();
                if (known.Count == 0) return Fail(command, "UNKNOWN_OBJECT", "현장 요원이 아직 확인하지 않은 물체입니다. 먼저 주변을 조사해 주세요.");
                var available = known.Where(record => record.IsAvailable).ToList();
                if (available.Count == 0) return Fail(command, "OBJECT_UNAVAILABLE", "현재 사용할 수 없는 물체입니다.");
                if (available.Count > 1)
                {
                    SyncTopLevel(command, steps);
                    return new ValidationResult
                    {
                        valid = false, code = "AMBIGUOUS_OBJECT", command = command, candidates = available,
                        message = "어떤 " + KindLabel(requested) + "을 말씀하시는 건가요? " + string.Join(", ", available.Select(record => record.Label))
                    };
                }
                ObjectRecord selected = available[0];
                // Doors still require the agent to be on their physical corridor side.
                if (!string.Equals(selected.LocationId, projectedLocation, StringComparison.OrdinalIgnoreCase))
                    return Fail(command, "OUT_OF_REACH", "먼저 " + selected.LocationId + "로 이동해 주세요.");
                if (step.action == CommandAction.OPEN && IsTerminal(selected) && command.restrictions.Contains("DO_NOT_DESTROY"))
                    return Fail(command, "RESTRICTION_CONFLICT", "단말기 실행은 증거를 파괴할 수 있어 해당 제한 조건으로 실행할 수 없습니다.");
                if (step.action == CommandAction.OPEN && !IsDoor(selected) && !IsCabinet(selected) && !IsTerminal(selected))
                    return Fail(command, "INVALID_OBJECT_ACTION", "이 물체에는 열기 행동을 사용할 수 없습니다.");
                if (step.action == CommandAction.PICKUP && !(selected.Kind == "USB" || selected.Kind == "STORAGE_DEVICE" || selected.Kind == "KEYCARD" || selected.Id == "USB" || selected.Id == "KEYCARD"))
                    return Fail(command, "INVALID_OBJECT_ACTION", "이 물체는 휴대할 수 없습니다.");
                step.objectId = selected.Id;
                if (string.IsNullOrEmpty(step.locationId)) step.locationId = selected.LocationId;
            }
            SyncTopLevel(command, steps);
            return new ValidationResult { valid = true, command = command, code = "VALID", message = "명령을 확인했습니다." };
        }

        static bool IsFinite(float value) { return !float.IsNaN(value) && !float.IsInfinity(value); }
        static bool IsDoor(ObjectRecord record) { return string.Equals(record.Kind, "DOOR", StringComparison.OrdinalIgnoreCase) || (record.Id ?? "").EndsWith("_DOOR", StringComparison.OrdinalIgnoreCase); }
        static bool IsCabinet(ObjectRecord record) { return string.Equals(record.Kind, "CABINET", StringComparison.OrdinalIgnoreCase) || (record.Id ?? "").EndsWith("_CABINET", StringComparison.OrdinalIgnoreCase); }
        static bool IsTerminal(ObjectRecord record) { return string.Equals(record.Kind, "TERMINAL", StringComparison.OrdinalIgnoreCase) || (record.Id ?? "").EndsWith("_TERMINAL", StringComparison.OrdinalIgnoreCase) || record.Id == "TERMINAL"; }
        public static bool IsRestriction(string value)
        {
            return value == "DO_NOT_PICKUP" || value == "AVOID_GUARD" || value == "DO_NOT_OPEN" || value == "DO_NOT_DESTROY" ||
                (!string.IsNullOrEmpty(value) && Regex.IsMatch(value, @"^LOCK_CODE:\d{3,8}$"));
        }
        static string KindLabel(string kind) { return kind == "DOOR" ? "문" : kind == "CABINET" ? "보관함" : kind == "DESK" ? "책상" : "물체"; }
        static void SyncTopLevel(Command command, List<CommandStep> steps)
        {
            if (command.sequence.Count == 0) command.sequence = steps;
            if (steps.Count == 0) return;
            command.action = steps[0].action;
            command.objectId = steps[0].objectId;
            command.locationId = steps[0].locationId;
        }
        static ValidationResult Fail(Command command, string code, string message)
        { return new ValidationResult { command = command, code = code, message = message }; }
    }
}
