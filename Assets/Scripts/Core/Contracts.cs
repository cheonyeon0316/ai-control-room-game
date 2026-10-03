using System;
using System.Collections.Generic;

namespace ControlRoom
{
    public enum CommandTarget { FIELD_AGENT, ANALYSIS_SYSTEM }
    public enum CommandAction { MOVE, WAIT, INSPECT, PICKUP, OPEN, PURGE, HIDE, REPORT, PHOTO, ANALYZE }
    public enum AgentState { Idle, Moving, Interacting, Waiting, Reporting, Hidden, Caught }
    public enum MissionState { Briefing, Playing, Success, Failure, Result }
    public enum CommandStatus { RECEIVED, PROCESSING, EXECUTING, COMPLETED, NEEDS_CLARIFICATION, FAILED }
    public enum Reliability { CONFIRMED, LIKELY, UNVERIFIED, CONFLICTED }
    public enum RiskLevel { LOW, MEDIUM, HIGH, COMPROMISED }

    [Serializable]
    public class CommandStep
    {
        public CommandAction action;
        public string objectId = "";
        public string locationId = "";
        public float duration = 3f;
        public CommandStep() { }
        public CommandStep(CommandAction action, string locationId = "", string objectId = "", float duration = 3f)
        { this.action = action; this.locationId = locationId; this.objectId = objectId; this.duration = duration; }
    }

    [Serializable]
    public class Command
    {
        public CommandTarget target;
        public CommandAction action;
        public string objectId = "";
        public string locationId = "";
        public List<string> conditions = new List<string>();
        public List<CommandStep> sequence = new List<CommandStep>();
        public List<string> restrictions = new List<string>();
        public string outputFormat = "summary";
        public float confidence = 1f;
        public string prompt = "";
        public IEnumerable<CommandStep> Steps => sequence.Count > 0 ? (IEnumerable<CommandStep>)sequence : new[] { new CommandStep(action, locationId, objectId) };
    }

    public class ParseResult
    {
        public Command command;
        public string error = "";
        public bool Success => command != null && string.IsNullOrEmpty(error);
    }

    [Serializable]
    public class ObjectRecord
    {
        public string Id, Label, LocationId, Kind;
        public bool IsKnown = true, IsAvailable = true;
        public ObjectRecord(string id, string label, string locationId, string kind)
        { Id = id; Label = label; LocationId = locationId; Kind = kind; }
    }

    public class WorldContext
    {
        public string locationId = "ENTRANCE";
        public List<ObjectRecord> objects = new List<ObjectRecord>();
        public HashSet<string> knownLocations = new HashSet<string> { "ENTRANCE", "MAIN_HALL", "LABORATORY", "STORAGE", "SERVER_ROOM", "EXIT" };
    }

    public class ValidationResult
    {
        public bool valid;
        public string code = "", message = "";
        public Command command;
        public List<ObjectRecord> candidates = new List<ObjectRecord>();
    }

    [Serializable]
    public class EvidenceRecord
    {
        public string id = "", title = "", content = "", sourceId = "", timestamp = "", locationId = "", imagePath = "";
        public Reliability reliability;
    }

    [Serializable]
    public class AnalysisRow
    {
        public string timestamp = "", person = "", locationId = "", cameraId = "", sourceId = "";
    }

    public class AnalysisResult
    {
        public string text = "", sourceId = "", timestamp = "";
        public Reliability reliability;
        public bool verifiedLocation;
        public List<EvidenceRecord> evidence = new List<EvidenceRecord>();
        public List<AnalysisRow> rows = new List<AnalysisRow>();
    }
}
