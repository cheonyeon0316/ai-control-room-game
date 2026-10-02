# Implementation contracts

Namespace: `ControlRoom`. Unity 6000.3.11f1, URP, uGUI, runtime NavMesh.

`Assets/Scripts/Core/Contracts.cs` is owned by root. Public data contracts are fixed; request additive changes via messages.

## Command domain (agent commands)

Own `Assets/Scripts/Command/` excluding `CommandManager.cs` (root).
Implement `RuleCommandParser.Parse(string prompt, CommandTarget target): ParseResult`, `CommandValidator.Validate(Command command, WorldContext context): ValidationResult`, `ClarificationContext` with `Pending`, `Candidates`, `Begin(ValidationResult)` / `Resolve(string response, WorldContext context): ValidationResult` / `Clear()`.
Validator resolves generic object kinds, rejects invalid/unavailable/unknown context and ambiguity; evaluates sequential locations in order. Preserve all restrictions. Clarification changes only the missing target, keeps original action and sequence.
Implement serializable JSON codec accepting legacy object/location aliases and strict enum validation, and Unity external LLM adapter translating only to Command, timeout/error isolation, no hardcoded keys. Root integrates async adapter into dispatcher.

## Analysis domain (agent analysis)

Own `Assets/Scripts/Analysis/`, `Assets/Scripts/Evidence/`, `Assets/Scripts/Mission/MissionRules.cs`, `Assets/Scripts/Core/CommandJournal.cs`.
Implement `AnalysisService.Query(string prompt, string outputFormat = "summary"): AnalysisResult`; authored 6-8 data source documents, 5 evidence records, access log including 22:14 김민수/22:37 박서연/22:52 김민수. Initial USB claim is stale SERVER_ROOM (3 days old). Source query exposes date; conflict query reveals latest relocation to LABORATORY locked cabinet `LAB_CABINET` (code `731`), 김민수 involvement. Never query Unity game truth. `EvidenceDatabase.AddRange(IEnumerable<EvidenceRecord>)`, `Entries`, `Clear()`.
Implement `MissionRules` state/timer/risk: `State`, `RemainingSeconds`, `ElapsedSeconds`, `Paused`, `HasUsb`, `FailureReason`, `Risk`, `Start()`, `Tick(float delta)`, `SetPaused(bool)`, `AcquireUsb()`, `ReachExit()`, `Caught()`, `DestroyCriticalEvidence()`, `LoseObjective()`, `AddRisk(float)`, `ShowResult()`, `Reset()`. Success iff USB + exit. Timer initial 600, only Playing/unpaused tick; terminal states immutable. Root handles scene Result transitions.
`CommandJournal.Record(prompt,command,status,message,clarifications,verification)` and accessible entries/profile/result summary, no score during gameplay.
`requestTarget` optional metadata preserves the intended assignee before external interpretation has produced a command. Pending entries can be completed after another assignee's request finishes; cancellation records the actual mission cause.

## Unity world (agent runtime)

Own `Assets/Scripts/Agent/`, `Assets/Scripts/CCTV/`, `Assets/Scripts/Mission/MissionWorld.cs`, `Assets/Scripts/Core/LowPolyVisual.cs` (optional).
Implement `MissionWorld : MonoBehaviour`: `Build()`, `Locations: Dictionary<string,Vector3>`, `Objects: Dictionary<string,WorldObject>`, `Agent: FieldAgent`, `Guard: GuardController`, `Cctv: CctvManager`, `GetContext(): WorldContext`. Bootstrap root calls Build after adding component, assign delegates before build if needed.
World 5 rooms: Entrance (0,0,-12), Main Hall (0,0,0), Lab (14,0,0), Storage (0,0,14), Server (14,0,14), connected walkable corridors. Agent starts Entrance. Exits same as Entrance. Agent speed ~5.2 for playable pacing, guard stays patrol Server/Storage route; avoid guard restriction waits/uses safe route where needed. Geometry on wall/obstacle layer for LOS. Build NavMesh with UnityEngine.AI.NavMeshBuilder or AI Navigation. Four real cameras RenderTexture (640x360), distinct views, lowpoly readable art. Doors initially closed visual; navigation should require opening LAB_DOOR at least before laboratory movement or interaction.
`WorldObject` exposes `Record`, `Transform`, `IsOpen`, `IsCollected`, `Unlock(string code)`, optional `RequiredCode`. USB physically in locked LAB_CABINET. Agent's own local inspection/report discovers USB knowledge, no global truth leak. Terminal inspect destructive operation warning, OPEN terminal interpreted as purge may cause evidence loss (root delegate).
`FieldAgent : MonoBehaviour`: `State`, `CurrentLocationId`, `HasUsb`, `LastSuccess`, `LastMessage`, `IsBusy`, `Func<bool> CanAct`, `Action OnUsbAcquired`, `Action OnExitReached`, `Action OnEvidenceDestroyed`, `Action<float> OnRisk`, `IEnumerator ExecuteStep(CommandStep step, List<string> restrictions)`, `Cancel()`, `Initialize(MissionWorld world)`. Guard may set caught through `Catch()`; actual Transform and NavMeshAgent movement must happen. Object interactions require same room and navigate to object stand point. HIDE/WAIT/REPORT/PHOTO; PHOTO yields local evidence via `Action<EvidenceRecord> OnEvidence`.
`GuardController`: `Action OnCaught`, `Func<bool> CanAct`, `FieldAgent Agent`, `RiskLevel Risk`, `bool DetectAgent()` distance+angle+Physics.Raycast, patrol Update. Tests may position transform for failure branch but no debug in success E2E. `CctvManager`: `Cameras`, `Textures` lists, `Names`, `Ids`, `SelectedIndex`, `Select(int)`, `SelectedTexture`, `Dispose()`. Root owns camera UI.

`FieldAgent.IsPaused` suspends actions; `EvidenceTimestamp` supplies mission time. PHOTO waits for an actual SRP camera-render completion and saves the captured PNG before adding evidence with `imagePath`. REPORT/INSPECT record observed room/object state. `ProceduralCharacterPose` applies poses relative to initial transforms; exposed `CurrentVisualPose` describes Idle/Walk/Run/action/Caught. `ReleaseNavigation()` removes the previous mission's NavMesh during restart.
`FieldAgent.SetPaused` and `GuardController.SetPaused` synchronously suspend native navigation through `NavMeshPauseState`, preserve its path, and restore its update flags on resume. Geometry uses layer 8, the field visual uses layer 9, and surveillance floor labels use layer 10. PHOTO excludes layers 9/10, while CCTV retains both. Subject bounds determine photo framing.

CCTV feeds use `Resources/Shaders/CctvFeed` for restrained noise, desaturation, subpixel colour separation and occasional narrow disturbance. Overlay scanlines and labels remain independent. The actual shader and character appearance require Unity rendering validation.

## Root

Own project configuration/Scenes/editor tooling/uGUI/CommandManager/GameManager/tests/Docs. All tests call actual public parser and Unity interactions, no fabricated pass reports. UI acceptance uses InputField/Dropdown/Button events; Unity test XML is source of results. Initial blank project = all AC unimplemented. Record each failing suite, SPEC-derived task revision and rerun in TASK.md / acceptance report.
