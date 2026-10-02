using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;

namespace ControlRoom
{
    [RequireComponent(typeof(NavMeshAgent))]
    public sealed class FieldAgent : MonoBehaviour
    {
        public AgentState State { get; private set; } = AgentState.Idle;
        public string CurrentLocationId { get; private set; } = "ENTRANCE";
        public bool HasUsb { get; private set; }
        public bool LastSuccess { get; private set; }
        public string LastMessage { get; private set; } = "요원 대기 중. 출입구에 있습니다.";
        public bool IsBusy { get; private set; }
        public Func<bool> CanAct;
        public Func<bool> IsPaused;
        public Func<string> EvidenceTimestamp;
        public Action OnUsbAcquired, OnExitReached, OnEvidenceDestroyed;
        public Action<float> OnRisk;
        public Action<EvidenceRecord> OnEvidence;
        public Transform Visual;
        public string CurrentVisualPose { get; private set; } = "Idle";
        public NavMeshAgent Navigation => navigation;
        private MissionWorld world;
        private NavMeshAgent navigation;
        private int cancellationVersion;
        private int evidenceSerial;
        private float poseTime;
        private float statePoseTime;
        private AgentState lastPoseState = AgentState.Idle;
        private CommandAction activePoseAction;
        private ProceduralCharacterPose characterPose;
        private readonly NavMeshPauseState navigationPause = new NavMeshPauseState();
        private GameObject photoCameraRoot;
        private RenderTexture photoTarget;
        private Texture2D photoReadback;
        private Action<ScriptableRenderContext, Camera> photoSrpCallback;
        private Camera.CameraCallback photoBuiltinCallback;

        public void Initialize(MissionWorld missionWorld)
        {
            world = missionWorld;
            if (Visual != null) characterPose = new ProceduralCharacterPose(Visual);
            navigation = GetComponent<NavMeshAgent>();
            navigation.speed = 5.2f;
            navigation.acceleration = 18;
            navigation.angularSpeed = 540;
            navigation.radius = .32f;
            navigation.height = 1.9f;
            navigation.stoppingDistance = .12f;
            navigation.autoBraking = true;
            navigation.obstacleAvoidanceType = ObstacleAvoidanceType.HighQualityObstacleAvoidance;
            if (NavMesh.SamplePosition(transform.position, out NavMeshHit start, 2f, NavMesh.AllAreas)) navigation.Warp(start.position);
            else Debug.LogError("Field agent has no valid runtime NavMesh start.");
        }

