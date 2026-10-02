using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;

namespace ControlRoom
{
    public sealed class RuleCommandParser
    {
        // Verb matches preserve the player's order. Their subjects are read only from their own clause.
        static readonly Regex Verbs = new Regex(
            @"(?<pickup>\bpick\s*(?:it\s+|them\s+)?up\b|\bcollect\b|\bretrieve\b|가져(?:와|오|가)|집어|주워|획득|회수|챙겨)|" +
            @"(?<photo>\bphotograph\b|\btake\s+(?:a\s+)?photo\b|사진\s*(?:을\s*)?(?:찍|촬영)|촬영)|" +
            @"(?<open>\bopen\b|\bunlock\b|열어|열고|열면|열기|개방|잠금\s*해제)|" +
            @"(?<inspect>\binspect\b|\bexamine\b|\bcheck\b|확인|조사|살펴|검사)|" +
            @"(?<wait>\bwait\b|대기|기다려|기다리)|" +
            @"(?<hide>\bhide\b|숨어|숨기|숨(?:어|으)|은신)|" +
            @"(?<report>\breport\b|보고|알려|무엇이\s*보여|뭐가\s*보여)|" +
            @"(?<move>\bmove\b|\bgo\b|\breturn\b|\bhead\b|이동|돌아와|돌아가|복귀|탈출해|들어가|(?:^|\s)가(?:줘|라|자|\s|$))",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        public ParseResult Parse(string prompt, CommandTarget target)
        {
            if (string.IsNullOrWhiteSpace(prompt)) return Fail("명령을 입력해 주세요.");
            if (prompt.Length > 8000) return Fail("명령이 너무 깁니다.");
            if (!Enum.IsDefined(typeof(CommandTarget), target)) return Fail("지원하지 않는 담당자입니다.");
            if (prompt.TrimStart().StartsWith("{", StringComparison.Ordinal))
            {
                var structured = CommandJsonCodec.Parse(prompt);
                if (structured.Success && structured.command.target != target) return Fail("선택한 담당자와 JSON target이 다릅니다.");
                return structured;
            }
            var command = new Command { target = target, prompt = prompt };
            SetFormat(command, prompt);
            if (target == CommandTarget.ANALYSIS_SYSTEM)
            {
                command.action = CommandAction.ANALYZE;
                command.locationId = CommandVocabulary.FindLocation(prompt);
                CaptureConditions(command, prompt);
                return new ParseResult { command = command };
            }

            string text = prompt;
            // Removing only complete negative clauses prevents a prohibited verb from becoming a step.
            text = ExtractRestriction(text, command, @"(?:do\s+not|don['’]t|never)\s+(?:pick\s*(?:it\s+|them\s+)?up|collect|retrieve)(?:(?!\band\b|\bthen\b)[^.;\n])*|(?:가져오|가져가|가져와|집|줍|주워오|획득|회수|챙기)[^.;\n]{0,10}(?:마(?:세요|라)?|말(?:고|아|아줘))", "DO_NOT_PICKUP");
            text = ExtractRestriction(text, command, @"(?:do\s+not|don['’]t|never)\s+open(?:(?!\band\b|\bthen\b)[^.;\n])*|(?:열지|열지는|개방하지)\s*(?:마|말고)", "DO_NOT_OPEN");
            text = ExtractRestriction(text, command, @"(?:do\s+not|don['’]t|never)\s+(?:destroy|delete)(?:(?!\band\b|\bthen\b)[^.;\n])*|(?:파괴|삭제)하지\s*(?:마|말고)", "DO_NOT_DESTROY");
            if (Regex.IsMatch(text, @"\b(?:kill|shoot|attack|hack|teleport|destroy|delete)\b|죽여|쏴|공격|해킹|순간이동|파괴해|삭제해", RegexOptions.IgnoreCase))
                return Fail("지원하지 않는 행동입니다. 이동, 조사, 획득, 열기, 대기, 숨기, 보고, 사진 촬영을 지시해 주세요.");
            if (Regex.IsMatch(prompt, @"경비.*(?:피해|피하|들키지|발각되지|지나가)|\b(?:avoid\s+(?:the\s+)?guard|stealth|undetected)\b", RegexOptions.IgnoreCase))
                command.restrictions.Add("AVOID_GUARD");
            var code = Regex.Match(prompt, @"(?<!\d)(\d{3,8})\s*(?:로|으로|번)|(?:code|암호|비밀번호)\s*[:=]?\s*(\d{3,8})(?!\d)", RegexOptions.IgnoreCase);
            if (code.Success) command.restrictions.Add("LOCK_CODE:" + (code.Groups[1].Success ? code.Groups[1].Value : code.Groups[2].Value));
            CaptureConditions(command, prompt);
            var matches = Verbs.Matches(text);
            if (matches.Count == 0) return Fail("행동을 해석하지 못했습니다. 대상과 행동을 함께 입력해 주세요.");
            int previousEnd = 0;
            string rememberedLocation = "";
            for (int index = 0; index < matches.Count; index++)
            {
                Match match = matches[index];
                CommandAction action = ActionOf(match);
                string before = text.Substring(previousEnd, match.Index - previousEnd);
                int nextVerb = index + 1 < matches.Count ? matches[index + 1].Index : text.Length;
                string after = text.Substring(match.Index + match.Length, nextVerb - match.Index - match.Length);
                bool english = Regex.IsMatch(match.Value, @"^[a-z]", RegexOptions.IgnoreCase);
                string subject = english ? after : before;
                string location = CommandVocabulary.FindLocation(subject);
                string obj = CommandVocabulary.FindObject(subject);
                if (action == CommandAction.MOVE && !string.IsNullOrEmpty(obj) && obj.EndsWith("DOOR", StringComparison.Ordinal) && string.IsNullOrEmpty(location)) action = CommandAction.OPEN;
                if (action == CommandAction.MOVE)
                {
                    if (string.IsNullOrEmpty(location) && Regex.IsMatch(match.Value, "복귀|돌아|return|탈출", RegexOptions.IgnoreCase)) location = "EXIT";
                    rememberedLocation = location;
                    obj = "";
                }
                else if (action == CommandAction.WAIT || action == CommandAction.REPORT || action == CommandAction.HIDE)
                {
                    obj = "";
                }
                else if (string.IsNullOrEmpty(location) && !string.IsNullOrEmpty(rememberedLocation)) location = rememberedLocation;
                float duration = 3f;
                var seconds = Regex.Match(subject, @"(?<n>\d+(?:\.\d+)?)\s*(?:초|seconds?|secs?|s\b)", RegexOptions.IgnoreCase);
                if (seconds.Success)
                {
                    if (!float.TryParse(seconds.Groups["n"].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out duration) || duration <= 0 || duration > 600)
                        return Fail("대기 시간은 0초 초과, 600초 이하이어야 합니다.");
                }
                command.sequence.Add(new CommandStep(action, location, obj, duration));
                previousEnd = english ? match.Index + match.Length + after.Length : match.Index + match.Length;
                // In English the object belongs after the verb; don't subtract the next verb's position.
                if (previousEnd > nextVerb) previousEnd = nextVerb;
            }
            command.action = command.sequence[0].action;
            command.locationId = command.sequence[0].locationId;
            command.objectId = command.sequence[0].objectId;
            return new ParseResult { command = command };
        }

        static string ExtractRestriction(string text, Command command, string pattern, string restriction)
        {
            var regex = new Regex(pattern, RegexOptions.IgnoreCase);
            if (!regex.IsMatch(text)) return text;
            if (!command.restrictions.Contains(restriction)) command.restrictions.Add(restriction);
            return regex.Replace(text, " ");
        }

        static CommandAction ActionOf(Match match)
        {
            if (match.Groups["pickup"].Success) return CommandAction.PICKUP;
            if (match.Groups["photo"].Success) return CommandAction.PHOTO;
            if (match.Groups["open"].Success) return CommandAction.OPEN;
            if (match.Groups["inspect"].Success) return CommandAction.INSPECT;
            if (match.Groups["wait"].Success) return CommandAction.WAIT;
            if (match.Groups["hide"].Success) return CommandAction.HIDE;
            if (match.Groups["report"].Success) return CommandAction.REPORT;
            return CommandAction.MOVE;
        }

        static void SetFormat(Command command, string prompt)
        {
            if (Regex.IsMatch(prompt, @"표로|\btable\b", RegexOptions.IgnoreCase)) command.outputFormat = "table";
            else if (Regex.IsMatch(prompt, @"시간순|chronological", RegexOptions.IgnoreCase)) command.outputFormat = "chronological";
            else if (Regex.IsMatch(prompt, @"인물별|사람별|by person", RegexOptions.IgnoreCase)) command.outputFormat = "by_person";
            else if (Regex.IsMatch(prompt, @"비교|compare|comparison", RegexOptions.IgnoreCase)) command.outputFormat = "comparison";
            else if (Regex.IsMatch(prompt, @"근거|evidence|source", RegexOptions.IgnoreCase)) command.outputFormat = "evidence";
            else if (Regex.IsMatch(prompt, @"목록|\blist\b", RegexOptions.IgnoreCase)) command.outputFormat = "list";
        }

        static void CaptureConditions(Command command, string prompt)
        {
            if (Regex.IsMatch(prompt, @"경비.*(?:지나가면|없으면|지나간\s*후)|after\s+(?:the\s+)?guard|when\s+(?:the\s+)?guard.*(?:leaves|clear)", RegexOptions.IgnoreCase)) command.conditions.Add("GUARD_CLEAR");
            var time = Regex.Match(prompt, @"\d{1,2}\s*시\s*(?:이후|부터)|\d{1,2}:\d{2}");
            if (time.Success) command.conditions.Add(time.Value);
        }

        static ParseResult Fail(string message) { return new ParseResult { error = message }; }
    }
}
