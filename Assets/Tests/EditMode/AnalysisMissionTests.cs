using System;
using System.Linq;
using NUnit.Framework;

namespace ControlRoom.Tests
{
    public class AnalysisMissionTests
    {
        [Test]
        public void ArchiveHasSevenDocumentsAndFiveAuthoredEvidence()
        {
            var service = new AnalysisService();
            Assert.That(service.Sources.Count, Is.EqualTo(7));
            Assert.That(service.AuthoredEvidence.Count, Is.EqualTo(5));
            Assert.That(service.Sources.Select(source => source.Id).Distinct().Count(), Is.EqualTo(7));
        }

        [Test]
        public void USBRequiresProvenanceAndCrossValidation()
        {
            var service = new AnalysisService();
            var initial = service.Query("기밀 USB 위치 알려줘");
            Assert.That(initial.text, Does.Contain("SERVER_ROOM"));
            Assert.That(initial.reliability, Is.EqualTo(Reliability.LIKELY));
            Assert.That(initial.verifiedLocation, Is.False);
            Assert.That(initial.timestamp, Is.EqualTo("2026-09-29 20:00"));
            var source = service.Query("USB가 서버실에 있다는 근거 보여줘");
            Assert.That(source.text, Does.Contain("3일 전"));
            Assert.That(source.reliability, Is.EqualTo(Reliability.UNVERIFIED));
            var verified = service.Query("이 정보와 충돌하는 자료 찾아");
            Assert.That(verified.verifiedLocation, Is.True);
            Assert.That(verified.reliability, Is.EqualTo(Reliability.CONFIRMED));
            Assert.That(verified.text, Does.Contain("LABORATORY").And.Contain("LAB_CABINET").And.Contain("731").And.Contain("김민수"));
            Assert.That(verified.evidence.Any(item => item.sourceId.Contains("INVENTORY_NOTE")), Is.True);
            Assert.That(service.Query("USB 위치 다시 알려줘").text, Does.Contain("LAB_CABINET"));
            service.Reset();
            Assert.That(service.Query("USB 위치").text, Does.Contain("SERVER_ROOM"));
        }

        [Test]
        public void LaboratoryAfter22ActuallyFiltersRows()
        {
            var result = new AnalysisService().Query("22시 이후 연구실에 들어간 사람만 찾아줘");
            Assert.That(result.rows.Count, Is.EqualTo(3));
            Assert.That(result.rows.Select(row => row.person), Is.EqualTo(new[] { "김민수", "박서연", "김민수" }));
            Assert.That(result.rows.Select(row => row.timestamp.Substring(11)), Is.EqualTo(new[] { "22:14", "22:37", "22:52" }));
            Assert.That(result.rows.All(row => row.locationId == "LABORATORY"), Is.True);
            Assert.That(result.text, Does.Contain("ACCESS_LOG").And.Contain("CONFIRMED"));
        }

        [Test]
        public void CombinedTimeCameraRoomAndPersonConditionsAreConjunctive()
        {
            var result = new AnalysisService().Query("22시부터 23시 사이 CAM 2와 CAM 4에서 연구실에 출입한 김민수만 시간순으로 정리해");
            Assert.That(result.rows.Count, Is.EqualTo(2));
            Assert.That(result.rows.All(row => row.cameraId == "CAM-02" && row.person == "김민수"), Is.True);
            Assert.That(result.sourceId, Is.EqualTo("CCTV_LOG_01"));
        }

        [Test]
        public void NarrowWindowReturnsOnlyMatchingPerson()
        {
            var result = new AnalysisService().Query("22:20부터 22:40 사이 연구실 출입자를 찾아");
            Assert.That(result.rows.Count, Is.EqualTo(1));
            Assert.That(result.rows[0].person, Is.EqualTo("박서연"));
            Assert.That(result.rows[0].timestamp, Does.EndWith("22:37"));
        }

        [Test]
        public void CameraFilterAndMovementPathUseArchive()
        {
            var camera = new AnalysisService().Query("CAM 4 기록만 목록으로 보여줘");
            Assert.That(camera.rows.Count, Is.EqualTo(2));
            Assert.That(camera.rows.All(row => row.cameraId == "CAM-04" && row.locationId == "SERVER_ROOM"), Is.True);
            var movement = new AnalysisService().Query("김민수 동선을 시간순으로 정리해");
            Assert.That(movement.rows.Count, Is.EqualTo(4));
            Assert.That(movement.rows.Select(row => row.locationId), Is.EqualTo(new[] { "SERVER_ROOM", "LABORATORY", "STORAGE", "LABORATORY" }));
        }