        public IEnumerator ExecuteStep(CommandStep step, List<string> restrictions)
        {
            LastSuccess = false;
            if (step == null) { LastMessage = "행동 정보가 없습니다."; yield break; }
            if (IsBusy) { LastMessage = "진행 중인 행동이 끝난 뒤 지시해 주세요."; yield break; }
            if (State == AgentState.Caught || !Allowed()) { LastMessage = "현재 임무에서는 명령을 수행할 수 없습니다."; yield break; }
            if (!Enum.IsDefined(typeof(CommandAction), step.action) || step.action == CommandAction.ANALYZE)
            { LastMessage = "현장 요원이 수행할 수 없는 행동입니다."; yield break; }
            restrictions = restrictions ?? new List<string>();
            if (step.action == CommandAction.OPEN && restrictions.Contains("DO_NOT_OPEN"))
            { LastMessage = "문을 열지 말라는 제한 때문에 실행하지 않았습니다."; yield break; }
            if (step.action == CommandAction.PICKUP && restrictions.Contains("DO_NOT_PICKUP"))
            { LastMessage = "가져오지 말라는 제한 때문에 실행하지 않았습니다."; yield break; }
            int version = cancellationVersion;
            IsBusy = true;
            LastSuccess = true;
            activePoseAction = step.action;
            State = AgentState.Idle;
            try
            {
                switch (step.action)
                {
                    case CommandAction.MOVE:
                        if (string.IsNullOrEmpty(step.locationId) || !world.Locations.TryGetValue(step.locationId, out Vector3 destination))
                        { Fail("이동할 장소를 알 수 없습니다."); break; }
                        if (NeedsClosedDoor(step.locationId, out string door))
                        { Fail(door + "이 닫혀 있습니다. 메인 복도에서 문을 먼저 열어 주세요."); break; }
                        yield return MoveTo(destination, restrictions, version);
                        if (!LastSuccess) break;
                        CurrentLocationId = step.locationId == "EXIT" ? "ENTRANCE" : step.locationId;
                        LastMessage = LocationLabel(CurrentLocationId) + "에 도착했습니다.";
                        if (CurrentLocationId == "ENTRANCE") OnExitReached?.Invoke();
                        break;
                    case CommandAction.WAIT:
                        State = AgentState.Waiting;
                        yield return Delay(Mathf.Clamp(step.duration, .1f, 600f), version);
                        if (LastSuccess) LastMessage = "대기를 마쳤습니다.";
                        break;
                    case CommandAction.HIDE:
                        if (navigation != null && navigation.isOnNavMesh) navigation.ResetPath();
                        State = AgentState.Hidden;
                        LastMessage = "엄폐하고 있습니다. 다음 명령이 올 때까지 경비 시야에서 숨습니다.";
                        OnRisk?.Invoke(-12);
                        break;
                    case CommandAction.REPORT:
                        State = AgentState.Reporting;
                        yield return Delay(.75f, version);
                        if (LastSuccess)
                        {
                            LastMessage = LocalReport();
                            StoreFieldObservation("FIELD_REPORT_" + CurrentLocationId,
                                "현장 보고 / " + LocationLabel(CurrentLocationId), LastMessage);
                        }
                        break;
                    case CommandAction.PHOTO:
                        State = AgentState.Reporting;
                        WorldObject photoObject = null;
                        if (!string.IsNullOrEmpty(step.objectId))
                        {
                            if (!TryLocalObject(step.objectId, out photoObject)) break;
                            yield return MoveTo(photoObject.StandPoint, restrictions, version);
                            if (!LastSuccess) break;
                        }
                        State = AgentState.Reporting;
                        yield return Delay(.7f, version);
                        if (LastSuccess) yield return CapturePhoto(photoObject, version);
                        break;
                    case CommandAction.INSPECT:
                    case CommandAction.PICKUP:
                    case CommandAction.OPEN:
                        if (!TryLocalObject(step.objectId, out WorldObject obj)) break;
                        if (step.action == CommandAction.OPEN && obj.Record.Kind == "TERMINAL" && restrictions.Contains("DO_NOT_DESTROY"))
                        { Fail("증거를 파괴하지 말라는 제한 때문에 터미널의 삭제 작업을 중단했습니다."); break; }
                        if (step.action == CommandAction.PICKUP && obj.Record.Kind == "USB" && !world.Objects["LAB_CABINET"].IsOpen)
                        { Fail("USB는 잠긴 캐비닛 안에 있습니다. 출처를 검증하여 코드를 확인하고 열어 주세요."); break; }
                        yield return MoveTo(obj.StandPoint, restrictions, version);
                        if (!LastSuccess) break;
                        State = AgentState.Interacting;
                        yield return Delay(.85f, version);
                        if (!LastSuccess) break;
                        Interact(step.action, obj, restrictions);
                        break;
                }
            }
            finally
            {
                if (version != cancellationVersion) LastSuccess = false;
                IsBusy = false;
                if (State != AgentState.Hidden && State != AgentState.Caught) State = AgentState.Idle;
            }
        }

        private bool NeedsClosedDoor(string destination, out string door)
        {
            door = "";
            if (destination == "LABORATORY" && CurrentLocationId != "LABORATORY" && !world.Objects["LAB_DOOR"].IsOpen)
            { door = "연구실 문"; return true; }
            if ((destination == "STORAGE" || destination == "SERVER_ROOM") && CurrentLocationId != "STORAGE" && CurrentLocationId != "SERVER_ROOM" && !world.Objects["STORAGE_DOOR"].IsOpen)
            { door = "창고 문"; return true; }
            return false;
        }

        private bool TryLocalObject(string id, out WorldObject obj)
        {
            obj = null;
            if (string.IsNullOrEmpty(id) || !world.Objects.TryGetValue(id, out obj))
            { Fail("지시한 물체를 찾을 수 없습니다. 대상을 구체적으로 알려 주세요."); return false; }
            if (obj.IsCollected || !obj.Record.IsAvailable)
            { Fail("이 물체는 이미 회수했거나 사용할 수 없습니다."); return false; }
            if (!obj.Record.IsKnown)
            { Fail("아직 확인하지 않은 물체입니다. 주변을 먼저 조사해 주세요."); return false; }
            if (obj.Record.LocationId != CurrentLocationId)
            { Fail(obj.Record.Label + "은 현재 방에서 보이지 않습니다. 먼저 " + LocationLabel(obj.Record.LocationId) + "로 이동해 주세요."); return false; }
            return true;
        }

