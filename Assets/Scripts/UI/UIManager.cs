using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ControlRoom
{
    public sealed class UIManager : MonoBehaviour
    {
        public InputField CommandInput { get; private set; }
        public Dropdown TargetDropdown { get; private set; }
        public Button SendButton { get; private set; }
        public Button StartButton { get; private set; }
        public Button ResultButton { get; private set; }
        public Button RestartButton { get; private set; }
        public Button InterpreterButton { get; private set; }
        public Button PhotoButton { get; private set; }
        public Button PhotoCloseButton { get; private set; }
        public bool PhotoVisible => photo != null && photo.gameObject.activeSelf;
        public List<RawImage> CameraImages { get; private set; } = new List<RawImage>();
        public RawImage ZoomImage { get; private set; }
        public Canvas Canvas { get; private set; }
        public int SelectedCamera { get; private set; } = -1;
        public bool ZoomVisible => zoom != null && zoom.gameObject.activeSelf;

        private GameManager game;
        private Font font;
        private bool ownsFont;
        private RectTransform screen, briefing, outcome, result, zoom, photo;
        private RawImage photoImage;
        private Text photoCaption;
        private Texture2D loadedPhoto;
        private Text time, risk, agent, objectives, liveResponse, sectionTitle, sourceText, resultText, resultHeading, zoomTitle;
        private readonly List<Text> cameraTimes = new List<Text>();
        private readonly List<Image> cameraBorders = new List<Image>();
        private readonly List<string> log = new List<string>();
        private readonly List<RawImage> scanlines = new List<RawImage>();
        private AudioSource audioSource;
        private readonly Dictionary<CommandStatus, AudioClip> radioCues = new Dictionary<CommandStatus, AudioClip>();
        private Texture2D lineTexture;
        private Material feedMaterial;
        private bool viewingEvidence, viewingHelp, success;
        private float nextPaint;
        private readonly Color bg = new Color(.025f, .037f, .048f);
        private readonly Color panel = new Color(.053f, .073f, .085f);
        private readonly Color border = new Color(.14f, .20f, .22f);
        private readonly Color ink = new Color(.78f, .84f, .85f);
        private readonly Color dim = new Color(.44f, .55f, .59f);
        private readonly Color mint = new Color(.40f, .87f, .76f);
        private readonly Color amber = new Color(.97f, .70f, .32f);

        public void Build(GameManager owner)
        {
            game = owner;
            font = Resources.Load<Font>("Fonts/NotoSansCJKkr-Regular");
#if !UNITY_WEBGL || UNITY_EDITOR
            if (font == null)
            {
                font = Font.CreateDynamicFontFromOSFont(new[] { "Malgun Gothic", "맑은 고딕", "Noto Sans CJK KR", "Arial" }, 24);
                ownsFont = font != null;
            }
#endif
            if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var canvasObject = new GameObject("Control Room HUD", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(transform);
            Canvas = canvasObject.GetComponent<Canvas>(); Canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            Canvas.sortingOrder = 10;
            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = .5f;
            screen = canvasObject.GetComponent<RectTransform>();
            if (FindFirstObjectByType<EventSystem>() == null)
            {
                var events = new GameObject("Command Input Events", typeof(EventSystem), typeof(StandaloneInputModule));
                events.transform.SetParent(transform);
            }
            Input.imeCompositionMode = IMECompositionMode.On;
            var basePanel = Panel(screen, "Console", 0, 0, 1920, 1080, bg);
            BuildHeader(basePanel); BuildCameras(basePanel); BuildSidebar(basePanel); BuildCommand(basePanel);
            BuildBriefing(basePanel); BuildOutcome(basePanel); BuildResult(basePanel);
            BuildPhoto(basePanel);
            audioSource = gameObject.AddComponent<AudioSource>(); audioSource.playOnAwake = false;
            radioCues[CommandStatus.RECEIVED] = MakeTone(660, .035f);
            radioCues[CommandStatus.PROCESSING] = MakeTone(720, .035f);
            radioCues[CommandStatus.EXECUTING] = MakeTone(540, .045f);
            radioCues[CommandStatus.COMPLETED] = MakeTone(880, .075f);
            radioCues[CommandStatus.NEEDS_CLARIFICATION] = MakeTone(440, .09f);
            radioCues[CommandStatus.FAILED] = MakeTone(260, .11f);
            game.Commands.OnResponse += ShowResponse;
            game.Commands.OnHistory += line => { AddLog(line); };
            RefreshSidebar();
        }

        private void BuildHeader(RectTransform parent)
        {
            Label(parent, "OPS / 01", 32, 24, 260, 30, 19, mint);
            Label(parent, "DATA LEAK", 32, 52, 590, 65, 45, ink, FontStyle.Bold);
            Label(parent, "RESEARCH FACILITY  /  REMOTE OPERATIONS", 640, 64, 650, 32, 16, dim);
            time = Label(parent, "10:00", 1320, 38, 245, 62, 44, ink, FontStyle.Bold);
            time.verticalOverflow = VerticalWrapMode.Overflow;
            risk = Label(parent, "RISK · LOW", 1585, 44, 225, 48, 18, mint);
            Button(parent, "Ⅱ", 1832, 43, 56, 44, () => game.TogglePause(), panel, ink);
            Panel(parent, "Header divider", 32, 126, 1856, 2, border);
            Label(parent, "LIVE SURVEILLANCE  /  카메라를 선택하면 확대됩니다", 32, 138, 1250, 28, 15, dim);
        }

        private void BuildCameras(RectTransform parent)
        {
            var feedShader = Resources.Load<Shader>("Shaders/CctvFeed");
            if (feedShader != null && feedShader.isSupported)
                feedMaterial = new Material(feedShader) { name = "CCTV display effects" };
            lineTexture = new Texture2D(4, 4, TextureFormat.RGBA32, false);
            var pixels = new Color[16];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = new Color(0, 0, 0, i < 4 ? .15f : 0);
            lineTexture.SetPixels(pixels); lineTexture.Apply(); lineTexture.wrapMode = TextureWrapMode.Repeat; lineTexture.filterMode = FilterMode.Point;
            for (int i = 0; i < 4; i++)
            {
                int index = i;
                float x = 32 + (i % 2) * 636, y = 177 + (i / 2) * 327;
                var frame = Panel(parent, "Camera frame " + i, x, y, 620, 310, border);
                cameraBorders.Add(frame.GetComponent<Image>());
                var feed = Image(frame, "Live " + i, 2, 2, 616, 306, game.World.Cctv.Textures[i]);
                if (feedMaterial != null) feed.material = feedMaterial;
                CameraImages.Add(feed);
                var click = feed.gameObject.AddComponent<Button>();
                click.onClick.AddListener(() => SelectCamera(index));
                var lines = Image(frame, "Scanlines", 2, 2, 616, 306, lineTexture);
                lines.raycastTarget = false; lines.uvRect = new Rect(0, 0, 154, 76.5f); scanlines.Add(lines);
                Panel(frame, "Feed header", 2, 2, 616, 36, new Color(.025f, .037f, .048f, .83f)).GetComponent<Image>().raycastTarget = false;
                Label(frame, game.World.Cctv.Ids[i] + " / " + game.World.Cctv.Names[i].ToUpperInvariant(), 14, 8, 440, 25, 15, ink);
                Label(frame, "● LIVE", 520, 9, 87, 23, 13, mint);
                cameraTimes.Add(Label(frame, "23:00:00  |  SIGNAL 98%", 14, 277, 520, 24, 13, ink));
            }
            zoom = Panel(parent, "Selected camera", 32, 177, 1256, 637, border);
            ZoomImage = Image(zoom, "Enlarged feed", 2, 2, 1252, 633, game.World.Cctv.Textures[0]);
            if (feedMaterial != null) ZoomImage.material = feedMaterial;
            var zoomLines = Image(zoom, "Zoom scanlines", 2, 2, 1252, 633, lineTexture);
            zoomLines.raycastTarget = false; zoomLines.uvRect = new Rect(0, 0, 313, 158);
            Panel(zoom, "Zoom header", 2, 2, 1252, 50, new Color(.025f, .037f, .048f, .85f));
            zoomTitle = Label(zoom, "CAM-01", 20, 12, 900, 30, 20, ink);
            Button(zoom, "4분할 보기 ×", 1080, 9, 156, 34, CloseZoom, panel, mint);
            zoom.gameObject.SetActive(false);
        }

        private void BuildSidebar(RectTransform parent)
        {
            var side = Panel(parent, "Operations sidebar", 1320, 177, 568, 637, panel);
            Label(side, "FIELD UNIT  /  ECHO-1", 22, 18, 520, 30, 16, mint);
            agent = Label(side, "ENTRANCE · Idle", 22, 52, 520, 42, 24, ink);
            objectives = Label(side, "목표: 기밀 USB 회수 후 출입구로 탈출\n시설 봉쇄 전까지 10분", 22, 102, 520, 76, 18, ink);
            Panel(side, "Sidebar divider", 22, 192, 524, 1, border);
            Button(side, "작전 기록", 22, 212, 154, 40, () => { viewingEvidence = false; viewingHelp = false; RefreshSidebar(); }, border, ink);
            Button(side, "증거 자료", 187, 212, 154, 40, () => { viewingEvidence = true; viewingHelp = false; RefreshSidebar(); }, border, ink);
            Button(side, "지도 / 도움", 352, 212, 192, 40, () => { viewingHelp = true; viewingEvidence = false; RefreshSidebar(); }, border, ink);
            sectionTitle = Label(side, "COMMUNICATION LOG", 22, 273, 365, 27, 14, dim);
            PhotoButton = Button(side, "현장 사진", 394, 272, 150, 30, ShowLatestPhoto, border, ink);
            var scroll = MakeScroll(side, "Dispatch scroll", 22, 310, 524, 303);
            sourceText = scroll.text;
            AddLog("연결 완료. ECHO-1이 출입구에서 대기합니다.");
        }

        private void BuildCommand(RectTransform parent)
        {
            var command = Panel(parent, "Command terminal", 32, 846, 1856, 202, panel);
            Label(command, "DISPATCH", 22, 15, 220, 30, 15, mint);
            Label(command, "NATURAL LANGUAGE COMMAND", 260, 15, 1050, 30, 15, dim);
            InterpreterButton = Button(command, "MODE · OFFLINE", 1620, 13, 214, 30, ToggleInterpreter, border, ink);
            TargetDropdown = MakeDropdown(command, 22, 54, 218, 57);
            var inputRoot = Panel(command, "Command input", 260, 54, 1338, 57, bg);
            var text = Label(inputRoot, "", 18, 8, 1297, 43, 22, ink);
            var placeholder = Label(inputRoot, "지시를 입력하세요. 예: 메인 복도로 이동해", 18, 8, 1297, 43, 20, dim);
            CommandInput = inputRoot.gameObject.AddComponent<InputField>();
            CommandInput.textComponent = text; CommandInput.placeholder = placeholder;
            CommandInput.lineType = InputField.LineType.SingleLine;
            CommandInput.characterLimit = 800; CommandInput.caretColor = mint; CommandInput.customCaretColor = true;
            CommandInput.selectionColor = new Color(mint.r, mint.g, mint.b, .28f);
            CommandInput.onEndEdit.AddListener(_ => { if ((Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)) && string.IsNullOrEmpty(Input.compositionString)) SubmitCommand(); });
            SendButton = Button(command, "SEND  →", 1620, 54, 214, 57, SubmitCommand, mint, bg);
            liveResponse = Label(command, "대기 · 현장 상황을 관찰하고 담당자에게 명령하세요.", 260, 126, 1574, 53, 18, dim);
            Label(command, "ENTER TO SEND", 22, 133, 225, 35, 13, dim);
        }

        private void BuildBriefing(RectTransform parent)
        {
            briefing = Modal(parent, "Mission briefing");
            Label(briefing, "CLASSIFIED  /  OPERATION 01", 50, 40, 1200, 32, 17, mint);
            Label(briefing, "데이터 유출", 50, 96, 1230, 100, 58, ink, FontStyle.Bold);
            Label(briefing, "시설이 봉쇄되기까지 10분.\n기밀 데이터가 저장된 USB를 찾아 현장 요원과 함께 회수하십시오.", 50, 222, 1230, 110, 26, ink);
            Label(briefing, "ECHO-1은 출입구에서 대기 중입니다.\nCCTV와 현장 보고, 자료 분석을 통해 현재 상황을 파악하십시오.\n오래된 자료는 현장 상황과 다를 수 있습니다.", 50, 362, 1230, 142, 23, dim);
            Panel(briefing, "Briefing rule", 50, 540, 1230, 1, border);
            Label(briefing, "PRIMARY   USB 회수 + 출입구 탈출\nSECONDARY   유출에 관여한 인물 식별\nTHREAT   경비에게 발견되거나 제한시간이 끝나면 작전 실패", 50, 574, 1230, 134, 21, ink);
            StartButton = Button(briefing, "작전 시작  →", 948, 735, 332, 62, () => game.StartMission(), mint, bg);
            Label(briefing, "1 FIELD UNIT   /   4 CCTV   /   1 ANALYSIS SYSTEM", 50, 748, 850, 35, 15, dim);
        }

        private void BuildOutcome(RectTransform parent)
        {
            outcome = Modal(parent, "Mission outcome");
            Label(outcome, "OPERATION ENDED", 50, 48, 1230, 35, 18, dim);
            var heading = Label(outcome, "", 50, 152, 1230, 150, 57, ink, FontStyle.Bold);
            heading.gameObject.name = "Outcome heading";
            var detail = Label(outcome, "", 50, 352, 1230, 240, 26, ink);
            detail.gameObject.name = "Outcome detail";
            ResultButton = Button(outcome, "작전 결과 보기  →", 858, 735, 422, 62, () => game.OpenResult(), mint, bg);
            outcome.gameObject.SetActive(false);
        }

        private void BuildResult(RectTransform parent)
        {
            result = Modal(parent, "Mission debrief");
            Label(result, "DEBRIEF  /  COMMAND PROFILE", 50, 38, 1230, 30, 17, mint);
            resultHeading = Label(result, "작전 결과", 50, 98, 1230, 96, 48, ink, FontStyle.Bold);
            var scroll = MakeScroll(result, "Profile scroll", 50, 231, 1230, 458);
            resultText = scroll.text; resultText.fontSize = 23;
            RestartButton = Button(result, "새 작전 시작  ↻", 948, 735, 332, 62, () => game.Restart(), mint, bg);
            result.gameObject.SetActive(false);
        }

        private void BuildPhoto(RectTransform parent)
        {
            photo = Modal(parent, "Field photo viewer");
            photoCaption = Label(photo, "FIELD PHOTO", 50, 32, 1030, 60, 19, ink);
            photoImage = Image(photo, "Captured field image", 50, 112, 1230, 692, null);
            PhotoCloseButton = Button(photo, "닫기 ×", 1130, 30, 150, 40, () => photo.gameObject.SetActive(false), border, mint);
            photo.gameObject.SetActive(false);
        }

        private void ShowLatestPhoto()
        {
            EvidenceRecord latest = null;
            foreach (var record in game.Evidence.Entries)
                if (!string.IsNullOrEmpty(record.imagePath)) latest = record;
            if (latest == null || !File.Exists(latest.imagePath))
            {
                AddLog("확보한 현장 사진이 없습니다. 요원에게 사진 촬영을 지시하십시오.");
                return;
            }
            try
            {
                if (loadedPhoto != null) Destroy(loadedPhoto);
                loadedPhoto = new Texture2D(2, 2, TextureFormat.RGB24, false);
                if (!loadedPhoto.LoadImage(File.ReadAllBytes(latest.imagePath)))
                { AddLog("현장 사진을 불러오지 못했습니다."); return; }
                photoImage.texture = loadedPhoto;
                photoCaption.text = latest.title + " / " + latest.timestamp + "\n" + latest.sourceId;
                photo.gameObject.SetActive(true);
            }
            catch (IOException) { AddLog("현장 사진 파일을 읽을 수 없습니다."); }
            catch (UnauthorizedAccessException) { AddLog("현장 사진 파일에 접근할 수 없습니다."); }
        }

        public void SubmitCommand()
        {
            if (string.IsNullOrWhiteSpace(CommandInput.text)) return;
            string prompt = CommandInput.text;
            CommandInput.text = "";
            game.Commands.Submit(prompt, TargetDropdown.value == 0 ? CommandTarget.FIELD_AGENT : CommandTarget.ANALYSIS_SYSTEM);
            CommandInput.ActivateInputField();
        }
        private void ToggleInterpreter()
        {
            if (!game.Interpreter.IsConfigured)
            {
                AddLog("연결된 명령 해석 서비스가 없습니다. 오프라인 해석기를 사용합니다.");
                return;
            }
            if (game.Commands.IsProcessing) return;
            game.Commands.UseLlm = !game.Commands.UseLlm;
            InterpreterButton.GetComponentInChildren<Text>().text = game.Commands.UseLlm ? "MODE · LLM" : "MODE · OFFLINE";
            if (viewingHelp) RefreshSidebar();
        }
        public void SelectCamera(int index)
        {
            if (index < 0 || index >= CameraImages.Count) return;
            SelectedCamera = index; game.World.Cctv.Select(index);
            ZoomImage.texture = game.World.Cctv.Textures[index];
            zoomTitle.text = game.World.Cctv.Ids[index] + " / " + game.World.Cctv.Names[index].ToUpperInvariant();
            zoom.gameObject.SetActive(true);
            for (int i = 0; i < cameraBorders.Count; i++) cameraBorders[i].color = i == index ? mint : border;
        }
        public void CloseZoom() { zoom.gameObject.SetActive(false); }
        public void HideBriefing() { briefing.gameObject.SetActive(false); CommandInput.ActivateInputField(); }
        public void ShowOutcome()
        {
            success = game.Rules.State == MissionState.Success;
            outcome.Find("Outcome heading").GetComponent<Text>().text = success ? "MISSION COMPLETE" : "MISSION FAILED";
            outcome.Find("Outcome detail").GetComponent<Text>().text = success
                ? "USB를 확보하고 요원이 출입구로 복귀했습니다.\n남은 시간  " + FormatTime(game.Rules.RemainingSeconds)
                : "실패 원인  " + game.Rules.FailureReason + "\n경과 시간  " + FormatTime(game.Rules.ElapsedSeconds);
            outcome.gameObject.SetActive(true);
        }
        public void ShowResult()
        {
            outcome.gameObject.SetActive(false); briefing.gameObject.SetActive(false);
            success = game.Rules.Outcome == MissionState.Success;
            resultHeading.text = success ? "작전 성공 / Mission Complete" : "작전 실패 / Mission Failed";
            resultText.text = "남은 시간  " + FormatTime(game.Rules.RemainingSeconds) + "\n\n" + game.Journal.GetResultSummary(game.Rules);
            result.gameObject.SetActive(true);
        }

        private void Update()
        {
            if (game == null || time == null) return;
            time.text = FormatTime(game.Rules.RemainingSeconds) + (game.Rules.Paused ? "  Ⅱ" : "");
            time.color = game.Rules.RemainingSeconds < 60 ? amber : ink;
            risk.text = "RISK · " + game.Rules.Risk;
            risk.color = game.Rules.Risk == RiskLevel.LOW ? mint : amber;
            agent.text = TranslateLocation(game.World.Agent.CurrentLocationId) + " · " + game.World.Agent.State;
            objectives.text = (game.Rules.HasUsb ? "✓ USB 확보" : "□ 기밀 USB 회수") + "\n" + (game.Rules.State == MissionState.Success ? "✓ 요원 탈출 완료" : "□ 출입구로 요원 복귀");
            SendButton.interactable = game.CanAct;
            TargetDropdown.interactable = game.CanAct;
            CommandInput.interactable = game.CanAct;
            if (Time.unscaledTime >= nextPaint)
            {
                nextPaint = Time.unscaledTime + .25f;
                foreach (var stamp in cameraTimes) stamp.text = game.MissionTimestamp.ToString("HH:mm:ss") + "  |  SIGNAL 98%";
                // Verification can update an existing source without changing entry count.
                if (viewingEvidence) RefreshSidebar();
            }
        }

        private void ShowResponse(CommandStatus status, string message)
        {
            string preview = message.Replace('\n', ' ');
            if (preview.Length > 135) preview = preview.Substring(0, 135) + "…";
            liveResponse.text = status + " · " + preview;
            liveResponse.color = status == CommandStatus.FAILED || status == CommandStatus.NEEDS_CLARIFICATION ? amber : mint;
            AddLog("[" + game.MissionTimestamp.ToString("HH:mm:ss") + "] " + status + "\n" + message);
            if (audioSource != null && radioCues.TryGetValue(status, out AudioClip cue))
                audioSource.PlayOneShot(cue, status == CommandStatus.RECEIVED || status == CommandStatus.PROCESSING || status == CommandStatus.EXECUTING ? .035f : .17f);
        }
        private void AddLog(string line)
        {
            log.Add(line); if (log.Count > 80) log.RemoveAt(0);
            if (!viewingEvidence && !viewingHelp) RefreshSidebar();
        }
        private void RefreshSidebar()
        {
            if (sourceText == null) return;
            if (viewingHelp)
            {
                sectionTitle.text = "FACILITY / COMMAND REFERENCE";
                sourceText.text = "출입구 → 메인 복도 → 연구실\n                    ↓\n                 창고 → 서버실\n\n현장 요원\n이동 / 대기 / 조사 / 획득 / 열기\n숨기 / 사진 / 주변 보고\n부정된 이동과 미지원 조건은 실행하지 않습니다.\n터미널 OPEN은 안내 화면만 엽니다.\nPURGE 삭제는 '삭제 확정'을 한 번 더 입력해야 합니다.\n예: 메인 복도로 이동해\n예: 주변 상황을 보고해\n예: 5초 동안 대기해\n\n분석 시스템\n출입 기록 / 문서 / 근거 / 충돌 자료\n예: 22시 이후 연구실 출입자 찾아줘\n예: USB 위치를 알려줘\n예: 이 정보의 근거를 보여줘\n\n분석 결과는 작전 기록과 증거 자료에서 확인할 수 있습니다.\n\n현재 해석기: " + (game.Commands.UseLlm ? "외부 LLM" : "오프라인 규칙 해석기");
                return;
            }
            if (viewingEvidence)
            {
                sectionTitle.text = "EVIDENCE DATABASE  /  " + game.Evidence.Entries.Count;
                var text = new StringBuilder();
                foreach (var record in game.Evidence.Entries)
                {
                    text.AppendLine(record.title + "  [" + record.reliability + "]");
                    text.AppendLine(record.content);
                    text.AppendLine("SOURCE " + record.sourceId + "\n" + record.timestamp);
                    text.AppendLine();
                }
                sourceText.text = text.Length > 0 ? text.ToString() : "아직 확보한 증거가 없습니다. 현장 조사 또는 자료 분석으로 확인하십시오.";
                return;
            }
            sectionTitle.text = "COMMUNICATION LOG";
            var buffer = new StringBuilder();
            for (int i = log.Count - 1; i >= 0; i--) buffer.Append(log[i]).Append("\n\n");
            sourceText.text = buffer.ToString();
        }

        private RectTransform Modal(RectTransform parent, string name)
        {
            var shade = Panel(parent, name + " shade", 0, 0, 1920, 1080, new Color(0, 0, 0, .80f));
            var body = Panel(shade, name, 295, 100, 1330, 860, panel);
            Panel(body, "Accent", 0, 0, 5, 860, mint);
            // Returning the body is sufficient: toggling its shade hides the input-blocking backdrop too.
            var group = shade.gameObject.AddComponent<ModalVisibility>(); group.Body = body;
            body.gameObject.AddComponent<ModalBody>().Shade = shade.gameObject;
            return body;
        }
        private RectTransform Panel(Transform parent, string name, float x, float y, float w, float h, Color color)
        {
            var obj = new GameObject(name, typeof(RectTransform), typeof(Image)); obj.transform.SetParent(parent, false);
            var rect = obj.GetComponent<RectTransform>(); Place(rect, x, y, w, h);
            obj.GetComponent<Image>().color = color; return rect;
        }
        private RawImage Image(Transform parent, string name, float x, float y, float w, float h, Texture texture)
        {
            var obj = new GameObject(name, typeof(RectTransform), typeof(RawImage)); obj.transform.SetParent(parent, false);
            Place(obj.GetComponent<RectTransform>(), x, y, w, h);
            var raw = obj.GetComponent<RawImage>(); raw.texture = texture; return raw;
        }
        private Text Label(Transform parent, string text, float x, float y, float w, float h, int size, Color color, FontStyle style = FontStyle.Normal)
        {
            var obj = new GameObject("Text", typeof(RectTransform), typeof(Text)); obj.transform.SetParent(parent, false);
            Place(obj.GetComponent<RectTransform>(), x, y, w, h);
            var label = obj.GetComponent<Text>(); label.text = text; label.font = font; label.fontSize = size;
            label.color = color; label.fontStyle = style; label.raycastTarget = false;
            label.horizontalOverflow = HorizontalWrapMode.Wrap; label.verticalOverflow = VerticalWrapMode.Truncate;
            return label;
        }
        private Button Button(Transform parent, string text, float x, float y, float w, float h, Action action, Color background, Color foreground)
        {
            var root = Panel(parent, text, x, y, w, h, background);
            var button = root.gameObject.AddComponent<Button>();
            var colors = button.colors; colors.highlightedColor = new Color(.85f, .95f, .94f); colors.pressedColor = new Color(.65f, .76f, .74f); button.colors = colors;
            var label = Label(root, text, 4, 2, w - 8, h - 4, 18, foreground); label.alignment = TextAnchor.MiddleCenter;
            button.onClick.AddListener(() => action()); return button;
        }
        private (ScrollRect scroll, Text text) MakeScroll(Transform parent, string name, float x, float y, float w, float h)
        {
            var root = Panel(parent, name, x, y, w, h, new Color(0, 0, 0, 0));
            var scroll = root.gameObject.AddComponent<ScrollRect>(); scroll.horizontal = false; scroll.movementType = ScrollRect.MovementType.Clamped;
            var viewport = Panel(root, "Viewport", 0, 0, w, h, new Color(0, 0, 0, 0));
            viewport.gameObject.AddComponent<RectMask2D>();
            var content = new GameObject("Content", typeof(RectTransform)); content.transform.SetParent(viewport, false);
            var contentRect = content.GetComponent<RectTransform>(); Place(contentRect, 0, 0, w - 10, h);
            var label = content.AddComponent<Text>(); label.font = font; label.fontSize = 18; label.color = ink;
            label.horizontalOverflow = HorizontalWrapMode.Wrap; label.verticalOverflow = VerticalWrapMode.Overflow;
            label.raycastTarget = false; label.lineSpacing = 1.13f;
            var fitter = content.AddComponent<ContentSizeFitter>(); fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.viewport = viewport; scroll.content = contentRect; scroll.scrollSensitivity = 34;
            return (scroll, label);
        }
        private Dropdown MakeDropdown(Transform parent, float x, float y, float w, float h)
        {
            var root = Panel(parent, "Dispatch target", x, y, w, h, border);
            var dropdown = root.gameObject.AddComponent<Dropdown>();
            var caption = Label(root, "FIELD AGENT", 12, 10, w - 35, h - 20, 17, ink); caption.alignment = TextAnchor.MiddleLeft;
            Label(root, "⌄", w - 28, 13, 23, 26, 19, mint);
            var template = Panel(root, "Template", 0, -108, w, 100, border);
            var scroll = template.gameObject.AddComponent<ScrollRect>(); scroll.horizontal = false;
            var viewport = Panel(template, "Viewport", 0, 0, w, 100, panel); viewport.gameObject.AddComponent<RectMask2D>();
            var content = Panel(viewport, "Content", 0, 0, w, 48, panel);
            var item = Panel(content, "Item", 0, 0, w, 48, panel);
            var toggle = item.gameObject.AddComponent<Toggle>(); toggle.targetGraphic = item.GetComponent<Image>();
            var check = Panel(item, "Check", 8, 20, 7, 7, mint).GetComponent<Image>(); toggle.graphic = check;
            var itemLabel = Label(item, "FIELD AGENT", 24, 8, w - 32, 32, 16, ink); itemLabel.alignment = TextAnchor.MiddleLeft;
            scroll.content = content; scroll.viewport = viewport;
            dropdown.template = template; dropdown.captionText = caption; dropdown.itemText = itemLabel;
            dropdown.options = new List<Dropdown.OptionData> { new Dropdown.OptionData("FIELD AGENT"), new Dropdown.OptionData("ANALYSIS SYSTEM") };
            dropdown.RefreshShownValue(); template.gameObject.SetActive(false);
            return dropdown;
        }
        private static void Place(RectTransform rect, float x, float y, float w, float h)
        { rect.anchorMin = rect.anchorMax = new Vector2(0, 1); rect.pivot = new Vector2(0, 1); rect.anchoredPosition = new Vector2(x, -y); rect.sizeDelta = new Vector2(w, h); }
        public static string FormatTime(float seconds) { int total = Mathf.CeilToInt(Mathf.Max(0, seconds)); return (total / 60).ToString("00") + ":" + (total % 60).ToString("00"); }
        public static string TranslateLocation(string id)
        { switch (id) { case "ENTRANCE": case "EXIT": return "출입구"; case "MAIN_HALL": return "메인 복도"; case "LABORATORY": return "연구실"; case "STORAGE": return "창고"; case "SERVER_ROOM": return "서버실"; default: return id; } }
        private static AudioClip MakeTone(float frequency, float duration)
        {
            int count = Mathf.Max(2, Mathf.RoundToInt(44100 * duration)); var data = new float[count];
            for (int i = 0; i < count; i++) data[i] = Mathf.Sin(2 * Mathf.PI * frequency * i / 44100f) * Mathf.Sin(Mathf.PI * i / count);
            var clip = AudioClip.Create("Radio cue", count, 1, 44100, false); clip.SetData(data, 0); return clip;
        }
        private void OnDestroy()
        {
            if (game != null && game.Commands != null) game.Commands.OnResponse -= ShowResponse;
            if (lineTexture != null) Destroy(lineTexture);
            if (feedMaterial != null) Destroy(feedMaterial);
            if (loadedPhoto != null) Destroy(loadedPhoto);
            if (ownsFont && font != null) Destroy(font);
            foreach (var cue in radioCues.Values) if (cue != null) Destroy(cue);
        }
    }

    // Keep modal backdrops synchronized when the public panel is toggled by the HUD.
    public sealed class ModalVisibility : MonoBehaviour { public RectTransform Body; }
    public sealed class ModalBody : MonoBehaviour
    {
        public GameObject Shade;
        private void OnEnable() { if (Shade != null) Shade.GetComponent<Image>().enabled = true; }
        private void OnDisable() { if (Shade != null) Shade.GetComponent<Image>().enabled = false; }
    }
}