        [TestCase("화성 생명체를 알려줘")]
        [TestCase("CAM 9 영상 찾아")]
        [TestCase("홍길동의 동선 찾아")]
        [TestCase("회의실 출입 기록 알려줘")]
        [TestCase("25시 이후 출입 로그")]
        [TestCase("22시 이후 2층 CCTV 확인")]
        public void UnknownQueriesReturnUncertainty(string query)
        {
            var result = new AnalysisService().Query(query);
            Assert.That(result.rows, Is.Empty);
            Assert.That(result.reliability, Is.EqualTo(Reliability.UNVERIFIED));
            Assert.That(result.verifiedLocation, Is.False);
            Assert.That(result.text, Does.Contain("확인 불가"));
            Assert.That(result.sourceId, Is.Not.Empty);
            Assert.That(result.timestamp, Is.Not.Empty);
        }

        [Test]
        public void OutputFormatsPreserveDataAndProvenance()
        {
            var service = new AnalysisService();
            var table = service.Query("22시 이후 연구실 출입자를 이름과 시간 기준으로 표로 정리해줘");
            Assert.That(table.text, Does.Contain("시간 | 이름").And.Contain("22:14 | 김민수"));
            var byPerson = service.Query("22시 이후 연구실 출입을 인물별로 정리해");
            Assert.That(byPerson.text, Does.Contain("김민수: 22:14").And.Contain("22:52").And.Contain("박서연: 22:37"));
            var evidence = service.Query("22시 이후 연구실 출입 목록, 근거 포함", "with_evidence");
            Assert.That(evidence.text, Does.Contain("근거 E_ACCESS").And.Contain("ACCESS_LOG"));
            var summary = service.Query("22시 이후 연구실 출입 간단 요약");
            Assert.That(summary.text, Does.Contain("3건, 2명"));
            Assert.That(summary.rows.Count, Is.EqualTo(table.rows.Count));
        }

        [Test]
        public void SpecificDocumentsAndKeywordsAreSearchable()
        {
            var service = new AnalysisService();
            var document = service.Query("SECURITY_REPORT 요약");
            Assert.That(document.text, Does.Contain("감사 증거를 영구 삭제"));
            Assert.That(document.sourceId, Is.EqualTo("SECURITY_REPORT"));
            Assert.That(service.Query("문서에서 'DATA-LEAK-01' 키워드 찾아").sourceId, Is.EqualTo("INVENTORY_NOTE"));
            Assert.That(service.Query("문서 키워드 홍길동 찾아").reliability, Is.EqualTo(Reliability.UNVERIFIED));
            Assert.That(service.Query("유출 협력자 분석").reliability, Is.EqualTo(Reliability.LIKELY));
        }

        [Test]
        public void ExplicitSourceAndUsbKeywordRequestsSearchTheirRequestedArchiveScope()
        {
            var explicitDocument = new AnalysisService().Query("EMAIL_02의 USB 관련 내용만 요약해");
            Assert.That(explicitDocument.sourceId, Is.EqualTo("EMAIL_02"));
            Assert.That(explicitDocument.text, Does.Contain("22:55").And.Contain("LAB_CABINET"));
            Assert.That(explicitDocument.verifiedLocation, Is.False);
            var keyword = new AnalysisService().Query("문서에서 'USB' 키워드를 찾아줘");
            Assert.That(keyword.sourceId.Split(',').Select(value => value.Trim()),
                Is.EquivalentTo(new[] { "EMAIL_01", "EMAIL_02", "RESEARCH_MEMO", "INVENTORY_NOTE" }));
            Assert.That(keyword.text, Does.Contain("자료 검색 결과 4건"));
            Assert.That(keyword.reliability, Is.EqualTo(Reliability.CONFLICTED));
        }

        [Test]
        public void RoomRecordRequestDoesNotTreatRoomNameAsAnUnknownPerson()
        {
            var service = new AnalysisService();
            var result = service.Query("연구실 기록 찾아줘");
            Assert.That(result.rows.Count, Is.EqualTo(3));
            Assert.That(result.rows.All(row => row.locationId == "LABORATORY"), Is.True);
            Assert.That(result.reliability, Is.EqualTo(Reliability.CONFIRMED));
            Assert.That(result.text, Does.Not.Contain("인물 기록을 찾지 못했습니다"));
            Assert.That(service.Query("홍길동의 동선 찾아줘").reliability, Is.EqualTo(Reliability.UNVERIFIED));
        }