        private IEnumerator MoveTo(Vector3 target, List<string> restrictions, int version)
        {
            if (navigation == null || !navigation.isOnNavMesh || !NavMesh.SamplePosition(target, out NavMeshHit hit, 1.8f, NavMesh.AllAreas))
            { Fail("해당 위치까지 이동 경로가 없습니다."); yield break; }
            var path = new NavMeshPath();
            if (!navigation.CalculatePath(hit.position, path) || path.status != NavMeshPathStatus.PathComplete)
            { Fail("이동 경로가 끊겨 있습니다."); yield break; }
            if (CrossesClosedDoor(path, out string closedDoor))
            { Fail(closedDoor + "이 닫혀 있어 이동할 수 없습니다. 먼저 문을 열어 주세요."); yield break; }
            float cautionElapsed = 0;
            if (restrictions.Contains("AVOID_GUARD"))
            {
                State = AgentState.Waiting;
                while (world.Guard != null && !world.Guard.GuardClearFor(hit.position, this))
                {
                    if (!Continue(version)) yield break;
                    if (Paused()) { yield return null; continue; }
                    cautionElapsed += Time.deltaTime;
                    LastMessage = "경비가 접근 경로를 지나고 있습니다. 안전한 순간까지 대기합니다.";
                    if (cautionElapsed > 18f) { Fail("경비가 계속 대상 구역을 지킵니다. 숨거나 다른 경로를 지시해 주세요."); yield break; }
                    yield return null;
                }
            }
            State = AgentState.Moving;
            navigation.isStopped = false;
            navigation.SetPath(path);
            float elapsed = 0;
            while (navigation.pathPending || navigation.remainingDistance > navigation.stoppingDistance + .08f)
            {
                if (!Continue(version)) { if (navigation.isOnNavMesh) navigation.ResetPath(); yield break; }
                if (Paused())
                {
                    navigation.isStopped = true;
                    yield return null;
                    continue;
                }
                navigation.isStopped = false;
                elapsed += Time.deltaTime;
                CurrentLocationId = world.LocationAt(transform.position);
                if (elapsed > 30f || (!navigation.pathPending && navigation.pathStatus == NavMeshPathStatus.PathInvalid))
                { navigation.ResetPath(); Fail("이동을 완료하지 못했습니다. 경로를 다시 확인해 주세요."); yield break; }
                if (restrictions.Contains("AVOID_GUARD") && world.Guard != null && !world.Guard.GuardClearFor(transform.position, this))
                {
                    navigation.isStopped = true;
                    State = AgentState.Hidden;
                    LastMessage = "경비 접근을 감지했습니다. 엄폐하고 통과를 기다립니다.";
                    yield return Delay(1.25f, version);
                    if (!LastSuccess) yield break;
                    if (!world.Guard.GuardClearFor(transform.position, this))
                    { navigation.ResetPath(); Fail("경비가 가까이 있어 이동을 중단했습니다. 다른 경로를 지시해 주세요."); yield break; }
                    navigation.isStopped = false;
                    State = AgentState.Moving;
                }
                yield return null;
            }
            if (!Continue(version)) yield break;
            navigation.ResetPath();
            CurrentLocationId = world.LocationAt(transform.position);
        }

        private IEnumerator Delay(float seconds, int version)
        {
            float elapsed = 0;
            while (elapsed < seconds)
            {
                if (!Continue(version)) yield break;
                if (!Paused()) elapsed += Time.deltaTime;
                yield return null;
            }
        }

