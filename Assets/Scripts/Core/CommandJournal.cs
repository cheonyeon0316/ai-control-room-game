using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ControlRoom
{
    [Serializable]
    public sealed class CommandJournalEntry
    {
        public string Prompt = "";
        public Command ParsedCommand;
        public CommandTarget? RequestTarget;
        public CommandStatus Status;
        public string Message = "";
        public int ClarificationCount;
        public bool VerificationRequest;
        public int RetryCount;
        public string Timestamp = "";
        public List<CommandStatus> StatusHistory = new List<CommandStatus>();
        public string OriginalPrompt => Prompt;
        public string ExecutionResult => Message;
    }

    /// <summary>Observed action counts, not a skill score or an assertion about player ability.</summary>
    public sealed class CommandSkillProfile
    {
        public int Commands, GoalDefinition, Context, Constraint, TaskDecomposition;
        public int Delegation, Verification, Iteration, OutputFormatting, CrossValidation;
        public int FieldAgentCommands, AnalysisCommands, Clarifications, FailedCommands;
        public string Recommendation = "";
    }

    public sealed class CommandJournal
    {
        private readonly List<CommandJournalEntry> entries = new List<CommandJournalEntry>();
        public IReadOnlyList<CommandJournalEntry> Entries => entries.AsReadOnly();

        public void Record(string prompt, Command command, CommandStatus status, string message = "", int clarifications = 0, bool verification = false, CommandTarget? requestTarget = null)
        {
            prompt = prompt ?? "";
            // A real parsed command is authoritative; target metadata identifies requests before parsing.
            CommandTarget? target = command != null ? (CommandTarget?)command.target : requestTarget;
            // RECEIVED denotes a new submission; following states update that submission.
            var entry = status == CommandStatus.RECEIVED ? null : entries.LastOrDefault(item => item.Prompt == prompt &&
                item.Status != CommandStatus.COMPLETED && item.Status != CommandStatus.FAILED &&
                (!target.HasValue || !item.RequestTarget.HasValue || item.RequestTarget == target));
            if (entry == null)
            {
                int retries = entries.Count(item => item.Status == CommandStatus.FAILED && SameRequest(item, prompt, command, target));
                entry = new CommandJournalEntry { Prompt = prompt, RetryCount = retries, Timestamp = DateTime.UtcNow.ToString("o") };
                entries.Add(entry);
            }
            if (command != null) entry.ParsedCommand = CloneCommand(command);
            if (target.HasValue) entry.RequestTarget = target;
            entry.Status = status;
            entry.Message = message ?? "";
            entry.ClarificationCount = Math.Max(entry.ClarificationCount, Math.Max(0, clarifications));
            entry.VerificationRequest |= verification;
            if (entry.StatusHistory.Count == 0 || entry.StatusHistory[entry.StatusHistory.Count - 1] != status) entry.StatusHistory.Add(status);
        }

        public CommandSkillProfile GetProfile(MissionState state)
        {
            // UI must explicitly provide the actual mission state; gameplay has no numeric profile.
            if (state != MissionState.Result) return null;
            var profile = new CommandSkillProfile { Commands = entries.Count };
            foreach (var entry in entries)
            {
                var command = entry.ParsedCommand;
                profile.Clarifications += entry.ClarificationCount;
                if (entry.Status == CommandStatus.FAILED) profile.FailedCommands++;
                if (entry.VerificationRequest) profile.Verification++;
                if (entry.RetryCount > 0 || entry.ClarificationCount > 0) profile.Iteration++;
                if (ContainsAny(entry.Prompt, "교차", "충돌", "비교", "cross", "compare")) profile.CrossValidation++;
                if (command == null) continue;
                if (!string.IsNullOrEmpty(command.locationId) || !string.IsNullOrEmpty(command.objectId)) profile.Context++;
                if (!string.IsNullOrEmpty(command.objectId) || ContainsAny(entry.Prompt, "USB", "회수", "탈출", "목표", "유출")) profile.GoalDefinition++;
                if ((command.conditions != null && command.conditions.Count > 0) || (command.restrictions != null && command.restrictions.Count > 0)) profile.Constraint++;
                if (command.sequence != null && command.sequence.Count > 1) profile.TaskDecomposition++;
                if (command.target == CommandTarget.ANALYSIS_SYSTEM) { profile.AnalysisCommands++; profile.Delegation++; }
                else profile.FieldAgentCommands++;
                if ((!string.IsNullOrEmpty(command.outputFormat) && command.outputFormat != "summary") ||
                    ContainsAny(entry.Prompt, "시간순", "인물별", "목록", "표로", "비교", "근거 포함", "요약")) profile.OutputFormatting++;
            }
            profile.Recommendation = profile.Verification == 0
                ? "분석 결과를 받은 뒤 출처와 작성 시각을 요청해 보세요."
                : profile.CrossValidation == 0 ? "서로 독립적인 자료를 비교하면 오래된 정보의 오류를 찾을 수 있습니다."
                : "출처 확인과 자료 비교를 수행했습니다. 다음 임무에서도 결과를 현장 관찰과 연결해 보세요.";
            return profile;
        }

        public CommandSkillProfile BuildProfile(MissionState state) => GetProfile(state);

        public string GetResultSummary(MissionState state)
        {
            var profile = GetProfile(state);
            if (profile == null) return "";
            var text = new StringBuilder("COMMAND PROFILE — 관찰된 행동 횟수\n");
            text.AppendLine("명령: " + profile.Commands + " / 수정: " + profile.Iteration + " / 확인 질문: " + profile.Clarifications);
            text.AppendLine("목표 명시: " + profile.GoalDefinition + " / 맥락: " + profile.Context + " / 조건: " + profile.Constraint);
            text.AppendLine("업무 분해: " + profile.TaskDecomposition + " / 분석 위임: " + profile.Delegation);
            text.AppendLine("근거 요청: " + profile.Verification + " / 교차 검증: " + profile.CrossValidation + " / 결과 형태: " + profile.OutputFormatting);
            text.AppendLine(profile.Recommendation);
            return text.ToString();
        }

        public string ResultSummary(MissionState state) => GetResultSummary(state);
        public string GetResultSummary(MissionRules rules) => rules == null ? "" : GetResultSummary(rules.State);
        public void Clear() => entries.Clear();

        private static bool SameRequest(CommandJournalEntry previous, string prompt, Command command, CommandTarget? target)
        {
            if (target.HasValue && previous.RequestTarget.HasValue && previous.RequestTarget != target) return false;
            if (previous.Prompt.Trim() == prompt.Trim()) return true;
            var parsed = previous.ParsedCommand;
            return command != null && parsed != null && parsed.target == command.target && parsed.action == command.action &&
                   parsed.objectId == command.objectId && parsed.locationId == command.locationId && parsed.action != CommandAction.ANALYZE;
        }

        private static bool ContainsAny(string value, params string[] terms) => terms.Any(term => (value ?? "").IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0);

        private static Command CloneCommand(Command value)
        {
            return new Command
            {
                target = value.target, action = value.action, objectId = value.objectId, locationId = value.locationId,
                confidence = value.confidence, outputFormat = value.outputFormat, prompt = value.prompt,
                conditions = value.conditions == null ? new List<string>() : new List<string>(value.conditions),
                restrictions = value.restrictions == null ? new List<string>() : new List<string>(value.restrictions),
                sequence = value.sequence == null ? new List<CommandStep>() : value.sequence.Select(step => new CommandStep(step.action, step.locationId, step.objectId, step.duration)).ToList()
            };
        }
    }
}
