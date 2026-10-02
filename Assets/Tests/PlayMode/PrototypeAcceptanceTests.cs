using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace ControlRoom.Tests
{
    public sealed class PrototypeAcceptanceTests
    {
        private GameManager game;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            Time.timeScale = 1;
            if (GameManager.Instance != null) UnityEngine.Object.Destroy(GameManager.Instance.gameObject);
            yield return null;
            game = new GameObject("Acceptance Mission").AddComponent<GameManager>();
            yield return null;
            Assert.IsTrue(game.World.NavigationReady, "Actual Unity NavMesh must be built.");
            Assert.IsTrue(game.World.Agent.Navigation.isOnNavMesh);
            Assert.IsTrue(game.World.Guard.Navigation.isOnNavMesh);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Time.timeScale = 1;
            if (GameManager.Instance != null) UnityEngine.Object.Destroy(GameManager.Instance.gameObject);
            yield return null;
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator AC01_FourRealDistinctCameraFeeds_SelectionAndLiveMovement()
        {
            game.Hud.StartButton.onClick.Invoke();
            yield return null; yield return null;
            Assert.AreEqual(4, game.World.Cctv.Cameras.Count);
            Assert.AreEqual(4, game.World.Cctv.Textures.Distinct().Count());
            Assert.AreEqual(4, game.Hud.CameraImages.Count);
            var hashes = new System.Collections.Generic.HashSet<int>();
            Directory.CreateDirectory(Path.Combine(Application.dataPath, "../TestResults"));
            for (int i = 0; i < 4; i++)
            {
                var camera = game.World.Cctv.Cameras[i];
                Assert.IsTrue(camera.enabled);
                Assert.AreSame(game.World.Cctv.Textures[i], camera.targetTexture);
                Assert.AreSame(camera.targetTexture, game.Hud.CameraImages[i].texture);
                Assert.AreEqual("ControlRoom/CCTVFeed", game.Hud.CameraImages[i].material.shader.name);
                Assert.IsTrue(game.Hud.CameraImages[i].material.shader.isSupported, "CCTV shader must compile on the target renderer.");
                Assert.IsTrue(camera.targetTexture.IsCreated());
                var texture = Read(camera.targetTexture);
                Assert.Greater(texture.GetPixels32().Distinct().Count(), 15, "CCTV must render scene geometry, not a blank texture.");
                hashes.Add(Hash(texture));
                File.WriteAllBytes(Path.Combine(Application.dataPath, "../TestResults/cam-0" + (i + 1) + ".png"), texture.EncodeToPNG());
                UnityEngine.Object.Destroy(texture);
                game.Hud.CameraImages[i].GetComponent<Button>().onClick.Invoke();
                Assert.AreEqual(i, game.World.Cctv.SelectedIndex);
                Assert.IsTrue(game.Hud.ZoomVisible);
                Assert.AreSame(camera.targetTexture, game.Hud.ZoomImage.texture);
            }
            Assert.AreEqual(4, hashes.Count, "All feeds should show different physical rooms.");
            game.Hud.CloseZoom();
            var before = Read(game.World.Cctv.Textures[0]);
            yield return Send("메인 복도로 이동해");
            yield return null;
            var after = Read(game.World.Cctv.Textures[0]);
            Assert.AreNotEqual(Hash(before), Hash(after), "Agent movement must affect actual rendered pixels.");
            UnityEngine.Object.Destroy(before); UnityEngine.Object.Destroy(after);
            yield return CaptureHud("control-room.png");
        }

        [UnityTest]
        public IEnumerator AC02_AC03_AC04_UguiNaturalLanguage_ParsedAndExecutedInSpace()
        {
            game.Hud.StartButton.onClick.Invoke();
            yield return Send("메인 복도로 이동해");
            yield return Send("연구실 문 열어");
            var initial = game.World.Agent.transform.position;
            float distanceToGoal = Vector3.Distance(initial, game.World.Locations["LABORATORY"]);
            Assert.Greater(distanceToGoal, 1f, "The movement fixture must start away from its destination.");
            yield return Send("연구실로 이동해.");
            Assert.AreEqual(CommandAction.MOVE, game.Commands.LastCommand.action);
            Assert.AreEqual("LABORATORY", game.Commands.LastCommand.locationId);
            Assert.AreEqual("LABORATORY", game.World.Agent.CurrentLocationId);
            Assert.Greater(Vector3.Distance(initial, game.World.Agent.transform.position), distanceToGoal - .5f);
            Assert.Less(Vector3.Distance(game.World.Locations["LABORATORY"], game.World.Agent.transform.position), .5f);
            Assert.IsFalse(game.World.Agent.IsBusy);
            Assert.AreEqual(CommandStatus.COMPLETED, game.Commands.LastStatus);
        }

        [UnityTest]
        public IEnumerator AC04_EightActions_RealObjectChanges_RestrictionsAndUnknownRejection()
        {
            game.Hud.StartButton.onClick.Invoke();
            yield return Send("메인 복도로 이동해");
            yield return Send("연구실 문 열어");
            yield return Send("연구실로 이동해");
            yield return Send("1초 동안 대기해");
            Assert.AreEqual(CommandAction.WAIT, game.Commands.LastCommand.action);
            yield return Send("책상 확인해");
            Assert.IsTrue(game.Commands.LastResponse.Contains("키카드"));
            Assert.IsTrue(game.Evidence.Entries.Single(e => e.id == "FIELD_INSPECT_LAB_DESK").content.Contains("키카드"));
            yield return Send("키카드 가져와");
            Assert.IsTrue(game.World.Objects["KEYCARD"].IsCollected);
            yield return Send("책상 확인해");
            Assert.IsFalse(game.Evidence.Entries.Single(e => e.id == "FIELD_INSPECT_LAB_DESK").content.Contains("키카드"), "Inspection must not report an already collected keycard on the desk.");
            yield return Send("캐비닛 사진 찍어");
            Assert.IsTrue(game.Evidence.Entries.Any(e => e.sourceId == "FIELD_AGENT_CAMERA" && e.id.StartsWith("FIELD_PHOTO")));
            var photograph = game.Evidence.Entries.Last(e => e.sourceId == "FIELD_AGENT_CAMERA");
            Assert.IsTrue(File.Exists(photograph.imagePath), "PHOTO must save an actual rendered image.");
            File.Copy(photograph.imagePath, Path.Combine(Application.dataPath, "../TestResults/field-photo.png"), true);
            Assert.IsTrue(photograph.timestamp.StartsWith("2026-10-02"));
            game.Hud.PhotoButton.onClick.Invoke();
            Assert.IsTrue(game.Hud.PhotoVisible);
            game.Hud.PhotoCloseButton.onClick.Invoke();
            Assert.IsFalse(game.Hud.PhotoVisible);
            yield return Send("1초 동안 숨어");
            Assert.AreEqual(CommandAction.HIDE, game.Commands.LastCommand.action);
            yield return Send("주변 상황 보고해");
            Assert.IsTrue(game.Commands.LastResponse.Contains("연구실"));
            var report = game.Evidence.Entries.Single(e => e.id == "FIELD_REPORT_LABORATORY");
            Assert.AreEqual("FIELD_AGENT_REPORT", report.sourceId);
            Assert.IsTrue(report.timestamp.StartsWith("2026-10-02"));
            Assert.AreEqual("LABORATORY", report.locationId);
            yield return Send("731로 연구실 캐비닛 열어");
            yield return Send("USB를 가져오지 마", CommandTarget.FIELD_AGENT, false);
            Assert.IsFalse(game.Rules.HasUsb);
            Assert.IsFalse(game.World.Objects["USB"].IsCollected);
            var position = game.World.Agent.transform.position;
            yield return Send("서버실을 폭파해", CommandTarget.FIELD_AGENT, false);
            Assert.AreEqual(CommandStatus.FAILED, game.Commands.LastStatus);
            Assert.Less(Vector3.Distance(position, game.World.Agent.transform.position), .1f);
        }

        [UnityTest]
        public IEnumerator AC05_Ambiguity_AsksBeforeActing_AnalysisDoesNotLosePendingContext()
        {
            game.Hud.StartButton.onClick.Invoke();
            yield return Send("메인 복도로 이동해");
            var position = game.World.Agent.transform.position;
            yield return Send("문 열어", CommandTarget.FIELD_AGENT, false);
            Assert.AreEqual(CommandStatus.NEEDS_CLARIFICATION, game.Commands.LastStatus);
            Assert.IsNotNull(game.Commands.Clarification.Pending);
            Assert.GreaterOrEqual(game.Commands.Clarification.Candidates.Count, 2);
            Assert.IsFalse(game.World.Objects["LAB_DOOR"].IsOpen);
            Assert.IsFalse(game.World.Objects["STORAGE_DOOR"].IsOpen);
            Assert.Less(Vector3.Distance(position, game.World.Agent.transform.position), .1f);
            yield return Send("22시 이후 연구실 출입자 찾아줘", CommandTarget.ANALYSIS_SYSTEM);
            Assert.IsNotNull(game.Commands.Clarification.Pending);
            yield return Send("연구실");
            Assert.AreEqual(CommandAction.OPEN, game.Commands.LastCommand.action);
            Assert.AreEqual("LAB_DOOR", game.Commands.LastCommand.objectId);
            Assert.IsTrue(game.World.Objects["LAB_DOOR"].IsOpen);
            Assert.IsFalse(game.World.Objects["STORAGE_DOOR"].IsOpen);
            Assert.IsNull(game.Commands.Clarification.Pending);
            var clarified = game.Journal.Entries.Where(e => e.Prompt == "문 열어").ToArray();
            Assert.AreEqual(1, clarified.Length, "An intervening analysis must not duplicate the pending field request.");
            Assert.AreEqual(CommandStatus.COMPLETED, clarified[0].Status);
            Assert.Greater(clarified[0].ClarificationCount, 0);
        }

        [UnityTest]
        public IEnumerator AC06_AC07_ActualAnalysisFiltersAndUsefulProvenance()
        {
            game.Hud.StartButton.onClick.Invoke();
            float remaining = game.Rules.RemainingSeconds;
            yield return Send("22시 이후 연구실 출입자를 이름과 시간 기준으로 표로 정리해줘", CommandTarget.ANALYSIS_SYSTEM);
            Assert.AreEqual(3, game.Commands.LastAnalysis.rows.Count);
            CollectionAssert.AreEqual(new[] { "22:14", "22:37", "22:52" }, game.Commands.LastAnalysis.rows.Select(r => r.timestamp.Substring(11)).ToArray());
            Assert.IsTrue(game.Commands.LastAnalysis.text.Contains("김민수"));
            Assert.Less(game.Rules.RemainingSeconds, remaining);
            yield return Send("22시 30분부터 22시 45분까지 연구실 출입자 찾아줘", CommandTarget.ANALYSIS_SYSTEM);
            Assert.AreEqual(1, game.Commands.LastAnalysis.rows.Count);
            Assert.AreEqual("박서연", game.Commands.LastAnalysis.rows[0].person);
            Assert.IsTrue(game.Commands.LastAnalysis.evidence.All(e => !string.IsNullOrEmpty(e.sourceId) && !string.IsNullOrEmpty(e.timestamp)));
        }

        [UnityTest]
        public IEnumerator AC08_AC09_StaleInformation_SourceAndCounterEvidence_ChangesDecision()
        {
            game.Hud.StartButton.onClick.Invoke();
            yield return Send("USB 위치 알려줘", CommandTarget.ANALYSIS_SYSTEM);
            Assert.IsTrue(game.Commands.LastAnalysis.text.Contains("서버실"));
            Assert.AreNotEqual(Reliability.CONFIRMED, game.Commands.LastAnalysis.reliability);
            Assert.AreEqual("LABORATORY", game.World.Objects["USB"].Record.LocationId);
            Assert.IsFalse(game.World.Objects["USB"].Record.IsKnown, "Analysis is not permitted to create field knowledge.");
            yield return Send("USB가 서버실에 있다는 근거 보여줘", CommandTarget.ANALYSIS_SYSTEM);
            Assert.IsTrue(game.Commands.LastAnalysis.text.Contains("3일") || game.Commands.LastAnalysis.text.Contains("2026-09-29"));
            yield return Send("이 정보와 충돌하는 최신 자료를 찾아 검증해줘", CommandTarget.ANALYSIS_SYSTEM);
            Assert.IsTrue(game.Commands.LastAnalysis.verifiedLocation);
            Assert.IsTrue(game.Commands.LastAnalysis.text.Contains("연구실"));
            Assert.IsTrue(game.Commands.LastAnalysis.text.Contains("731"));
            Assert.IsTrue(game.Evidence.Entries.Any(e => e.reliability == Reliability.CONFIRMED || e.reliability == Reliability.CONFLICTED));
        }

        [UnityTest]
        public IEnumerator AC10_TimerStopsDuringBriefingAndPause_TimeoutFails()
        {
            float start = game.Rules.RemainingSeconds;
            yield return new WaitForSeconds(.15f);
            Assert.AreEqual(start, game.Rules.RemainingSeconds);
            game.Hud.StartButton.onClick.Invoke();
            yield return new WaitForSeconds(.15f);
            Assert.Less(game.Rules.RemainingSeconds, start);
            game.TogglePause(); float paused = game.Rules.RemainingSeconds;
            yield return new WaitForSeconds(.15f);
            Assert.AreEqual(paused, game.Rules.RemainingSeconds);
            game.TogglePause();
            game.Rules.Tick(600);
            yield return null;
            Assert.AreEqual(MissionState.Failure, game.Rules.State);
            Assert.AreEqual(0f, game.Rules.RemainingSeconds);
            float ended = game.Rules.RemainingSeconds; game.Rules.Tick(5); Assert.AreEqual(ended, game.Rules.RemainingSeconds);
            yield return Send("메인 복도로 이동해", CommandTarget.FIELD_AGENT, false);
            Assert.AreEqual(CommandStatus.FAILED, game.Commands.LastStatus);
        }

        [UnityTest]
        public IEnumerator AC10_PauseSuspendsActualMovement_ThenResumes()
        {
            game.Hud.StartButton.onClick.Invoke();
            Submit("메인 복도로 이동해", CommandTarget.FIELD_AGENT);
            yield return new WaitForSeconds(.3f);
            game.TogglePause();
            yield return null;
            var pausedPosition = game.World.Agent.transform.position;
            yield return new WaitForSeconds(.2f);
            Assert.Less(Vector3.Distance(pausedPosition, game.World.Agent.transform.position), .03f);
            Assert.IsTrue(game.Commands.IsFieldProcessing);
            game.TogglePause();
            yield return WaitIdle();
            Assert.AreEqual("MAIN_HALL", game.World.Agent.CurrentLocationId);
            Assert.AreEqual(CommandStatus.COMPLETED, game.Commands.LastStatus);
        }

        [UnityTest]
        public IEnumerator AC11_GuardDetection_UsesDistanceAngleAndPhysicalOcclusion()
        {
            game.Hud.StartButton.onClick.Invoke();
            game.World.Guard.Navigation.Warp(game.World.Locations["MAIN_HALL"]);
            game.World.Guard.transform.rotation = Quaternion.LookRotation(Vector3.forward);
            game.World.Agent.Navigation.Warp(new Vector3(0, 0, 2));
            Physics.SyncTransforms();
            Assert.IsTrue(game.World.Guard.DetectAgent());
            yield return null;
            Assert.AreEqual(MissionState.Failure, game.Rules.State);
            Assert.AreEqual(AgentState.Caught, game.World.Agent.State);
            Assert.IsNotEmpty(game.Rules.FailureReason);
        }

        [UnityTest]
        public IEnumerator GuardClearCondition_BlocksReportDuringThreat_AndPause_ThenResumes()
        {
            game.Hud.StartButton.onClick.Invoke();
            game.World.Guard.CanAct = () => false; // Isolate the condition gate from the capture failure branch.
            Assert.IsTrue(game.World.Guard.Navigation.Warp(game.World.Locations["ENTRANCE"] + Vector3.forward * 2));
            Physics.SyncTransforms();
            Submit("경비가 지나가면 주변 상황 보고해", CommandTarget.FIELD_AGENT);
            yield return new WaitForSeconds(.1f);
            Assert.IsTrue(game.Commands.IsFieldProcessing);
            Assert.AreEqual(AgentState.Idle, game.World.Agent.State);
            Assert.AreEqual(CommandStatus.EXECUTING, game.Journal.Entries.Last().Status);
            game.TogglePause();
            Assert.IsTrue(game.World.Guard.Navigation.Warp(game.World.Locations["SERVER_ROOM"]));
            Physics.SyncTransforms();
            yield return new WaitForSeconds(.2f);
            Assert.IsTrue(game.Commands.IsFieldProcessing);
            Assert.AreEqual(AgentState.Idle, game.World.Agent.State);
            game.TogglePause();
            yield return WaitIdle();
            Assert.AreEqual(CommandStatus.COMPLETED, game.Commands.LastStatus);
            Assert.IsTrue(game.Commands.LastResponse.Contains("출입구"));
            Assert.AreEqual(1, game.Journal.Entries.Count);
        }

        [UnityTest]
        public IEnumerator GuardClearCondition_TimeoutFailsWithoutStartingAction()
        {
            game.Hud.StartButton.onClick.Invoke();
            game.World.Guard.CanAct = () => false;
            Assert.IsTrue(game.World.Guard.Navigation.Warp(game.World.Locations["ENTRANCE"] + Vector3.forward * 2));
            Physics.SyncTransforms();
            Time.timeScale = 10;
            Submit("경비가 지나가면 주변 상황 보고해", CommandTarget.FIELD_AGENT);
            yield return WaitIdle();
            Assert.AreEqual(CommandStatus.FAILED, game.Commands.LastStatus);
            Assert.IsTrue(game.Commands.LastResponse.Contains("GUARD_CLEAR"));
            Assert.AreEqual(AgentState.Idle, game.World.Agent.State);
            Assert.AreEqual("ENTRANCE", game.World.Agent.CurrentLocationId);
            Assert.AreEqual(MissionState.Playing, game.Rules.State);
        }

        [UnityTest]
        public IEnumerator InterruptedCommand_RecordsFailureAndCause()
        {
            game.Hud.StartButton.onClick.Invoke();
            game.World.Guard.CanAct = () => false;
            Assert.IsTrue(game.World.Guard.Navigation.Warp(game.World.Locations["ENTRANCE"] + Vector3.forward * 2));
            Physics.SyncTransforms();
            Submit("경비가 지나가면 주변 상황 보고해", CommandTarget.FIELD_AGENT);
            yield return null;
            game.Rules.Tick(600);
            yield return null;
            Assert.AreEqual(MissionState.Failure, game.Rules.State);
            Assert.IsFalse(game.Commands.IsProcessing);
            Assert.AreEqual(CommandStatus.FAILED, game.Commands.LastStatus);
            Assert.AreEqual(1, game.Journal.Entries.Count);
            Assert.AreEqual(CommandStatus.FAILED, game.Journal.Entries[0].Status);
            Assert.AreEqual(game.Rules.FailureReason, game.Journal.Entries[0].Message);
        }

        [UnityTest]
        public IEnumerator AC11_GuardRejectsTargetsBehindOutsideRangeAndOccludedByWall()
        {
            game.Hud.StartButton.onClick.Invoke();
            game.World.Guard.enabled = false; // Directly test actual distance/angle/Physics checks without patrol motion.
            Assert.IsTrue(game.World.Guard.Navigation.Warp(game.World.Locations["MAIN_HALL"]));
            game.World.Guard.transform.rotation = Quaternion.LookRotation(Vector3.forward);
            Assert.IsTrue(game.World.Agent.Navigation.Warp(new Vector3(0, 0, 2)));
            Physics.SyncTransforms();
            Assert.IsTrue(game.World.Guard.DetectAgent());
            Assert.IsTrue(game.World.Agent.Navigation.Warp(new Vector3(0, 0, -2)));
            Physics.SyncTransforms();
            Assert.IsFalse(game.World.Guard.DetectAgent(), "A nearby target behind the guard is outside the view angle.");
            game.World.Guard.transform.rotation = Quaternion.LookRotation(Vector3.back);
            Assert.IsTrue(game.World.Agent.Navigation.Warp(game.World.Locations["ENTRANCE"]));
            Assert.IsFalse(game.World.Guard.DetectAgent(), "A target outside detection distance cannot be caught.");
            game.World.Guard.transform.rotation = Quaternion.LookRotation(Vector3.forward);
            Assert.IsTrue(game.World.Agent.Navigation.Warp(new Vector3(0, 0, 2)));
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = "Acceptance LOS obstacle"; wall.layer = MissionWorld.ObstacleLayer;
            wall.transform.SetParent(game.transform);
            wall.transform.position = new Vector3(0, 1.5f, 1); wall.transform.localScale = new Vector3(2, 3, .3f);
            Physics.SyncTransforms();
            Assert.IsFalse(game.World.Guard.DetectAgent(), "A collider on the obstacle layer must block guard sight.");
            yield return null;
        }

        [UnityTest]
        public IEnumerator AC11_ActualTerminalPurge_DestroysCriticalEvidence()
        {
            game.Hud.StartButton.onClick.Invoke();
            // Isolate the evidence-loss failure branch; successful E2E below keeps the patrol active.
            game.World.Guard.CanAct = () => false;
            yield return Send("메인 복도로 이동해");
            yield return Send("창고 문 열어");
            yield return Send("창고로 이동해");
            yield return Send("서버실로 이동해");
            yield return Send("터미널 확인해");
            Assert.IsTrue(game.Commands.LastResponse.Contains("PURGE"));
            yield return Send("터미널 열어", CommandTarget.FIELD_AGENT, false);
            Assert.AreEqual(MissionState.Failure, game.Rules.State);
            Assert.IsNotEmpty(game.Rules.FailureReason);
        }

        [UnityTest]
        public IEnumerator Task011_LlmUnavailable_DoesNotChangeWorldOrStopTimer()
        {
            game.Hud.StartButton.onClick.Invoke();
            game.Interpreter.Configure("", ""); game.Commands.UseLlm = true;
            var initial = game.World.Agent.transform.position;
            float remaining = game.Rules.RemainingSeconds;
            yield return Send("연구실로 이동해", CommandTarget.FIELD_AGENT, false);
            Assert.AreEqual(CommandStatus.FAILED, game.Commands.LastStatus);
            Assert.IsTrue(game.Commands.LastResponse.Contains("COMMAND PROCESSING FAILED"));
            Assert.AreEqual(initial, game.World.Agent.transform.position);
            yield return new WaitForSeconds(.1f);
            Assert.Less(game.Rules.RemainingSeconds, remaining);
            Assert.AreEqual(MissionState.Playing, game.Rules.State);
        }

        [UnityTest]
        public IEnumerator AC12_CompleteMissionThroughUgui_WithAllLearningMomentsAndRealGuard()
        {
            Assert.AreEqual(MissionState.Briefing, game.Rules.State);
            Assert.IsNull(game.Journal.BuildProfile(MissionState.Playing), "Skill profile stays off gameplay UI.");
            game.Hud.StartButton.onClick.Invoke();
            game.Hud.CameraImages[0].GetComponent<Button>().onClick.Invoke();
            Assert.AreEqual(0, game.Hud.SelectedCamera); game.Hud.CloseZoom(); // Observe.
            yield return Send("메인 복도로 이동해"); // Command.
            yield return Send("문 열어", CommandTarget.FIELD_AGENT, false);
            Assert.AreEqual(CommandStatus.NEEDS_CLARIFICATION, game.Commands.LastStatus);
            yield return Send("연구실"); // Clarify without changing the original action.
            yield return Send("USB 위치 알려줘", CommandTarget.ANALYSIS_SYSTEM); // Delegate and review.
            Assert.IsTrue(game.Commands.LastAnalysis.text.Contains("서버실"));
            yield return Send("USB가 서버실에 있다는 근거 보여줘", CommandTarget.ANALYSIS_SYSTEM);
            Assert.AreNotEqual(Reliability.CONFIRMED, game.Commands.LastAnalysis.reliability); // Discover stale information.
            yield return Send("이 결론과 충돌하는 자료를 찾아 검증해줘", CommandTarget.ANALYSIS_SYSTEM);
            Assert.IsTrue(game.Commands.LastAnalysis.verifiedLocation); // Verify / follow up.
            var code = Regex.Match(game.Commands.LastAnalysis.text, @"(?<!\d)\d{3}(?!\d)");
            Assert.IsTrue(code.Success, "The verified in-game document must expose the unlock code.");
            yield return Send("경비에게 들키지 않게 연구실로 이동해"); // Decide and act.
            yield return Send("연구실 캐비닛 확인해");
            yield return Send(code.Value + "로 연구실 캐비닛 열어");
            Assert.IsTrue(game.World.Objects["LAB_CABINET"].IsOpen);
            yield return Send("USB를 가져와");
            Assert.IsTrue(game.Rules.HasUsb);
            Assert.AreEqual(MissionState.Playing, game.Rules.State, "USB alone is insufficient.");
            Assert.IsTrue(game.World.Guard.enabled, "Success path must keep actual guard patrol/detection enabled.");
            yield return Send("경비에게 들키지 않게 출입구로 이동해", CommandTarget.FIELD_AGENT, false);
            Assert.AreEqual(MissionState.Success, game.Rules.State);
            Assert.Greater(game.Rules.RemainingSeconds, 0);
            Assert.IsTrue(game.Journal.Entries.Any(e => e.ClarificationCount > 0));
            Assert.IsTrue(game.Journal.Entries.Any(e => e.VerificationRequest));
            Assert.IsTrue(game.Journal.Entries.Any(e => e.ParsedCommand != null && e.ParsedCommand.target == CommandTarget.ANALYSIS_SYSTEM));
            game.Hud.ResultButton.onClick.Invoke();
            yield return null;
            Assert.AreEqual(MissionState.Result, game.Rules.State);
            Assert.AreEqual(MissionState.Success, game.Rules.Outcome);
            Assert.IsNotNull(game.Journal.BuildProfile(MissionState.Result));
            Assert.IsNotEmpty(game.Journal.GetResultSummary(game.Rules));
            if (Application.CanStreamedLevelBeLoaded("Result")) Assert.AreEqual("Result", SceneManager.GetActiveScene().name);
            yield return CaptureHud("mission-result.png");
        }

        [UnityTest]
        public IEnumerator Task020_ReferenceResolution_UiControlsStayInsideTheirPanels()
        {
            Canvas.ForceUpdateCanvases();
            Assert.AreEqual(new Vector2(1920, 1080), game.Hud.Canvas.GetComponent<CanvasScaler>().referenceResolution);
            foreach (var control in new RectTransform[] { game.Hud.CommandInput.GetComponent<RectTransform>(), game.Hud.TargetDropdown.GetComponent<RectTransform>(), game.Hud.SendButton.GetComponent<RectTransform>() })
            {
                var parent = control.parent as RectTransform;
                var corners = new Vector3[4]; control.GetWorldCorners(corners);
                foreach (var corner in corners)
                {
                    var local = parent.InverseTransformPoint(corner);
                    Assert.IsTrue(parent.rect.Contains(new Vector2(local.x, local.y)), control.name + " must fit command panel.");
                }
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator Task018_RestartCreatesFreshNavigationAndClickableInput()
        {
            game.Hud.StartButton.onClick.Invoke();
            yield return Send("메인 복도로 이동해");
            game.Rules.Tick(600);
            yield return null;
            game.Hud.ResultButton.onClick.Invoke();
            yield return null;
            game.Hud.RestartButton.onClick.Invoke();
            yield return null; yield return null;
            game = GameManager.Instance;
            Assert.IsNotNull(game);
            Assert.AreEqual(MissionState.Briefing, game.Rules.State);
            Assert.AreEqual(600f, game.Rules.RemainingSeconds);
            Assert.IsFalse(game.Rules.HasUsb);
            Assert.AreEqual(0, game.Journal.Entries.Count);
            Assert.AreEqual(0, game.Evidence.Entries.Count);
            Assert.IsTrue(game.World.Agent.Navigation.isOnNavMesh);
            var systems = UnityEngine.Object.FindObjectsByType<UnityEngine.EventSystems.EventSystem>(FindObjectsSortMode.None);
            Assert.AreEqual(1, systems.Length, "Restart must replace the old persistent EventSystem.");
            var pointer = new UnityEngine.EventSystems.PointerEventData(systems[0]);
            pointer.position = RectTransformUtility.WorldToScreenPoint(null, game.Hud.StartButton.transform.TransformPoint(game.Hud.StartButton.GetComponent<RectTransform>().rect.center));
            var hits = new System.Collections.Generic.List<UnityEngine.EventSystems.RaycastResult>();
            systems[0].RaycastAll(pointer, hits);
            Assert.IsTrue(hits.Any(h => h.gameObject == game.Hud.StartButton.gameObject), "Briefing Start button must receive pointer raycasts after restart.");
            game.Hud.StartButton.onClick.Invoke();
            yield return Send("메인 복도로 이동해");
        }

        private IEnumerator Send(string prompt, CommandTarget target = CommandTarget.FIELD_AGENT, bool expectCompleted = true)
        {
            Submit(prompt, target);
            yield return WaitIdle();
            if (expectCompleted) Assert.AreEqual(CommandStatus.COMPLETED, game.Commands.LastStatus, prompt + " -> " + game.Commands.LastResponse);
        }
        private void Submit(string prompt, CommandTarget target)
        {
            game.Hud.TargetDropdown.value = target == CommandTarget.FIELD_AGENT ? 0 : 1;
            game.Hud.CommandInput.text = prompt;
            game.Hud.SendButton.onClick.Invoke();
        }
        private IEnumerator WaitIdle()
        {
            float timeout = Time.realtimeSinceStartup + 35f;
            while (game.Commands.IsProcessing)
            {
                Assert.Less(Time.realtimeSinceStartup, timeout, "Command timed out: " + game.Commands.LastResponse);
                yield return null;
            }
            yield return null;
        }
        private IEnumerator CaptureHud(string filename)
        {
            var canvas = game.Hud.Canvas;
            var originalMode = canvas.renderMode;
            var originalCamera = canvas.worldCamera;
            float originalDistance = canvas.planeDistance;
            var cameraRoot = new GameObject("Acceptance HUD capture");
            var captureCamera = cameraRoot.AddComponent<Camera>();
            var target = new RenderTexture(1920, 1080, 24, RenderTextureFormat.ARGB32);
            Texture2D image = null;
            int frames = 0;
            Action<ScriptableRenderContext, Camera> rendered = (context, camera) => { if (camera == captureCamera) frames++; };
            RenderPipelineManager.endCameraRendering += rendered;
            try
            {
                target.Create();
                captureCamera.targetTexture = target;
                captureCamera.clearFlags = CameraClearFlags.SolidColor;
                captureCamera.backgroundColor = Color.black;
                captureCamera.depth = 50;
                captureCamera.transform.position = new Vector3(0, 0, -30);
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = captureCamera;
                canvas.planeDistance = 1;
                Canvas.ForceUpdateCanvases();
                float deadline = Time.realtimeSinceStartup + 10;
                while (frames < 2)
                { Assert.Less(Time.realtimeSinceStartup, deadline, "HUD capture camera did not render."); yield return null; }
                image = Read(target);
                Assert.Greater(image.GetPixels32().Distinct().Count(), 30, "The HUD capture must contain actual UI pixels.");
                int errorPixels = image.GetPixels32().Count(pixel => pixel.r > 200 && pixel.g < 50 && pixel.b > 200);
                Assert.Less(errorPixels, image.width * image.height / 100, "A broad magenta shader-error display is not a usable CCTV feed.");
                string path = Path.Combine(Application.dataPath, "../TestResults/" + filename);
                File.WriteAllBytes(path, image.EncodeToPNG());
                Assert.IsTrue(File.Exists(path));
            }
            finally
            {
                RenderPipelineManager.endCameraRendering -= rendered;
                canvas.renderMode = originalMode; canvas.worldCamera = originalCamera; canvas.planeDistance = originalDistance;
                captureCamera.targetTexture = null;
                target.Release(); UnityEngine.Object.Destroy(target); UnityEngine.Object.Destroy(cameraRoot);
                if (image != null) UnityEngine.Object.Destroy(image);
                Canvas.ForceUpdateCanvases();
            }
        }
        private static Texture2D Read(RenderTexture texture)
        {
            var previous = RenderTexture.active; RenderTexture.active = texture;
            var copy = new Texture2D(texture.width, texture.height, TextureFormat.RGB24, false);
            copy.ReadPixels(new Rect(0, 0, texture.width, texture.height), 0, 0); copy.Apply(); RenderTexture.active = previous;
            return copy;
        }
        private static int Hash(Texture2D texture)
        {
            unchecked { int hash = 17; var colors = texture.GetPixels32(); for (int i = 0; i < colors.Length; i += 37) hash = hash * 31 + colors[i].GetHashCode(); return hash; }
        }
    }
}