        private IEnumerator CapturePhoto(WorldObject subject, int version)
        {
            var cameraRoot = new GameObject("FIELD_AGENT_CAMERA / Capture");
            photoCameraRoot = cameraRoot;
            try
            {
            cameraRoot.transform.SetParent(world.transform, false);
            Camera camera = cameraRoot.AddComponent<Camera>();
            FramePhoto(camera, subject);
            camera.nearClipPlane = .06f;
            camera.farClipPlane = 40f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.015f, .025f, .035f);
            camera.allowHDR = false;
            camera.allowMSAA = false;
            photoTarget = new RenderTexture(640, 360, 24, RenderTextureFormat.ARGB32)
            { name = "Field photograph", filterMode = FilterMode.Bilinear };
            if (!photoTarget.Create()) { Fail("현장 카메라의 영상 버퍼를 만들지 못했습니다."); yield break; }
            camera.targetTexture = photoTarget;
            int renderedFrames = 0;
            int startedFrame = Time.frameCount;
            photoSrpCallback = (_, renderedCamera) => { if (renderedCamera == camera) renderedFrames++; };
            photoBuiltinCallback = renderedCamera => { if (renderedCamera == camera) renderedFrames++; };
            RenderPipelineManager.endCameraRendering += photoSrpCallback;
            Camera.onPostRender += photoBuiltinCallback;
                float activeElapsed = 0;
                // The render pipeline draws this enabled camera normally; Camera.Render is unsupported by SRP.
                while (renderedFrames < 2 || Time.frameCount - startedFrame < 2)
                {
                    if (!Continue(version)) yield break;
                    if (Paused())
                    {
                        camera.enabled = false;
                        renderedFrames = 0;
                        yield return null;
                        continue;
                    }
                    camera.enabled = true;
                    activeElapsed += Time.unscaledDeltaTime;
                    if (activeElapsed > 5f)
                    { Fail("현장 카메라가 영상을 렌더하지 못해 사진을 저장하지 않았습니다."); yield break; }
                    yield return null;
                }
                while (Paused())
                {
                    if (!Continue(version)) yield break;
                    camera.enabled = false;
                    yield return null;
                }
                if (!Continue(version)) yield break;
                camera.enabled = false;
                if (!SavePhotograph(out string imagePath, out string saveError))
                { Fail("현장 사진 저장 실패: " + saveError); yield break; }
                string report = LocalReport();
                OnEvidence?.Invoke(new EvidenceRecord
                {
                    id = "FIELD_PHOTO_" + (++evidenceSerial), title = LocationLabel(CurrentLocationId) + " 현장 사진",
                    content = report, sourceId = "FIELD_AGENT_CAMERA", timestamp = Timestamp(), imagePath = imagePath,
                    locationId = CurrentLocationId, reliability = Reliability.CONFIRMED
                });
                LastMessage = "현장 사진을 증거함에 등록했습니다. " + report;
            }
            finally
            {
                if (photoCameraRoot == cameraRoot) DisposePhoto();
            }
        }

        private void FramePhoto(Camera camera, WorldObject subject)
        {
            // CCTV keeps these layers visible; field evidence excludes the operator and surveillance guide labels.
            camera.cullingMask = ~((1 << MissionWorld.FieldAgentVisualLayer) | (1 << MissionWorld.SurveillanceLabelLayer));
            camera.aspect = 640f / 360f;
            if (subject == null)
            {
                camera.fieldOfView = 72f;
                camera.transform.position = transform.position + Vector3.up * 1.55f + transform.forward * .3f;
                camera.transform.rotation = Quaternion.LookRotation(transform.forward, Vector3.up);
                return;
            }
            Physics.SyncTransforms();
            Bounds bounds = PhotoBounds(subject);
            Vector3 towardOperator = Vector3.ProjectOnPlane(transform.position - bounds.center, Vector3.up);
            if (towardOperator.sqrMagnitude < .01f) towardOperator = -transform.forward;
            Vector3 back = (towardOperator.normalized + Vector3.up * .16f).normalized;
            Quaternion orientation = Quaternion.LookRotation(-back, Vector3.up);
            Vector3 right = orientation * Vector3.right, up = orientation * Vector3.up;
            float halfWidth = 0, halfHeight = 0, halfDepth = 0;
            for (int x = -1; x <= 1; x += 2)
                for (int y = -1; y <= 1; y += 2)
                    for (int z = -1; z <= 1; z += 2)
                    {
                        Vector3 corner = Vector3.Scale(bounds.extents, new Vector3(x, y, z));
                        halfWidth = Mathf.Max(halfWidth, Mathf.Abs(Vector3.Dot(corner, right)));
                        halfHeight = Mathf.Max(halfHeight, Mathf.Abs(Vector3.Dot(corner, up)));
                        halfDepth = Mathf.Max(halfDepth, Mathf.Abs(Vector3.Dot(corner, back)));
                    }
            camera.fieldOfView = 50f;
            float verticalTangent = Mathf.Tan(camera.fieldOfView * .5f * Mathf.Deg2Rad);
            float horizontalTangent = verticalTangent * camera.aspect;
            float fitDistance = Mathf.Max(halfWidth / horizontalTangent, halfHeight / verticalTangent) + halfDepth;
            float distance = Mathf.Max(.65f, fitDistance * 1.2f);
            camera.transform.position = bounds.center + back * distance;
            camera.transform.rotation = orientation;
        }

