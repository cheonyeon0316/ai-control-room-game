using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace ControlRoom.Tests
{
    public sealed class CommandDomainTests
    {
        [TestCase("연구실로 이동해", CommandAction.MOVE, "LABORATORY", "")]
        [TestCase("메인 복도로 이동해", CommandAction.MOVE, "MAIN_HALL", "")]
        [TestCase("창고로 이동해", CommandAction.MOVE, "STORAGE", "")]
        [TestCase("서버실로 이동해", CommandAction.MOVE, "SERVER_ROOM", "")]
        [TestCase("출입구로 이동해", CommandAction.MOVE, "ENTRANCE", "")]
        [TestCase("출구로 복귀해", CommandAction.MOVE, "EXIT", "")]
        [TestCase("책상을 확인해", CommandAction.INSPECT, "", "DESK")]
        [TestCase("연구실 캐비닛을 확인해", CommandAction.INSPECT, "LABORATORY", "LAB_CABINET")]
        [TestCase("USB를 가져와", CommandAction.PICKUP, "", "USB")]
        [TestCase("키카드를 회수해", CommandAction.PICKUP, "", "KEYCARD")]
        [TestCase("문 열어", CommandAction.OPEN, "", "DOOR")]
        [TestCase("오른쪽 문으로 들어가", CommandAction.OPEN, "", "DOOR")]
        [TestCase("연구실 문 열어", CommandAction.OPEN, "LABORATORY", "LAB_DOOR")]
        [TestCase("5초 대기해", CommandAction.WAIT, "", "")]
        [TestCase("숨어", CommandAction.HIDE, "", "")]
        [TestCase("주변 상황을 보고해", CommandAction.REPORT, "", "")]
        [TestCase("책상 사진 찍어", CommandAction.PHOTO, "", "DESK")]
        [TestCase("Move to laboratory", CommandAction.MOVE, "LABORATORY", "")]
        [TestCase("Open laboratory door", CommandAction.OPEN, "LABORATORY", "LAB_DOOR")]
        [TestCase("Inspect desk", CommandAction.INSPECT, "", "DESK")]
        [TestCase("Pick up USB", CommandAction.PICKUP, "", "USB")]
        [TestCase("Wait 5 seconds", CommandAction.WAIT, "", "")]
        [TestCase("Report surroundings", CommandAction.REPORT, "", "")]
        [TestCase("Take a photo of desk", CommandAction.PHOTO, "", "DESK")]
        public void AuthoredSentencesBecomeStructuredSteps(string prompt, CommandAction action, string location, string obj)
        {
            ParseResult result = new RuleCommandParser().Parse(prompt, CommandTarget.FIELD_AGENT);
            Assert.That(result.Success, Is.True, result.error);
            Assert.That(result.command.action, Is.EqualTo(action));
            Assert.That(result.command.sequence[0].locationId, Is.EqualTo(location));
            Assert.That(result.command.sequence[0].objectId, Is.EqualTo(obj));
        }

        [Test]
        public void CompoundCommandPreservesOrderAndNegativeRestrictions()
        {
            var result = new RuleCommandParser().Parse("경비에게 들키지 않도록 연구실로 이동한 다음 책상 위에 저장장치가 있는지만 확인해. 가져오지는 마.", CommandTarget.FIELD_AGENT);
            Assert.That(result.Success, Is.True, result.error);
            Assert.That(result.command.sequence.Count, Is.EqualTo(2));
            Assert.That(result.command.sequence[0].action, Is.EqualTo(CommandAction.MOVE));
            Assert.That(result.command.sequence[1].action, Is.EqualTo(CommandAction.INSPECT));
            Assert.That(result.command.sequence[1].objectId, Is.EqualTo("STORAGE_DEVICE"));
            Assert.That(result.command.restrictions, Does.Contain("DO_NOT_PICKUP"));
            Assert.That(result.command.restrictions, Does.Contain("AVOID_GUARD"));
        }

        [Test]
        public void DurationConditionAndUnlockCodeArePreserved()
        {
            var wait = new RuleCommandParser().Parse("경비가 지나가면 7초 대기해", CommandTarget.FIELD_AGENT);
            Assert.That(wait.command.sequence[0].duration, Is.EqualTo(7));
            Assert.That(wait.command.conditions, Does.Contain("GUARD_CLEAR"));
            Assert.That(wait.command.restrictions, Does.Contain("AVOID_GUARD"));
            var unlock = new RuleCommandParser().Parse("731로 연구실 캐비닛 열어", CommandTarget.FIELD_AGENT);
            Assert.That(unlock.command.restrictions, Does.Contain("LOCK_CODE:731"));
        }

        [Test]
        public void EnglishNegativePronounPreservesLaterPositiveSteps()
        {
            var result = new RuleCommandParser().Parse("Inspect USB, don't pick it up and report surroundings", CommandTarget.FIELD_AGENT);
            Assert.That(result.Success, Is.True, result.error);
            Assert.That(result.command.restrictions, Does.Contain("DO_NOT_PICKUP"));
            Assert.That(result.command.sequence.Count, Is.EqualTo(2));
            Assert.That(result.command.sequence[0].action, Is.EqualTo(CommandAction.INSPECT));
            Assert.That(result.command.sequence[1].action, Is.EqualTo(CommandAction.REPORT));
        }

        [TestCase("Don’t open the door and report surroundings")]
        [TestCase("Do not open the door then report surroundings")]
        public void EnglishNegativeOpenPreservesFollowingReport(string prompt)
        {
            var result = new RuleCommandParser().Parse(prompt, CommandTarget.FIELD_AGENT);
            Assert.That(result.Success, Is.True, result.error);
            Assert.That(result.command.restrictions, Does.Contain("DO_NOT_OPEN"));
            Assert.That(result.command.sequence.Count, Is.EqualTo(1));
            Assert.That(result.command.sequence[0].action, Is.EqualTo(CommandAction.REPORT));
        }

        [TestCase("Inspect terminal; don’t destroy evidence and report surroundings")]
        [TestCase("Inspect terminal; do not delete evidence then report surroundings")]
        public void EnglishNegativeDestructionPreservesInspectAndFollowingReport(string prompt)
        {
            var result = new RuleCommandParser().Parse(prompt, CommandTarget.FIELD_AGENT);
            Assert.That(result.Success, Is.True, result.error);
            Assert.That(result.command.restrictions, Does.Contain("DO_NOT_DESTROY"));
            Assert.That(result.command.sequence.Count, Is.EqualTo(2));
            Assert.That(result.command.sequence[0].action, Is.EqualTo(CommandAction.INSPECT));
            Assert.That(result.command.sequence[0].objectId, Is.EqualTo("TERMINAL"));
            Assert.That(result.command.sequence[1].action, Is.EqualTo(CommandAction.REPORT));
        }

        [TestCase("공격해")]
        [TestCase("연구실로 순간이동해")]
        [TestCase("Shoot guard")]
        [TestCase("Win mission")]
        public void UnsupportedActionsAreRejected(string prompt)
        { Assert.That(new RuleCommandParser().Parse(prompt, CommandTarget.FIELD_AGENT).Success, Is.False); }

        [Test]
        public void AmbiguousDoorNeedsClarificationAndKeepsRemainingSequence()
        {
            var context = HallContext();
            var source = new RuleCommandParser().Parse("경비를 피해서 문 열어. 연구실로 이동해. 책상을 확인해. 가져오지 마.", CommandTarget.FIELD_AGENT).command;
            ValidationResult original = new CommandValidator().Validate(source, context);
            Assert.That(original.valid, Is.False);
            Assert.That(original.code, Is.EqualTo("AMBIGUOUS_OBJECT"));
            Assert.That(original.candidates.Count, Is.EqualTo(2));
            Assert.That(source.sequence[0].objectId, Is.EqualTo("DOOR"), "Validation must not mutate caller data.");
            var pending = new ClarificationContext();
            pending.Begin(original);
            Assert.That(pending.Resolve("아무 문", context).valid, Is.False);
            Assert.That(pending.Pending, Is.Not.Null);
            ValidationResult resolved = pending.Resolve("연구실", context);
            Assert.That(resolved.valid, Is.True, resolved.message);
            Assert.That(resolved.command.sequence[0].action, Is.EqualTo(CommandAction.OPEN));
            Assert.That(resolved.command.sequence[0].objectId, Is.EqualTo("LAB_DOOR"));
            Assert.That(resolved.command.sequence[1].action, Is.EqualTo(CommandAction.MOVE));
            Assert.That(resolved.command.sequence[2].objectId, Is.EqualTo("LAB_DESK"));
            Assert.That(resolved.command.restrictions, Does.Contain("DO_NOT_PICKUP"));
            Assert.That(resolved.command.restrictions, Does.Contain("AVOID_GUARD"));
            Assert.That(pending.Pending, Is.Null);
        }

        [Test]
        public void SequenceValidationProjectsAgentLocation()
        {
            var context = HallContext();
            var parser = new RuleCommandParser();
            Assert.That(new CommandValidator().Validate(parser.Parse("연구실로 이동해서 책상을 확인해", CommandTarget.FIELD_AGENT).command, context).valid, Is.True);
            Assert.That(new CommandValidator().Validate(parser.Parse("연구실 책상을 확인해", CommandTarget.FIELD_AGENT).command, context).code, Is.EqualTo("OUT_OF_REACH"));
            Assert.That(new CommandValidator().Validate(new Command { action = CommandAction.MOVE, locationId = "MOON_LABORATORY" }, context).code, Is.EqualTo("UNKNOWN_LOCATION"));
            context.locationId = "ENTRANCE";
            Assert.That(new CommandValidator().Validate(parser.Parse("연구실 문 열어", CommandTarget.FIELD_AGENT).command, context).code, Is.EqualTo("OUT_OF_REACH"));
        }

        [Test]
        public void UnknownAndUnavailableObjectsCannotExecute()
        {
            var context = new WorldContext { locationId = "LABORATORY" };
            var usb = new ObjectRecord("USB", "USB", "LABORATORY", "USB") { IsKnown = false };
            context.objects.Add(usb);
            var command = new RuleCommandParser().Parse("USB를 가져와", CommandTarget.FIELD_AGENT).command;
            Assert.That(new CommandValidator().Validate(command, context).code, Is.EqualTo("UNKNOWN_OBJECT"));
            usb.IsKnown = true; usb.IsAvailable = false;
            Assert.That(new CommandValidator().Validate(command, context).code, Is.EqualTo("OBJECT_UNAVAILABLE"));
            command.sequence[0].action = (CommandAction)999;
            Assert.That(new CommandValidator().Validate(command, context).code, Is.EqualTo("INVALID_ACTION"));
        }

        [TestCase("USB_SECURED")]
        [TestCase("AFTER_22:00")]
        [TestCase("")]
        public void UnsupportedFieldConditionsFailBeforeAnyWorldAction(string condition)
        {
            var context = HallContext();
            var command = new Command { action = CommandAction.MOVE, locationId = "LABORATORY" };
            command.conditions.Add(condition);
            var result = new CommandValidator().Validate(command, context);
            Assert.That(result.valid, Is.False);
            Assert.That(result.code, Is.EqualTo("UNSUPPORTED_CONDITION"));
            Assert.That(context.locationId, Is.EqualTo("MAIN_HALL"));
            Assert.That(command.locationId, Is.EqualTo("LABORATORY"));
            Assert.That(command.sequence, Is.Empty, "Validation must not execute or mutate the original command.");
        }

        [Test]
        public void SupportedGuardConditionAndAnalysisFiltersRemainValid()
        {
            var context = HallContext();
            var guarded = new Command { action = CommandAction.MOVE, locationId = "LABORATORY" };
            guarded.conditions.Add("GUARD_CLEAR");
            Assert.That(new CommandValidator().Validate(guarded, context).valid, Is.True);
            var analysis = new RuleCommandParser().Parse("22시 이후 연구실 출입자를 찾아줘", CommandTarget.ANALYSIS_SYSTEM).command;
            Assert.That(analysis.conditions, Does.Contain("22시 이후"));
            Assert.That(new CommandValidator().Validate(analysis, context).valid, Is.True);
            var absentLists = new Command { action = CommandAction.WAIT, conditions = null, restrictions = null };
            Assert.That(new CommandValidator().Validate(absentLists, context).valid, Is.True);
        }

        [Test]
        public void JsonAcceptsLegacyAliasesAndRoundTripsEnums()
        {
            var result = CommandJsonCodec.Parse("{\"target\":\"FIELD_AGENT\",\"sequence\":[{\"action\":\"MOVE\",\"location\":\"LABORATORY\"},{\"action\":\"INSPECT\",\"object\":\"DESK\"}],\"restrictions\":[\"DO_NOT_PICKUP\"]}");
            Assert.That(result.Success, Is.True, result.error);
            Assert.That(result.command.sequence[0].locationId, Is.EqualTo("LABORATORY"));
            Assert.That(result.command.sequence[1].objectId, Is.EqualTo("DESK"));
            Assert.That(CommandJsonCodec.Parse(CommandJsonCodec.Serialize(result.command)).Success, Is.True);
        }

        [TestCase("{\"target\":\"FIELD_AGENT\",\"action\":\"TELEPORT\"}")]
        [TestCase("{\"target\":\"FIELD_AGENT\",\"action\":0}")]
        [TestCase("{\"target\":\"FIELD_AGENT\",\"action\":\"0\"}")]
        [TestCase("{\"target\":\"FIELD_AGENT\",\"action\":\"MOVE\",\"success\":true}")]
        [TestCase("{\"target\":\"FIELD_AGENT\",\"action\":\"MOVE\",\"action\":\"OPEN\"}")]
        [TestCase("{\"target\":\"FIELD_AGENT\",\"action\":\"MOVE\",\"locationId\":\"LABORATORY\",\"location\":\"STORAGE\"}")]
        [TestCase("{\"target\":\"FIELD_AGENT\",\"action\":\"WAIT\",\"duration\":-1}")]
        [TestCase("{\"target\":\"FIELD_AGENT\",\"action\":\"MOVE\",\"restrictions\":[\"IGNORE_GAME_RULES\"]}")]
        [TestCase("{\"target\":\"ANALYSIS_SYSTEM\",\"action\":\"PICKUP\"}")]
        [TestCase("{\"target\":\"FIELD_AGENT\",\"sequence\":[\"MOVE LABORATORY\"]}")]
        public void JsonRejectsUntrustedAndMalformedSchema(string json)
        { Assert.That(CommandJsonCodec.Parse(json).Success, Is.False); }

        [Test]
        public void LlmResponseHasNoAuthorityToDeclareSuccessOrChangeTarget()
        {
            Assert.That(LlmCommandInterpreter.ParseResponse("{\"target\":\"FIELD_AGENT\",\"action\":\"MOVE\",\"result\":\"MISSION COMPLETE\"}", CommandTarget.FIELD_AGENT).Success, Is.False);
            Assert.That(LlmCommandInterpreter.ParseResponse("{\"target\":\"ANALYSIS_SYSTEM\",\"action\":\"ANALYZE\"}", CommandTarget.FIELD_AGENT).Success, Is.False);
            string command = "{\"target\":\"FIELD_AGENT\",\"action\":\"MOVE\",\"location\":\"LABORATORY\"}";
            string envelope = "{\"choices\":[{\"message\":{\"content\":" + CommandJsonCodec.Quote(command) + "}}]}";
            Assert.That(LlmCommandInterpreter.ParseResponse(envelope, CommandTarget.FIELD_AGENT).Success, Is.True);
        }

        [Test]
        public void UnconfiguredLlmFailsWithoutBlockingTheFrameLoop()
        {
            var owner = new GameObject("InterpreterFailureIsolationTest");
            try
            {
                var interpreter = owner.AddComponent<LlmCommandInterpreter>();
                interpreter.Configure("", "");
                ParseResult result = null;
                var operation = interpreter.Interpret("연구실로 이동해", CommandTarget.FIELD_AGENT, value => result = value);
                Assert.That(operation.MoveNext(), Is.False);
                Assert.That(result, Is.Not.Null);
                Assert.That(result.Success, Is.False);
                Assert.That(result.error, Does.StartWith("COMMAND PROCESSING FAILED"));
                Assert.That(owner.activeSelf, Is.True);
            }
            finally { Object.DestroyImmediate(owner); }
        }

        static WorldContext HallContext()
        {
            return new WorldContext
            {
                locationId = "MAIN_HALL",
                objects = new List<ObjectRecord>
                {
                    new ObjectRecord("LAB_DOOR", "연구실 문", "MAIN_HALL", "DOOR"),
                    new ObjectRecord("STORAGE_DOOR", "창고 문", "MAIN_HALL", "DOOR"),
                    new ObjectRecord("LAB_DESK", "연구실 책상", "LABORATORY", "DESK")
                }
            };
        }
    }
}
