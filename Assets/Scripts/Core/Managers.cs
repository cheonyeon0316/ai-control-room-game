using System.Collections.Generic;
using UnityEngine;

namespace ControlRoom
{
    public sealed class MissionManager : MonoBehaviour
    {
        public MissionRules Rules { get; private set; }
        public MissionState State => Rules.State;
        public void Initialize(MissionRules rules) { Rules = rules; }
        public void StartMission() { Rules.Start(); }
    }
    public sealed class TimeManager : MonoBehaviour
    {
        public MissionRules Rules { get; private set; }
        public void Initialize(MissionRules rules) { Rules = rules; }
        public void Tick(float delta) { Rules.Tick(delta); }
    }
    public sealed class RiskManager : MonoBehaviour
    {
        public MissionRules Rules { get; private set; }
        public void Initialize(MissionRules rules) { Rules = rules; }
        public void Add(float amount) { Rules.AddRisk(amount); }
    }
    public sealed class AgentManager : MonoBehaviour
    {
        public FieldAgent Agent { get; private set; }
        public void Initialize(FieldAgent agent) { Agent = agent; }
    }
    public sealed class AnalysisManager : MonoBehaviour
    {
        public AnalysisService Service { get; private set; }
        public void Initialize(AnalysisService service) { Service = service; }
        public AnalysisResult Query(string prompt, string format) { return Service.Query(prompt, format); }
    }
    public sealed class EvidenceManager : MonoBehaviour
    {
        public EvidenceDatabase Database { get; private set; }
        public void Initialize(EvidenceDatabase database) { Database = database; }
        public void Add(IEnumerable<EvidenceRecord> evidence) { Database.AddRange(evidence); }
    }
}