        private static Bounds PhotoBounds(WorldObject subject)
        {
            Bounds bounds = new Bounds(subject.Transform.position, Vector3.one * .1f);
            bool hasGeometry = false;
            foreach (Collider collider in subject.Transform.GetComponentsInChildren<Collider>(true))
            {
                if (!collider.enabled || !collider.gameObject.activeInHierarchy) continue;
                if (!hasGeometry) { bounds = collider.bounds; hasGeometry = true; }
                else bounds.Encapsulate(collider.bounds);
            }
            foreach (Renderer renderer in subject.Transform.GetComponentsInChildren<Renderer>(true))
            {
                if (!renderer.enabled || !renderer.gameObject.activeInHierarchy) continue;
                if (!hasGeometry) { bounds = renderer.bounds; hasGeometry = true; }
                else bounds.Encapsulate(renderer.bounds);
            }
            return bounds;
        }

        private bool SavePhotograph(out string imagePath, out string error)
        {
            imagePath = "";
            error = "";
            RenderTexture previous = RenderTexture.active;
            try
            {
                RenderTexture.active = photoTarget;
                photoReadback = new Texture2D(photoTarget.width, photoTarget.height, TextureFormat.RGB24, false);
                photoReadback.ReadPixels(new Rect(0, 0, photoTarget.width, photoTarget.height), 0, 0);
                photoReadback.Apply(false, false);
                byte[] encoded = photoReadback.EncodeToPNG();
                if (encoded == null || encoded.Length < 8) { error = "PNG 영상 인코딩 실패"; return false; }
                string directory = Path.Combine(Application.persistentDataPath, "Photos");
                Directory.CreateDirectory(directory);
                string filename = "FIELD_PHOTO_" + Guid.NewGuid().ToString("N") + ".png";
                imagePath = Path.GetFullPath(Path.Combine(directory, filename));
                File.WriteAllBytes(imagePath, encoded);
                return true;
            }
            catch (Exception exception)
            {
                imagePath = "";
                error = exception.Message;
                return false;
            }
            finally { RenderTexture.active = previous; }
        }

        private void DisposePhoto()
        {
            if (photoSrpCallback != null) RenderPipelineManager.endCameraRendering -= photoSrpCallback;
            if (photoBuiltinCallback != null) Camera.onPostRender -= photoBuiltinCallback;
            photoSrpCallback = null;
            photoBuiltinCallback = null;
            if (photoCameraRoot != null)
            {
                Camera camera = photoCameraRoot.GetComponent<Camera>();
                if (camera != null) { camera.enabled = false; camera.targetTexture = null; }
                Destroy(photoCameraRoot);
            }
            if (photoTarget != null) { photoTarget.Release(); Destroy(photoTarget); }
            if (photoReadback != null) Destroy(photoReadback);
            photoCameraRoot = null;
            photoTarget = null;
            photoReadback = null;
        }

        private string Timestamp() => EvidenceTimestamp != null ? EvidenceTimestamp() : "MISSION_TIME_UNKNOWN";

        private bool CrossesClosedDoor(NavMeshPath path, out string label)
        {
            label = "";
            foreach (WorldObject obj in world.Objects.Values)
            {
                if (obj.Record.Kind != "DOOR" || obj.IsOpen) continue;
                bool alongX = obj.Record.Id != "STORAGE_DOOR";
                Vector3 center = obj.Transform.position;
                Vector3 from = transform.position;
                foreach (Vector3 to in path.corners)
                {
                    float a = alongX ? from.x - center.x : from.z - center.z;
                    float b = alongX ? to.x - center.x : to.z - center.z;
                    if (a * b < 0)
                    {
                        Vector3 crossing = Vector3.Lerp(from, to, a / (a - b));
                        float lateral = alongX ? Mathf.Abs(crossing.z - center.z) : Mathf.Abs(crossing.x - center.x);
                        if (lateral < 1.85f) { label = obj.Record.Label; return true; }
                    }
                    from = to;
                }
            }
            return false;
        }

