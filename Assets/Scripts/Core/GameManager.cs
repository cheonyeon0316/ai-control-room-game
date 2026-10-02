using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ControlRoom
{
    public sealed class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }
        public MissionRules Rules { get; private set; }
        public MissionWorld World { get; private set; }
        public CommandManager Commands { get; private set; }
        public AnalysisService Analysis { get; private set; }
        public EvidenceDatabase Evidence { get; private set; }
        public CommandJournal Journal { get; private set; }
        public UIManager Hud { get; private set; }
        public LlmCommandInterpreter Interpreter { get; private set; }
        public MissionManager Mission { get; private set; }
        public TimeManager Clock { get; private set; }
        public RiskManager Risk { get; private set; }
        public bool CanAct => Rules != null && Rules.State == MissionState.Playing && !Rules.Paused;
        public DateTime MissionTimestamp => new DateTime(2026, 10, 2, 23, 5, 0).AddSeconds(Rules.ElapsedSeconds);
        private MissionState previousState;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
#if UNITY_WEBGL && !UNITY_EDITOR
            WebGLInput.captureAllKeyboardInput = false;
#endif
            Application.targetFrameRate = 60;
            Rules = new MissionRules();
            Analysis = new AnalysisService();
            Evidence = new EvidenceDatabase();
            Journal = new CommandJournal();
            var display = new GameObject("Control room display camera", typeof(Camera), typeof(AudioListener));
            display.transform.SetParent(transform);
            var displayCamera = display.GetComponent<Camera>();
            displayCamera.cullingMask = 0;
            displayCamera.clearFlags = CameraClearFlags.SolidColor;
            displayCamera.backgroundColor = new Color(.025f, .037f, .048f);
            displayCamera.depth = 10;
            Mission = gameObject.AddComponent<MissionManager>(); Mission.Initialize(Rules);
            Clock = gameObject.AddComponent<TimeManager>(); Clock.Initialize(Rules);
            Risk = gameObject.AddComponent<RiskManager>(); Risk.Initialize(Rules);
            var worldRoot = new GameObject("MissionManager / Facility");
            worldRoot.transform.SetParent(transform);
            World = worldRoot.AddComponent<MissionWorld>();
            World.Build();
            World.Agent.CanAct = () => CanAct;
            World.Agent.IsPaused = () => Rules.Paused;
            World.Agent.EvidenceTimestamp = () => MissionTimestamp.ToString("yyyy-MM-dd HH:mm:ss");
            World.Agent.OnUsbAcquired = () => Rules.AcquireUsb();
            World.Agent.OnExitReached = () => Rules.ReachExit();
            World.Agent.OnEvidenceDestroyed = () => Rules.DestroyCriticalEvidence();
            World.Agent.OnRisk = amount => Risk.Add(amount);
            World.Agent.OnEvidence = record => Evidence.AddRange(new[] { record });
            World.Guard.CanAct = () => CanAct;
            World.Guard.OnCaught = () => { World.Agent.Catch(); Rules.Caught(); };
            gameObject.AddComponent<AgentManager>().Initialize(World.Agent);
            gameObject.AddComponent<AnalysisManager>().Initialize(Analysis);
            gameObject.AddComponent<EvidenceManager>().Initialize(Evidence);
            Interpreter = gameObject.AddComponent<LlmCommandInterpreter>();
            Commands = gameObject.AddComponent<CommandManager>(); Commands.Initialize(this);
            Hud = gameObject.AddComponent<UIManager>(); Hud.Build(this);
            previousState = Rules.State;
            if (Application.isBatchMode)
                Debug.Log("Control room ready: cameras=" + World.Cctv.Cameras.Count + ", navigation=" +
                    (World.NavigationReady && World.Agent.Navigation.isOnNavMesh && World.Guard.Navigation.isOnNavMesh) + ", state=" + Rules.State);
        }

        private void Update()
        {
            if (Rules == null) return;
            Clock.Tick(Time.deltaTime);
            World.Guard.Risk = Rules.Risk;
            if (Rules.State != previousState)
            {
                previousState = Rules.State;
                if (Rules.State == MissionState.Success || Rules.State == MissionState.Failure)
                {
                    if (Rules.State == MissionState.Failure)
                    {
                        World.Agent.Cancel();
                        Commands.CancelActive();
                    }
                    Hud.ShowOutcome();
                }
            }
        }

        public void StartMission() { Mission.StartMission(); Hud.HideBriefing(); }
        // Native browser input preserves Korean IME composition before dispatching.
        public void BeginWebMission()
        {
            if (Rules.State == MissionState.Briefing) StartMission();
        }
        public void SubmitWebCommand(string payload)
        {
            if (!CanAct) return;
            var request = JsonUtility.FromJson<WebCommandRequest>(payload);
            if (request == null || string.IsNullOrWhiteSpace(request.prompt)) return;
            Hud.TargetDropdown.value = request.target == 1 ? 1 : 0;
            Hud.CommandInput.text = request.prompt;
            Hud.SendButton.onClick.Invoke();
        }
        [Serializable]
        private sealed class WebCommandRequest
        {
            public int target;
            public string prompt;
        }
        public void TogglePause()
        {
            Rules.SetPaused(!Rules.Paused);
            World.Agent.SetPaused(Rules.Paused);
            World.Guard.SetPaused(Rules.Paused);
        }
        public void OpenResult()
        {
            if (Rules.State != MissionState.Success && Rules.State != MissionState.Failure) return;
            Rules.ShowResult();
            Hud.ShowResult();
            if (Application.CanStreamedLevelBeLoaded("Result")) SceneManager.LoadScene("Result");
        }
        public void Restart()
        {
            World.Agent.Cancel();
            Commands.CancelActive();
            World.ReleaseNavigation();
            gameObject.SetActive(false);
            Instance = null;
            Destroy(gameObject);
            SceneManager.LoadScene("Mission_01");
        }
        private void OnDestroy() { if (Instance == this) Instance = null; }
    }
}