        [Test]
        public void PersonScopedEmailComparisonDoesNotTriggerUnrelatedUsbVerification()
        {
            var service = new AnalysisService();
            service.Query("USB 위치 알려줘");
            var result = service.Query("박서연 이메일 비교");
            Assert.That(result.sourceId, Is.EqualTo("EMAIL_02"));
            Assert.That(result.text, Does.Contain("문서 비교").And.Contain("박서연"));
            Assert.That(result.verifiedLocation, Is.False);
            Assert.That(result.reliability, Is.EqualTo(Reliability.LIKELY));
            var noMatch = service.Query("이도현 이메일 비교");
            Assert.That(noMatch.reliability, Is.EqualTo(Reliability.UNVERIFIED));
            Assert.That(noMatch.verifiedLocation, Is.False);
        }

        [Test]
        public void EvidenceDatabaseUpdatesSameIdWithoutDuplicateAndCopiesInputs()
        {
            var service = new AnalysisService();
            var database = new EvidenceDatabase();
            var old = service.Query("USB 위치");
            database.AddRange(old.evidence);
            var verified = service.Query("USB 최신 위치 교차 검증");
            database.AddRange(verified.evidence);
            Assert.That(database.Entries.Count(item => item.id == "E_USB_LOCATION"), Is.EqualTo(1));
            Assert.That(database.Entries.First(item => item.id == "E_USB_LOCATION").locationId, Is.EqualTo("LABORATORY"));
            verified.evidence[0].locationId = "FORGED";
            Assert.That(database.Entries.First(item => item.id == "E_USB_LOCATION").locationId, Is.EqualTo("LABORATORY"));
            database.Clear();
            Assert.That(database.Entries, Is.Empty);
        }

        [Test]
        public void PhotoEvidencePathSurvivesCopyStorageAndReplacement()
        {
            var original = new EvidenceRecord
            {
                id = "FIELD_PHOTO_01", title = "캐비닛 현장 사진", sourceId = "FIELD_AGENT_PHOTO",
                timestamp = "2026-10-02 23:06", locationId = "LABORATORY",
                imagePath = "C:/mission-captures/photo-001.png", reliability = Reliability.CONFIRMED
            };
            var copy = EvidenceDatabase.Copy(original);
            Assert.That(copy.imagePath, Is.EqualTo(original.imagePath));
            Assert.That(copy, Is.Not.SameAs(original));
            var database = new EvidenceDatabase();
            database.AddRange(new[] { original });
            original.imagePath = "C:/mission-captures/photo-002.png";
            Assert.That(database.Entries[0].imagePath, Is.EqualTo("C:/mission-captures/photo-001.png"));
            database.AddRange(new[] { original });
            Assert.That(database.Entries.Count, Is.EqualTo(1));
            Assert.That(database.Entries[0].imagePath, Is.EqualTo("C:/mission-captures/photo-002.png"));
        }

        [Test]
        public void AnalysisEvidenceCloneRetainsImagePathAndIsIndependentFromArchive()
        {
            var service = new AnalysisService();
            var authored = service.AuthoredEvidence.First(item => item.id == "E_CABINET_CODE");
            authored.imagePath = "C:/mission-captures/panel-reference.png";
            var result = service.Query("연구실 캐비닛 암호 알려줘");
            Assert.That(result.evidence[0], Is.Not.SameAs(authored));
            Assert.That(result.evidence[0].imagePath, Is.EqualTo(authored.imagePath));
            result.evidence[0].imagePath = "CHANGED";
            Assert.That(authored.imagePath, Is.EqualTo("C:/mission-captures/panel-reference.png"));
        }

        [Test]
        public void TimeWindowIncludesBoundariesAndCameraMustMatchTheRoom()
        {
            var service = new AnalysisService();
            var range = service.Query("22:14부터 22:37 사이 CAM 2 연구실 출입자를 시간순으로 정리해");
            Assert.That(range.rows.Select(row => row.timestamp.Substring(11)), Is.EqualTo(new[] { "22:14", "22:37" }));
            var mismatchedCamera = service.Query("22시부터 23시 사이 CAM 4에서 연구실 출입 기록 찾아");
            Assert.That(mismatchedCamera.rows, Is.Empty);
            Assert.That(mismatchedCamera.reliability, Is.EqualTo(Reliability.UNVERIFIED));
            var wrongDate = service.Query("2026-10-01 22시 이후 CAM 2 연구실 출입 기록 찾아");
            Assert.That(wrongDate.rows, Is.Empty);
            Assert.That(wrongDate.reliability, Is.EqualTo(Reliability.UNVERIFIED));
        }

