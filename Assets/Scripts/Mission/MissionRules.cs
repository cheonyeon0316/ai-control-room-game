using System;

namespace ControlRoom
{
    /// <summary>Authoritative mission state, independent from language interpretation and presentation.</summary>
    public sealed class MissionRules
    {
        public const float DurationSeconds = 600f;
        public MissionState State { get; private set; } = MissionState.Briefing;
        public float RemainingSeconds { get; private set; } = DurationSeconds;
        public float ElapsedSeconds => DurationSeconds - RemainingSeconds;
        public bool Paused { get; private set; }
        public bool HasUsb { get; private set; }
        public string FailureReason { get; private set; } = "";
        public RiskLevel Risk { get; private set; } = RiskLevel.LOW;
        public float RiskValue { get; private set; }
        public MissionState Outcome { get; private set; } = MissionState.Briefing;

        public void Start()
        {
            if (State != MissionState.Briefing) return;
            State = MissionState.Playing;
            Outcome = MissionState.Playing;
        }

        public void Tick(float delta)
        {
            if (State != MissionState.Playing || Paused || float.IsNaN(delta) || float.IsInfinity(delta) || delta <= 0f) return;
            RemainingSeconds = Math.Max(0f, RemainingSeconds - delta);
            if (RemainingSeconds <= 0f) Fail("TIMEOUT: 제한시간이 종료되었습니다.");
        }

        public void SetPaused(bool paused)
        {
            if (State == MissionState.Playing) Paused = paused;
        }

        public void AcquireUsb()
        {
            if (State == MissionState.Playing) HasUsb = true;
        }

        public void ReachExit()
        {
            if (State != MissionState.Playing || !HasUsb) return;
            State = MissionState.Success;
            Outcome = MissionState.Success;
            Paused = false;
        }

        public void Caught() => Fail("CAUGHT: 현장 요원이 체포되었습니다.");
        public void DestroyCriticalEvidence() => DestroyCriticalEvidence("UNKNOWN", "원인 미상");
        public void DestroyCriticalEvidence(string targetId, string cause) =>
            Fail("EVIDENCE_DESTROYED: 대상=" + (targetId ?? "UNKNOWN") + "; 원인=" + (cause ?? "원인 미상"));
        public void LoseObjective() => Fail("OBJECTIVE_LOST: 핵심 목표 달성이 불가능해졌습니다.");

        public void AddRisk(float amount)
        {
            if (State != MissionState.Playing || amount <= 0f || float.IsNaN(amount) || float.IsInfinity(amount)) return;
            RiskValue = Math.Min(100f, RiskValue + amount);
            Risk = RiskValue >= 100f ? RiskLevel.COMPROMISED : RiskValue >= 65f ? RiskLevel.HIGH : RiskValue >= 30f ? RiskLevel.MEDIUM : RiskLevel.LOW;
        }

        public void ShowResult()
        {
            if (State == MissionState.Success || State == MissionState.Failure) State = MissionState.Result;
        }

        public void Reset()
        {
            State = MissionState.Briefing;
            Outcome = MissionState.Briefing;
            RemainingSeconds = DurationSeconds;
            Paused = false;
            HasUsb = false;
            FailureReason = "";
            RiskValue = 0f;
            Risk = RiskLevel.LOW;
        }

        private void Fail(string reason)
        {
            if (State != MissionState.Playing) return;
            FailureReason = reason;
            State = MissionState.Failure;
            Outcome = MissionState.Failure;
            Paused = false;
        }
    }
}