        private void Interact(CommandAction action, WorldObject obj, List<string> restrictions)
        {
            if (action == CommandAction.INSPECT)
            {
                switch (obj.Record.Kind)
                {
                    case "CABINET":
                        bool usbInCabinet = world.Objects.TryGetValue("USB", out WorldObject cabinetUsb) &&
                            cabinetUsb.Record.LocationId == CurrentLocationId && !cabinetUsb.IsCollected && cabinetUsb.Record.IsAvailable;
                        if (usbInCabinet)
                        {
                            Collider cabinetBounds = obj.Transform.GetComponent<Collider>();
                            usbInCabinet = cabinetBounds != null ? cabinetBounds.bounds.Contains(cabinetUsb.Transform.position) :
                                Vector3.Distance(obj.Transform.position, cabinetUsb.Transform.position) < 1.8f;
                        }
                        if (usbInCabinet)
                        {
                            world.RevealUsb();
                            LastMessage = obj.IsOpen
                                ? "열린 캐비닛 안에서 기밀 USB를 확인했습니다. 회수할 수 있습니다."
                                : "캐비닛 틈에서 USB를 확인했습니다. 잠금 코드를 입력해야 회수할 수 있습니다. 최신 자료의 근거를 검증해 주세요.";
                        }
                        else LastMessage = obj.IsOpen
                            ? "캐비닛은 열려 있고 내부에 회수할 USB가 보이지 않습니다."
                            : "캐비닛은 잠겨 있습니다. 내부에 회수할 USB는 확인되지 않습니다.";
                        break;
                    case "TERMINAL": LastMessage = "증거 보관 터미널입니다. OPEN은 기록 영구 삭제(PURGE)를 실행합니다. 열면 핵심 증거가 파괴되어 임무가 실패합니다."; break;
                    case "DESK": LastMessage = InspectDesk(obj); break;
                    case "DOOR": LastMessage = obj.Record.Label + (obj.IsOpen ? "은 열려 있습니다." : "은 닫혀 있습니다. 어떤 문을 열지 명확히 지시해 주세요."); break;
                    case "USB": LastMessage = "기밀 데이터 USB입니다. 잠금 캐비닛을 열고 회수할 수 있습니다."; break;
                    default: LastMessage = obj.Record.Label + "을 확인했습니다."; break;
                }
                StoreFieldObservation(obj.Record.Kind == "CABINET" ? "FIELD_CABINET" : "FIELD_INSPECT_" + obj.Record.Id,
                    obj.Record.Label + " 조사", LastMessage);
                return;
            }
            if (action == CommandAction.PICKUP)
            {
                if (obj.Record.Kind != "USB" && obj.Record.Kind != "KEYCARD")
                { Fail("이 물체는 가지고 이동할 수 없습니다."); return; }
                obj.Collect();
                LastMessage = obj.Record.Label + "을 회수했습니다.";
                if (obj.Record.Kind == "USB") { HasUsb = true; OnUsbAcquired?.Invoke(); LastMessage += " 출입구로 돌아가 탈출해 주세요."; }
                return;
            }
            if (action == CommandAction.OPEN)
            {
                if (obj.Record.Kind == "TERMINAL")
                {
                    LastMessage = "터미널의 PURGE가 실행되어 핵심 증거가 파괴되었습니다.";
                    obj.Record.IsAvailable = false;
                    OnEvidenceDestroyed?.Invoke();
                    return;
                }
                if (obj.Record.Kind != "DOOR" && obj.Record.Kind != "CABINET")
                { Fail("이 물체는 열 수 없습니다."); return; }
                if (obj.IsOpen) { LastMessage = obj.Record.Label + "은 이미 열려 있습니다."; return; }
                string code = "";
                foreach (string restriction in restrictions)
                    if (restriction != null && restriction.StartsWith("LOCK_CODE:", StringComparison.Ordinal)) code = restriction.Substring("LOCK_CODE:".Length);
                if (!obj.Unlock(code))
                { OnRisk?.Invoke(6); Fail("캐비닛의 잠금 코드가 맞지 않습니다. 분석 결과의 근거와 최신 자료를 확인해 주세요."); return; }
                LastMessage = obj.Record.Label + "을 열었습니다.";
                if (obj.Record.Kind == "CABINET") LastMessage += " 기밀 USB를 확인했습니다.";
                OnRisk?.Invoke(obj.Record.Id == "STORAGE_DOOR" ? 12 : 2);
            }
        }

        private string LocalReport()
        {
            var labels = new List<string>();
            foreach (WorldObject obj in world.Objects.Values)
            {
                if (obj.Record.LocationId != CurrentLocationId || !obj.Record.IsKnown || !obj.Record.IsAvailable || obj.IsCollected) continue;
                string status = "";
                if (obj.Record.Kind == "DOOR") status = obj.IsOpen ? " (열림)" : " (닫힘)";
                else if (obj.Record.Kind == "CABINET") status = obj.IsOpen ? " (열림)" : " (잠김)";
                else if (!obj.Transform.gameObject.activeInHierarchy) status = " (현장 확인됨, 잠긴 보관함 안)";
                labels.Add(obj.Record.Label + status);
            }
            string nearby = labels.Count > 0 ? string.Join(", ", labels) : "확인된 상호작용 물체 없음";
            bool guardVisible = GuardVisibleLocally();
            return LocationLabel(CurrentLocationId) + ": " + nearby + ". " +
                (HasUsb ? "기밀 USB를 휴대 중입니다. " : "") +
                (guardVisible ? "현재 시야에 경비가 보입니다." : "현재 시야에서 경비는 보이지 않습니다.");
        }

