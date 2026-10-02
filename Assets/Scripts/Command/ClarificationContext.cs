using System;
using System.Collections.Generic;
using System.Linq;

namespace ControlRoom
{
    public sealed class ClarificationContext
    {
        public Command Pending { get; private set; }
        public IReadOnlyList<ObjectRecord> Candidates => candidates;
        readonly List<ObjectRecord> candidates = new List<ObjectRecord>();
        string pendingCode = "";

        public void Begin(ValidationResult result)
        {
            Clear();
            if (result == null || result.valid || result.command == null) return;
            if (result.code != "AMBIGUOUS_OBJECT" && result.code != "MISSING_OBJECT" && result.code != "MISSING_LOCATION") return;
            Pending = CommandVocabulary.Clone(result.command);
            candidates.AddRange(result.candidates ?? new List<ObjectRecord>());
            pendingCode = result.code;
        }

        public ValidationResult Resolve(string response, WorldContext context)
        {
            if (Pending == null) return new ValidationResult { code = "NO_PENDING_COMMAND", message = "보완할 명령이 없습니다." };
            if (string.IsNullOrWhiteSpace(response)) return StillPending("대상이나 장소를 알려 주세요.");
            Command revised = CommandVocabulary.Clone(Pending);
            if (revised.sequence.Count == 0) revised.sequence.Add(new CommandStep(revised.action, revised.locationId, revised.objectId));
            string room = CommandVocabulary.FindLocation(response);
            string objectId = CommandVocabulary.FindObject(response);
            var step = revised.sequence.FirstOrDefault(s => pendingCode == "MISSING_LOCATION" ? s.action == CommandAction.MOVE && string.IsNullOrEmpty(s.locationId) :
                pendingCode == "MISSING_OBJECT" ? (s.action == CommandAction.OPEN || s.action == CommandAction.INSPECT || s.action == CommandAction.PICKUP) && string.IsNullOrEmpty(s.objectId) :
                candidates.Any(candidate => CommandVocabulary.MatchesObject(s.objectId, candidate)) && CommandVocabulary.IsGenericObject(s.objectId));
            if (step == null) return StillPending("기존 명령의 대상이 아직 명확하지 않습니다.");
            if (pendingCode == "MISSING_LOCATION")
            {
                if (string.IsNullOrEmpty(room)) return StillPending("어느 장소로 이동할까요?");
                step.locationId = room;
            }
            else if (pendingCode == "MISSING_OBJECT")
            {
                if (string.IsNullOrEmpty(objectId)) return StillPending("어떤 물체를 말씀하시는 건가요?");
                step.objectId = objectId;
                if (!string.IsNullOrEmpty(room)) step.locationId = room;
            }
            else
            {
                var selected = candidates.Where(candidate =>
                    string.Equals(candidate.Id, response.Trim(), StringComparison.OrdinalIgnoreCase) ||
                    (!string.IsNullOrEmpty(room) && CommandVocabulary.MatchesRoom(candidate, room)) ||
                    (!string.IsNullOrEmpty(objectId) && !CommandVocabulary.IsGenericObject(objectId) && CommandVocabulary.MatchesObject(objectId, candidate)) ||
                    (!string.IsNullOrEmpty(candidate.Label) && response.Contains(candidate.Label))).ToList();
                if (selected.Count != 1) return StillPending("후보 중 하나를 정확히 지정해 주세요: " + string.Join(", ", candidates.Select(candidate => candidate.Label)));
                step.objectId = selected[0].Id;
                // Destination clarification does not rewrite a door's physical interaction room.
                step.locationId = selected[0].LocationId;
            }
            revised.action = revised.sequence[0].action;
            revised.objectId = revised.sequence[0].objectId;
            revised.locationId = revised.sequence[0].locationId;
            var result = new CommandValidator().Validate(revised, context);
            if (result.valid) Clear();
            else if (result.code == "AMBIGUOUS_OBJECT" || result.code == "MISSING_OBJECT" || result.code == "MISSING_LOCATION") Begin(result);
            return result;
        }

        ValidationResult StillPending(string message)
        {
            return new ValidationResult { command = CommandVocabulary.Clone(Pending), code = pendingCode, message = message, candidates = new List<ObjectRecord>(candidates) };
        }

        public void Clear() { Pending = null; candidates.Clear(); pendingCode = ""; }
    }
}