        [Test]
        public void TimerOnlyTicksDuringPlayingAndWhileUnpaused()
        {
            var rules = new MissionRules();
            rules.Tick(20f);
            Assert.That(rules.RemainingSeconds, Is.EqualTo(600f));
            rules.Start();
            rules.Tick(30f);
            rules.SetPaused(true);
            rules.Tick(30f);
            Assert.That(rules.RemainingSeconds, Is.EqualTo(570f));
            rules.SetPaused(false);
            rules.Tick(float.NaN);
            rules.Tick(-50f);
            rules.Tick(10f);
            Assert.That(rules.ElapsedSeconds, Is.EqualTo(40f));
        }

        [Test]
        public void SuccessRequiresUsbAndPhysicalExitCallback()
        {
            var rules = new MissionRules();
            rules.Start();
            rules.ReachExit();
            Assert.That(rules.State, Is.EqualTo(MissionState.Playing));
            rules.AcquireUsb();
            Assert.That(rules.State, Is.EqualTo(MissionState.Playing));
            rules.ReachExit();
            Assert.That(rules.State, Is.EqualTo(MissionState.Success));
            rules.Caught();
            rules.Tick(600f);
            Assert.That(rules.State, Is.EqualTo(MissionState.Success));
            rules.ShowResult();
            Assert.That(rules.State, Is.EqualTo(MissionState.Result));
            Assert.That(rules.Outcome, Is.EqualTo(MissionState.Success));
        }

        [TestCase("TIMEOUT")]
        [TestCase("CAUGHT")]
        [TestCase("EVIDENCE_DESTROYED")]
        [TestCase("OBJECTIVE_LOST")]
        public void AllSpecFailureConditionsAreTerminal(string reason)
        {
            var rules = new MissionRules();
            rules.Start();
            switch (reason)
            {
                case "TIMEOUT": rules.Tick(650f); break;
                case "CAUGHT": rules.Caught(); break;
                case "EVIDENCE_DESTROYED": rules.DestroyCriticalEvidence(); break;
                case "OBJECTIVE_LOST": rules.LoseObjective(); break;
            }
            Assert.That(rules.State, Is.EqualTo(MissionState.Failure));
            Assert.That(rules.FailureReason, Does.Contain(reason));
            rules.AcquireUsb();
            rules.ReachExit();
            Assert.That(rules.State, Is.EqualTo(MissionState.Failure));
            Assert.That(rules.HasUsb, Is.False);
        }

        [Test]
        public void RiskAndRestartResetEveryMissionVariable()
        {
            var rules = new MissionRules();
            rules.Start();
            rules.AddRisk(30f);
            Assert.That(rules.Risk, Is.EqualTo(RiskLevel.MEDIUM));
            rules.AddRisk(35f);
            Assert.That(rules.Risk, Is.EqualTo(RiskLevel.HIGH));
            rules.AddRisk(100f);
            Assert.That(rules.Risk, Is.EqualTo(RiskLevel.COMPROMISED));
            Assert.That(rules.RiskValue, Is.EqualTo(100f));
            rules.Tick(20f);
            rules.AcquireUsb();
            rules.SetPaused(true);
            rules.Caught();
            rules.ShowResult();
            rules.Reset();
            Assert.That(rules.State, Is.EqualTo(MissionState.Briefing));
            Assert.That(rules.RemainingSeconds, Is.EqualTo(600f));
            Assert.That(rules.ElapsedSeconds, Is.Zero);
            Assert.That(rules.Risk, Is.EqualTo(RiskLevel.LOW));
            Assert.That(rules.HasUsb, Is.False);
            Assert.That(rules.Paused, Is.False);
            Assert.That(rules.FailureReason, Is.Empty);
        }