        private string InspectDesk(WorldObject desk)
        {
            var found = new List<string>();
            Renderer deskRenderer = desk.Transform.GetComponent<Renderer>();
            foreach (WorldObject item in world.Objects.Values)
            {
                if (item.Record.LocationId != CurrentLocationId || item.IsCollected || !item.Record.IsAvailable ||
                    !item.Transform.gameObject.activeInHierarchy ||
                    (item.Record.Kind != "USB" && item.Record.Kind != "KEYCARD" && item.Record.Kind != "STORAGE_DEVICE")) continue;
                Vector3 position = item.Transform.position;
                bool onDesk;
                if (deskRenderer != null)
                {
                    Bounds bounds = deskRenderer.bounds;
                    onDesk = position.x >= bounds.min.x - .1f && position.x <= bounds.max.x + .1f &&
                        position.z >= bounds.min.z - .1f && position.z <= bounds.max.z + .1f &&
                        position.y >= bounds.max.y - .15f && position.y <= bounds.max.y + .6f;
                }
                else onDesk = Vector3.Distance(position, desk.Transform.position) < 1.8f && position.y >= desk.Transform.position.y;
                if (!onDesk) continue;
                item.Record.IsKnown = true;
                found.Add(item.Record.Label);
            }
            return desk.Record.Label + "을 확인했습니다. " +
                (found.Count > 0 ? "책상 위에서 " + string.Join(", ", found) + "을 확인했습니다." : "책상 위에 현재 회수 가능한 물체는 보이지 않습니다.");
        }

        private bool GuardVisibleLocally()
        {
            if (world.Guard == null || world.LocationAt(world.Guard.transform.position) != CurrentLocationId) return false;
            Vector3 offset = world.Guard.transform.position - transform.position;
            offset.y = 0;
            if (offset.magnitude > 8 || (offset.sqrMagnitude > .01f && Vector3.Angle(transform.forward, offset) > 65)) return false;
            Vector3 origin = transform.position + Vector3.up * 1.3f;
            Vector3 target = world.Guard.transform.position + Vector3.up * 1.3f;
            Physics.SyncTransforms();
            return !Physics.Raycast(origin, (target - origin).normalized, Vector3.Distance(origin, target),
                1 << MissionWorld.ObstacleLayer, QueryTriggerInteraction.Ignore);
        }

        private void StoreFieldObservation(string id, string title, string content)
        {
            OnEvidence?.Invoke(new EvidenceRecord
            {
                id = id, title = title, content = content, sourceId = "FIELD_AGENT_REPORT", timestamp = Timestamp(),
                locationId = CurrentLocationId, reliability = Reliability.CONFIRMED
            });
        }

        private static string LocationLabel(string id)
        {
            switch (id) { case "ENTRANCE": case "EXIT": return "출입구"; case "MAIN_HALL": return "메인 복도";
                case "LABORATORY": return "연구실"; case "STORAGE": return "창고"; case "SERVER_ROOM": return "서버실"; default: return id; }
        }

        private bool Allowed() => CanAct == null || CanAct();
        private bool Paused() => navigationPause.IsPaused || (IsPaused != null && IsPaused());

        public void SetPaused(bool paused) => navigationPause.SetPaused(navigation, paused);
        private bool Continue(int version)
        {
            if (version == cancellationVersion && State != AgentState.Caught && (Allowed() || Paused())) return true;
            Fail(State == AgentState.Caught ? "경비에게 체포되었습니다." : "임무 상태 변경으로 명령이 중단되었습니다.");
            return false;
        }
        private void Fail(string message) { LastSuccess = false; LastMessage = message; }

        public void Cancel()
        {
            cancellationVersion++;
            navigationPause.KeepStoppedOnResume();
            DisposePhoto();
            if (navigation != null && navigation.isOnNavMesh) { navigation.isStopped = true; navigation.ResetPath(); }
            IsBusy = false;
            LastSuccess = false;
            if (State != AgentState.Caught) State = AgentState.Idle;
        }

