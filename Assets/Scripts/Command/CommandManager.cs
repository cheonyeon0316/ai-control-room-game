using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ControlRoom
{
    public sealed class CommandManager : MonoBehaviour
    {
        public Command LastCommand { get; private set; }
        public AnalysisResult LastAnalysis { get; private set; }
        public CommandStatus LastStatus { get; private set; }
        public string LastResponse { get; private set; } = "명령을 기다립니다.";
        public bool IsFieldProcessing { get; private set; }
        public bool IsAnalysisProcessing { get; private set; }
        public bool IsProcessing => IsFieldProcessing || IsAnalysisProcessing;
        public bool UseLlm;
        public ClarificationContext Clarification { get; private set; } = new ClarificationContext();
        public event Action<CommandStatus, string> OnResponse;
        public event Action<string> OnHistory;
        private readonly RuleCommandParser parser = new RuleCommandParser();
        private readonly CommandValidator validator = new CommandValidator();
        private GameManager game;
        private int clarificationCount;
        private Command activeFieldCommand, activeAnalysisCommand;
        private string activeFieldPrompt, activeAnalysisPrompt;
        private int activeFieldClarifications;
        private Command pendingPurgeCommand;
        private string pendingPurgePrompt = "";
        public void Initialize(GameManager owner) { game = owner; }

        public void Submit(string prompt, CommandTarget target)
        {
            prompt = (prompt ?? "").Trim();
            if (prompt.Length == 0) { Respond(CommandStatus.FAILED, "명령을 입력해 주세요."); return; }
            if (!game.CanAct) { Respond(CommandStatus.FAILED, "미션 진행 중에 명령을 보낼 수 있습니다."); return; }
            OnHistory?.Invoke((target == CommandTarget.FIELD_AGENT ? "FIELD" : "ANALYSIS") + " > " + prompt);
            if ((target == CommandTarget.FIELD_AGENT && IsFieldProcessing) || (target == CommandTarget.ANALYSIS_SYSTEM && IsAnalysisProcessing))
            { Respond(CommandStatus.FAILED, "담당자가 작업 중입니다. 완료 응답을 기다려 주세요."); return; }
            if (pendingPurgeCommand != null)
            {
                if (target == CommandTarget.FIELD_AGENT && IsPurgeConfirmation(prompt))
                { ConfirmPendingPurge(); return; }
                if (target == CommandTarget.FIELD_AGENT && IsPurgeCancellation(prompt))
                { CancelPendingPurge("PURGE를 취소했습니다. 증거는 보존되었습니다."); return; }
                CancelPendingPurge("새 명령이 접수되어 대기 중인 PURGE를 취소했습니다. 증거는 보존되었습니다.");
            }
            Respond(CommandStatus.RECEIVED, "명령 수신.");
            string safetyError = RuleCommandParser.ValidateNaturalLanguageSafety(prompt, target);
            if (!string.IsNullOrEmpty(safetyError))
            {
                Respond(CommandStatus.FAILED, safetyError);
                game.Journal.Record(prompt, null, CommandStatus.FAILED, safetyError, 0, false, requestTarget: target);
                return;
            }
            if (target == CommandTarget.FIELD_AGENT && Clarification.Pending != null)
            {
                clarificationCount++;
                var resolved = Clarification.Resolve(prompt, game.World.GetContext());
                HandleValidated(resolved, prompt);
                return;
            }
            if (UseLlm && !prompt.TrimStart().StartsWith("{", StringComparison.Ordinal)) StartCoroutine(InterpretExternal(prompt, target));
            else HandleParsed(parser.Parse(prompt, target), prompt, target);
        }

        private IEnumerator InterpretExternal(string prompt, CommandTarget target)
        {
            SetBusy(target, true);
            TrackRequest(target, null, prompt, 0, CommandStatus.PROCESSING);
            Respond(CommandStatus.PROCESSING, "명령 해석 중.");
            ParseResult result = null;
            yield return game.Interpreter.Interpret(prompt, target, parsed => result = parsed);
            while (game.Rules.State == MissionState.Playing && game.Rules.Paused) yield return null;
            if (game.Rules.State != MissionState.Playing)
            { FailRequest(target, "미션이 종료되어 명령 해석 요청을 취소했습니다."); yield break; }
            SetBusy(target, false);
            ClearRequest(target);
            HandleParsed(result ?? new ParseResult { error = "COMMAND PROCESSING FAILED" }, prompt, target);
        }

        private void HandleParsed(ParseResult parsed, string prompt, CommandTarget requestedTarget)
        {
            Respond(CommandStatus.PROCESSING, "명령 구조와 실행 가능 여부 확인.");
            if (parsed == null || !parsed.Success)
            {
                var error = parsed?.error ?? "COMMAND PROCESSING FAILED";
                Respond(CommandStatus.FAILED, error);
                game.Journal.Record(prompt, null, CommandStatus.FAILED, error, 0, false, requestTarget: requestedTarget);
                return;
            }
            LastCommand = parsed.command;
            if (parsed.command.target == CommandTarget.ANALYSIS_SYSTEM)
            {
                var validation = validator.Validate(parsed.command, game.World.GetContext());
                if (validation.valid) StartCoroutine(Analyze(parsed.command, prompt));
                else
                {
                    Respond(CommandStatus.FAILED, validation.message);
                    game.Journal.Record(prompt, parsed.command, CommandStatus.FAILED, validation.message, 0, false);
                }
            }
            else HandleValidated(validator.Validate(parsed.command, game.World.GetContext()), prompt);
        }

        private void HandleValidated(ValidationResult result, string prompt)
        {
            if (!result.valid)
            {
                if (result.candidates.Count > 1 || result.code.StartsWith("AMBIGUOUS", StringComparison.Ordinal) || result.code == "MISSING_LOCATION" || result.code == "MISSING_OBJECT")
                {
                    Clarification.Begin(result);
                    Respond(CommandStatus.NEEDS_CLARIFICATION, result.message);
                    game.Journal.Record(prompt, result.command, CommandStatus.NEEDS_CLARIFICATION, result.message, clarificationCount, false);
                }
                else
                {
                    Respond(CommandStatus.FAILED, result.message);
                    game.Journal.Record(prompt, result.command, CommandStatus.FAILED, result.message, clarificationCount, false);
                }
                return;
            }
            LastCommand = result.command;
            Clarification.Clear();
            if (ContainsPurge(result.command))
            {
                BeginPurgeConfirmation(result.command, prompt);
                return;
            }
            StartCoroutine(Execute(result.command, prompt));
        }

        private IEnumerator Execute(Command command, string prompt, bool purgeConfirmed = false)
        {
            IsFieldProcessing = true;
            int resolvedClarifications = clarificationCount;
            clarificationCount = 0;
            TrackRequest(CommandTarget.FIELD_AGENT, command, string.IsNullOrEmpty(command.prompt) ? prompt : command.prompt, resolvedClarifications, CommandStatus.EXECUTING);
            Respond(CommandStatus.EXECUTING, "요원 실행 중 · " + command.action);
            bool succeeded = true;
            string failureMessage = "";
            foreach (var step in command.Steps)
            {
                while (game.Rules.State == MissionState.Playing && game.Rules.Paused) yield return null;
                if (game.Rules.State == MissionState.Success) break;
                if (game.Rules.State != MissionState.Playing) { succeeded = false; break; }
                if (command.conditions.Contains("GUARD_CLEAR"))
                {
                    float waitedSeconds = 0;
                    bool announcedWait = false;
                    while (game.Rules.State == MissionState.Playing)
                    {
                        if (game.Rules.Paused) { yield return null; continue; }
                        if (game.World.Guard == null || game.World.Guard.GuardClearFor(game.World.Agent)) break;
                        if (!announcedWait)
                        {
                            announcedWait = true;
                            Respond(CommandStatus.EXECUTING, "경비가 통과하는 조건을 기다립니다. 안전해지면 지시한 행동을 시작합니다.");
                        }
                        waitedSeconds += Time.deltaTime;
                        if (waitedSeconds >= 18f)
                        {
                            succeeded = false;
                            failureMessage = "경비가 계속 주변에 있어 GUARD_CLEAR 조건을 충족하지 못했습니다. 행동을 시작하지 않았습니다.";
                            break;
                        }
                        yield return null;
                    }
                    if (!succeeded) break;
                    if (game.Rules.State != MissionState.Playing)
                    {
                        succeeded = false;
                        failureMessage = string.IsNullOrEmpty(game.Rules.FailureReason) ? "임무가 종료되어 조건 대기 중인 행동을 취소했습니다." : game.Rules.FailureReason;
                        break;
                    }
                }
                var restrictions = new List<string>(command.restrictions);
                if (command.conditions.Contains("GUARD_CLEAR") && !restrictions.Contains("AVOID_GUARD")) restrictions.Add("AVOID_GUARD");
                if (purgeConfirmed && step.action == CommandAction.PURGE) restrictions.Add("PURGE_CONFIRMED");
                yield return game.World.Agent.ExecuteStep(step, restrictions);
                if (!game.World.Agent.LastSuccess) { succeeded = false; break; }
            }
            IsFieldProcessing = false;
            ClearRequest(CommandTarget.FIELD_AGENT);
            var status = succeeded ? CommandStatus.COMPLETED : CommandStatus.FAILED;
            Respond(status, string.IsNullOrEmpty(failureMessage) ? game.World.Agent.LastMessage : failureMessage);
            game.Journal.Record(string.IsNullOrEmpty(command.prompt) ? prompt : command.prompt, command, status, LastResponse, resolvedClarifications, false);
        }

        private IEnumerator Analyze(Command command, string prompt)
        {
            IsAnalysisProcessing = true;
            TrackRequest(CommandTarget.ANALYSIS_SYSTEM, command, prompt, 0, CommandStatus.EXECUTING);
            Respond(CommandStatus.EXECUTING, "자료 분석 중. 현장과 시간은 계속 진행됩니다.");
            float remaining = 1.3f;
            while (remaining > 0f)
            {
                if (game.Rules.State != MissionState.Playing)
                { FailRequest(CommandTarget.ANALYSIS_SYSTEM, "미션이 종료되어 분석 요청을 취소했습니다."); yield break; }
                if (!game.Rules.Paused) remaining -= Time.deltaTime;
                yield return null;
            }
            LastAnalysis = game.Analysis.Query(command.prompt, command.outputFormat);
            game.Evidence.AddRange(LastAnalysis.evidence);
            IsAnalysisProcessing = false;
            ClearRequest(CommandTarget.ANALYSIS_SYSTEM);
            Respond(CommandStatus.COMPLETED, LastAnalysis.text);
            bool verified = System.Text.RegularExpressions.Regex.IsMatch(prompt, "근거|충돌|검증|비교|source|evidence|verify|verification|contradict|cross.?check|compare", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            game.Journal.Record(prompt, command, CommandStatus.COMPLETED, LastResponse, 0, verified);
        }

        private void SetBusy(CommandTarget target, bool busy)
        { if (target == CommandTarget.FIELD_AGENT) IsFieldProcessing = busy; else IsAnalysisProcessing = busy; }
        private void TrackRequest(CommandTarget target, Command command, string prompt, int clarifications, CommandStatus status)
        {
            if (target == CommandTarget.FIELD_AGENT)
            { activeFieldCommand = command; activeFieldPrompt = prompt; activeFieldClarifications = clarifications; }
            else { activeAnalysisCommand = command; activeAnalysisPrompt = prompt; }
            game.Journal.Record(prompt, command, status, "요청 처리 중.", clarifications, false, requestTarget: target);
        }
        private void ClearRequest(CommandTarget target)
        {
            if (target == CommandTarget.FIELD_AGENT)
            { activeFieldCommand = null; activeFieldPrompt = null; activeFieldClarifications = 0; }
            else { activeAnalysisCommand = null; activeAnalysisPrompt = null; }
        }
        private void FailRequest(CommandTarget target, string reason)
        {
            if (target == CommandTarget.FIELD_AGENT && activeFieldPrompt != null)
                game.Journal.Record(activeFieldPrompt, activeFieldCommand, CommandStatus.FAILED, reason, activeFieldClarifications, false, requestTarget: target);
            else if (target == CommandTarget.ANALYSIS_SYSTEM && activeAnalysisPrompt != null)
                game.Journal.Record(activeAnalysisPrompt, activeAnalysisCommand, CommandStatus.FAILED, reason, 0, false, requestTarget: target);
            SetBusy(target, false); ClearRequest(target); Respond(CommandStatus.FAILED, reason);
        }
        private void Respond(CommandStatus status, string message)
        { LastStatus = status; LastResponse = message; OnResponse?.Invoke(status, message); }
        public void CancelActive()
        {
            string reason = string.IsNullOrEmpty(game?.Rules.FailureReason) ? "미션이 종료되어 요청을 취소했습니다." : game.Rules.FailureReason;
            bool hadPending = activeFieldPrompt != null || activeAnalysisPrompt != null || Clarification.Pending != null;
            if (game != null)
            {
                if (activeFieldPrompt != null) game.Journal.Record(activeFieldPrompt, activeFieldCommand, CommandStatus.FAILED, reason, activeFieldClarifications, false, requestTarget: CommandTarget.FIELD_AGENT);
                if (activeAnalysisPrompt != null) game.Journal.Record(activeAnalysisPrompt, activeAnalysisCommand, CommandStatus.FAILED, reason, 0, false, requestTarget: CommandTarget.ANALYSIS_SYSTEM);
                if (Clarification.Pending != null) game.Journal.Record(Clarification.Pending.prompt, Clarification.Pending, CommandStatus.FAILED, reason, clarificationCount, false);
            }
            StopAllCoroutines(); IsFieldProcessing = false; IsAnalysisProcessing = false;
            ClearRequest(CommandTarget.FIELD_AGENT); ClearRequest(CommandTarget.ANALYSIS_SYSTEM); clarificationCount = 0;
            Clarification.Clear();
            if (hadPending) Respond(CommandStatus.FAILED, reason);
        }

        private void BeginPurgeConfirmation(Command command, string prompt)
        {
            pendingPurgeCommand = CommandVocabulary.Clone(command);
            pendingPurgePrompt = string.IsNullOrEmpty(command.prompt) ? prompt : command.prompt;
            string message = "증거 보관 터미널의 감사 증거를 영구 삭제합니다. 삭제하려면 다음 입력에서 '삭제 확정'이라고 입력하세요. 취소하거나 새 명령을 입력하면 증거를 보존합니다.";
            Respond(CommandStatus.NEEDS_CLARIFICATION, message);
            game.Journal.Record(pendingPurgePrompt, pendingPurgeCommand, CommandStatus.NEEDS_CLARIFICATION, message, clarificationCount, false, requestTarget: CommandTarget.FIELD_AGENT);
            clarificationCount = 0;
        }

        private void ConfirmPendingPurge()
        {
            Command command = pendingPurgeCommand;
            string originalPrompt = pendingPurgePrompt;
            pendingPurgeCommand = null;
            pendingPurgePrompt = "";
            ValidationResult validation = validator.Validate(command, game.World.GetContext());
            if (!validation.valid)
            {
                Respond(CommandStatus.FAILED, "PURGE를 다시 확인할 수 없습니다. " + validation.message);
                game.Journal.Record(originalPrompt, command, CommandStatus.FAILED, LastResponse, clarificationCount, false, requestTarget: CommandTarget.FIELD_AGENT);
                clarificationCount = 0;
                return;
            }
            LastCommand = validation.command;
            StartCoroutine(Execute(validation.command, originalPrompt, purgeConfirmed: true));
        }

        private void CancelPendingPurge(string message)
        {
            Command command = pendingPurgeCommand;
            string originalPrompt = pendingPurgePrompt;
            pendingPurgeCommand = null;
            pendingPurgePrompt = "";
            Respond(CommandStatus.COMPLETED, message);
            game.Journal.Record(originalPrompt, command, CommandStatus.COMPLETED, message, clarificationCount, false, requestTarget: CommandTarget.FIELD_AGENT);
            clarificationCount = 0;
        }

        private static bool ContainsPurge(Command command)
        {
            foreach (CommandStep step in command.Steps)
                if (step.action == CommandAction.PURGE) return true;
            return false;
        }

        private static bool IsPurgeConfirmation(string prompt)
        {
            return System.Text.RegularExpressions.Regex.IsMatch(prompt ?? "", @"^(?:삭제\s*확정|purge\s+confirm)$", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        }

        private static bool IsPurgeCancellation(string prompt)
        {
            return System.Text.RegularExpressions.Regex.IsMatch(prompt ?? "", @"^(?:취소|cancel)$", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        }
    }
}