        [Test]
        public void JournalRecordsCommandLifecycleAndHidesProfileDuringGameplay()
        {
            var journal = new CommandJournal();
            var command = new Command { action = CommandAction.OPEN, objectId = "LAB_DOOR", locationId = "MAIN_HALL", target = CommandTarget.FIELD_AGENT };
            journal.Record("연구실 문 열어", command, CommandStatus.RECEIVED);
            journal.Record("연구실 문 열어", command, CommandStatus.NEEDS_CLARIFICATION, "대상 확인", 1);
            journal.Record("연구실 문 열어", command, CommandStatus.COMPLETED, "열었습니다", 1);
            Assert.That(journal.Entries.Count, Is.EqualTo(1));
            Assert.That(journal.Entries[0].ClarificationCount, Is.EqualTo(1));
            Assert.That(journal.Entries[0].StatusHistory.Count, Is.EqualTo(3));
            Assert.That(journal.Entries[0].ParsedCommand.objectId, Is.EqualTo("LAB_DOOR"));
            command.objectId = "CHANGED";
            Assert.That(journal.Entries[0].ParsedCommand.objectId, Is.EqualTo("LAB_DOOR"));
            Assert.That(journal.GetProfile(MissionState.Playing), Is.Null);
            Assert.That(journal.GetResultSummary(MissionState.Playing), Is.Empty);
            var profile = journal.BuildProfile(MissionState.Result);
            Assert.That(profile.Commands, Is.EqualTo(1));
            Assert.That(profile.Iteration, Is.EqualTo(1));
            Assert.That(journal.GetResultSummary(MissionState.Result), Does.Contain("관찰된 행동 횟수"));
        }

        [Test]
        public void JournalCountsActualVerificationRestrictionsSequencesAndRetries()
        {
            var journal = new CommandJournal();
            var failure = new Command { target = CommandTarget.FIELD_AGENT, action = CommandAction.PICKUP, objectId = "USB", locationId = "SERVER_ROOM" };
            journal.Record("USB 가져와", failure, CommandStatus.FAILED, "찾지 못했습니다");
            journal.Record("USB 가져와", failure, CommandStatus.COMPLETED, "재시도");
            Assert.That(journal.Entries[1].RetryCount, Is.EqualTo(1));
            var analysis = new Command { target = CommandTarget.ANALYSIS_SYSTEM, action = CommandAction.ANALYZE, outputFormat = "with_evidence" };
            journal.Record("USB 정보 교차 검증 근거 포함", analysis, CommandStatus.COMPLETED, "확인", verification: true);
            var sequence = new Command { target = CommandTarget.FIELD_AGENT, action = CommandAction.MOVE, locationId = "LABORATORY" };
            sequence.sequence.Add(new CommandStep(CommandAction.MOVE, "LABORATORY"));
            sequence.sequence.Add(new CommandStep(CommandAction.INSPECT, "LABORATORY", "LAB_CABINET"));
            sequence.restrictions.Add("DO_NOT_PICKUP");
            journal.Record("연구실 이동 후 조사, 가져오지 마", sequence, CommandStatus.COMPLETED);
            var profile = journal.GetProfile(MissionState.Result);
            Assert.That(profile.Commands, Is.EqualTo(4));
            Assert.That(profile.Verification, Is.EqualTo(1));
            Assert.That(profile.CrossValidation, Is.EqualTo(1));
            Assert.That(profile.TaskDecomposition, Is.EqualTo(1));
            Assert.That(profile.Constraint, Is.EqualTo(1));
            Assert.That(profile.Delegation, Is.EqualTo(1));
            Assert.That(profile.Iteration, Is.EqualTo(1));
            journal.Clear();
            Assert.That(journal.Entries, Is.Empty);
        }