        public void Catch()
        {
            Cancel();
            State = AgentState.Caught;
            LastMessage = "경비에게 발각되어 체포되었습니다.";
        }

        private void OnDisable() => Cancel();

        private void Update()
        {
            if (Paused())
            {
                navigationPause.SetPaused(navigation, true);
                return;
            }
            if (Visual == null) return;
            if (characterPose == null) characterPose = new ProceduralCharacterPose(Visual);
            if (lastPoseState != State) { statePoseTime = 0; lastPoseState = State; }
            statePoseTime += Time.deltaTime;
            float speed = navigation != null && navigation.enabled && navigation.isOnNavMesh ? navigation.velocity.magnitude : 0;
            bool running = speed > 4.2f;
            poseTime += Time.deltaTime * (running ? 15f : speed > .12f ? 9f : 2f);
            float cycle = Mathf.Sin(poseTime);
            float bob = 0, height = 1;
            Vector3 bodyAngles = Vector3.zero, leftArm = Vector3.zero, rightArm = Vector3.zero,
                leftLeg = Vector3.zero, rightLeg = Vector3.zero;
            if (State == AgentState.Caught)
            {
                CurrentVisualPose = "Caught";
                height = .87f;
                bodyAngles = new Vector3(-4, 0, 0);
                leftArm = new Vector3(-135, 0, -22);
                rightArm = new Vector3(-135, 0, 22);
                leftLeg = rightLeg = new Vector3(12, 0, 0);
            }
            else if (State == AgentState.Hidden)
            {
                CurrentVisualPose = "Hide";
                height = .7f;
                bodyAngles = new Vector3(7, 0, 0);
                leftLeg = new Vector3(22, 0, -7);
                rightLeg = new Vector3(22, 0, 7);
                leftArm = new Vector3(-44, 0, -12);
                rightArm = new Vector3(-44, 0, 12);
            }
            else if (State == AgentState.Moving && speed > .12f)
            {
                CurrentVisualPose = running ? "Run" : "Walk";
                float amplitude = (running ? 38f : 24f) * Mathf.Clamp01(speed / 2.2f);
                leftLeg.x = cycle * amplitude;
                rightLeg.x = -cycle * amplitude;
                leftArm.x = -cycle * (running ? 34 : 20);
                rightArm.x = cycle * (running ? 34 : 20);
                leftArm.z = -5; rightArm.z = 5;
                bodyAngles.x = running ? 7 : 2;
                bob = Mathf.Abs(cycle) * (running ? .055f : .028f);
            }
            else if (State == AgentState.Interacting)
            {
                float reach = Mathf.Sin(statePoseTime * 5f);
                if (activePoseAction == CommandAction.PICKUP)
                {
                    CurrentVisualPose = "Pickup";
                    height = .88f;
                    bodyAngles.x = 17;
                    leftLeg.x = rightLeg.x = 13;
                    rightArm = new Vector3(-77 + reach * 9, -8, 12);
                    leftArm = new Vector3(-18, 0, -9);
                }
                else if (activePoseAction == CommandAction.OPEN)
                {
                    CurrentVisualPose = "Open";
                    bodyAngles = new Vector3(4, reach * 5, 0);
                    rightArm = new Vector3(-64, 15 + reach * 17, 12);
                    leftArm = new Vector3(-12, 0, -8);
                }
                else
                {
                    CurrentVisualPose = "Inspect";
                    bodyAngles.x = 9;
                    leftArm = new Vector3(-35, 0, -12);
                    rightArm = new Vector3(-46 + reach * 5, -12, 9);
                }
            }
            else if (State == AgentState.Reporting)
            {
                CurrentVisualPose = activePoseAction == CommandAction.PHOTO ? "Photo" : "Report";
                if (activePoseAction == CommandAction.PHOTO)
                { leftArm = new Vector3(-94, -10, -10); rightArm = new Vector3(-94, 10, 10); }
                else rightArm = new Vector3(-126, -14, 20);
            }
            else
            {
                CurrentVisualPose = State == AgentState.Waiting ? "Wait" : "Idle";
                bob = cycle * .008f;
                leftArm.z = -3 + cycle * 1.5f;
                rightArm.z = 3 - cycle * 1.5f;
            }
            float blend = 1f - Mathf.Exp(-12f * Time.deltaTime);
            characterPose.Apply(Vector3.up * bob, bodyAngles, height, leftArm, rightArm, leftLeg, rightLeg, blend);
        }
    }
}