        [Test]
        public void JournalUpdatesPendingFieldCommandAcrossInterveningAnalysisAndMatchesTarget()
        {
            var journal = new CommandJournal();
            var field = new Command { target = CommandTarget.FIELD_AGENT, action = CommandAction.OPEN, objectId = "DOOR" };
            var analysis = new Command { target = CommandTarget.ANALYSIS_SYSTEM, action = CommandAction.ANALYZE };
            journal.Record("문 열어", field, CommandStatus.NEEDS_CLARIFICATION, "어느 문입니까?");
            journal.Record("출입 기록 찾아줘", analysis, CommandStatus.COMPLETED, "분석 완료");
            field.objectId = "LAB_DOOR";
            journal.Record("문 열어", field, CommandStatus.COMPLETED, "연구실 문 개방", 1);
            Assert.That(journal.Entries.Count, Is.EqualTo(2));
            Assert.That(journal.Entries[0].Status, Is.EqualTo(CommandStatus.COMPLETED));
            Assert.That(journal.Entries[0].ParsedCommand.objectId, Is.EqualTo("LAB_DOOR"));
            Assert.That(journal.Entries[0].ClarificationCount, Is.EqualTo(1));
            Assert.That(journal.Entries[0].StatusHistory, Is.EqualTo(new[] { CommandStatus.NEEDS_CLARIFICATION, CommandStatus.COMPLETED }));
            Assert.That(journal.BuildProfile(MissionState.Result).Commands, Is.EqualTo(2));

            journal.Clear();
            journal.Record("같은 요청", field, CommandStatus.NEEDS_CLARIFICATION);
            journal.Record("같은 요청", analysis, CommandStatus.RECEIVED);
            journal.Record("같은 요청", field, CommandStatus.COMPLETED, "현장 완료", 1);
            Assert.That(journal.Entries.Count, Is.EqualTo(2));
            Assert.That(journal.Entries[0].Status, Is.EqualTo(CommandStatus.COMPLETED));
            Assert.That(journal.Entries[1].Status, Is.EqualTo(CommandStatus.RECEIVED));
            Assert.That(journal.Entries[1].ParsedCommand.target, Is.EqualTo(CommandTarget.ANALYSIS_SYSTEM));
            journal.Record("같은 요청", analysis, CommandStatus.COMPLETED, "분석 완료");
            Assert.That(journal.Entries.Count, Is.EqualTo(2));
            Assert.That(journal.Entries.All(entry => entry.Status == CommandStatus.COMPLETED), Is.True);
        }

        [Test]
        public void JournalSeparatesSamePromptTargetsWhileParsingIsStillNull()
        {
            var journal = new CommandJournal();
            journal.Record("동일한 요청", null, CommandStatus.RECEIVED, requestTarget: CommandTarget.FIELD_AGENT);
            journal.Record("동일한 요청", null, CommandStatus.RECEIVED, requestTarget: CommandTarget.ANALYSIS_SYSTEM);
            journal.Record("동일한 요청", null, CommandStatus.PROCESSING, "현장 명령 해석 중", requestTarget: CommandTarget.FIELD_AGENT);
            journal.Record("동일한 요청", null, CommandStatus.PROCESSING, "분석 명령 해석 중", requestTarget: CommandTarget.ANALYSIS_SYSTEM);
            Assert.That(journal.Entries.Count, Is.EqualTo(2));
            Assert.That(journal.Entries.All(entry => entry.ParsedCommand == null), Is.True);
            Assert.That(journal.Entries[0].RequestTarget, Is.EqualTo(CommandTarget.FIELD_AGENT));
            Assert.That(journal.Entries[1].RequestTarget, Is.EqualTo(CommandTarget.ANALYSIS_SYSTEM));
            Assert.That(journal.Entries[0].Message, Is.EqualTo("현장 명령 해석 중"));
            Assert.That(journal.Entries[1].Message, Is.EqualTo("분석 명령 해석 중"));
            journal.Record("동일한 요청", null, CommandStatus.FAILED, "현장 해석 실패", requestTarget: CommandTarget.FIELD_AGENT);
            journal.Record("동일한 요청", null, CommandStatus.FAILED, "분석 해석 실패", requestTarget: CommandTarget.ANALYSIS_SYSTEM);
            Assert.That(journal.Entries.Count, Is.EqualTo(2));
            Assert.That(journal.Entries.All(entry => entry.Status == CommandStatus.FAILED), Is.True);
            Assert.That(journal.Entries.All(entry => entry.ParsedCommand == null), Is.True);
            Assert.That(journal.Entries[0].Message, Is.EqualTo("현장 해석 실패"));
            Assert.That(journal.Entries[1].Message, Is.EqualTo("분석 해석 실패"));
            Assert.That(journal.Entries.All(entry => entry.RetryCount == 0), Is.True);

            var parsed = new Command { target = CommandTarget.FIELD_AGENT, action = CommandAction.REPORT };
            journal.Record("해석된 요청", parsed, CommandStatus.COMPLETED, requestTarget: CommandTarget.ANALYSIS_SYSTEM);
            Assert.That(journal.Entries[2].RequestTarget, Is.EqualTo(CommandTarget.FIELD_AGENT), "A real Command overrides an inconsistent metadata hint.");
        }
    }
}
