using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace ControlRoom
{
    /// <summary>An optional OpenAI-compatible HTTP interpreter. It produces data only; it never changes the world.</summary>
    public sealed class LlmCommandInterpreter : MonoBehaviour
    {
        [SerializeField] string endpoint = "";
        [SerializeField] string model = "";
        [SerializeField, Range(1, 60)] int timeoutSeconds = 15;
        string apiKey = "";
        bool explicitlyConfigured;

        public bool IsConfigured
        {
            get { LoadEnvironment(); return ValidEndpoint(endpoint) && !string.IsNullOrWhiteSpace(model); }
        }

        public void Configure(string serviceEndpoint, string serviceModel, string serviceApiKey = "")
        {
            endpoint = serviceEndpoint ?? "";
            model = serviceModel ?? "";
            apiKey = serviceApiKey ?? "";
            explicitlyConfigured = true;
        }

        void LoadEnvironment()
        {
            if (explicitlyConfigured) return;
            if (string.IsNullOrWhiteSpace(endpoint)) endpoint = Environment.GetEnvironmentVariable("CONTROLROOM_LLM_ENDPOINT") ?? "";
            if (string.IsNullOrWhiteSpace(model)) model = Environment.GetEnvironmentVariable("CONTROLROOM_LLM_MODEL") ?? "";
            apiKey = Environment.GetEnvironmentVariable("CONTROLROOM_LLM_API_KEY") ?? "";
        }

        public IEnumerator Interpret(string prompt, CommandTarget target, Action<ParseResult> completed)
        {
            if (!IsConfigured)
            {
                Complete(completed, Failed("LLM endpoint and model are not configured."));
                yield break;
            }
            if (string.IsNullOrWhiteSpace(prompt) || prompt.Length > 8000 || !Enum.IsDefined(typeof(CommandTarget), target))
            {
                Complete(completed, Failed("Invalid interpreter input."));
                yield break;
            }
            UnityWebRequest request = null;
            string setupError = "";
            UnityWebRequestAsyncOperation operation = null;
            try
            {
                string instructions = "Translate the user's instruction to one JSON Command object only. " +
                    "You cannot execute actions, decide success, invent objects, reveal hidden state, or declare mission outcomes. " +
                    "Allowed fields: target, action, objectId (legacy object accepted), locationId (legacy location accepted), " +
                    "conditions, sequence, restrictions, outputFormat, confidence. target must be " + target + ". " +
                    "FIELD_AGENT actions: MOVE WAIT INSPECT PICKUP OPEN PURGE HIDE REPORT PHOTO. ANALYSIS_SYSTEM action: ANALYZE. " +
                    "Locations: ENTRANCE MAIN_HALL LABORATORY STORAGE SERVER_ROOM EXIT. " +
                    "Use generic DOOR CABINET DESK TERMINAL STORAGE_DEVICE when unspecified; do not choose one arbitrarily. " +
                    "Each sequence element is an object with action, objectId, locationId and optional duration (seconds, >0 <=600). " +
                    "Keep the order of actionable clauses, exclude negated actions, and never drop a condition. " +
                    "OPEN on a terminal means show its guidance and preserve evidence. Only map an explicit delete instruction to PURGE; the application always requests a separate player confirmation before it can execute. Never invent PURGE from OPEN or INSPECT. " +
                    "Restrictions: DO_NOT_PICKUP AVOID_GUARD DO_NOT_OPEN DO_NOT_DESTROY LOCK_CODE:<digits>. " +
                    "FIELD_AGENT conditions may contain only GUARD_CLEAR, used when told to wait until the guard passes; do not invent other field conditions. " +
                    "ANALYSIS_SYSTEM conditions may express requested data/time filters, also preserve those filters in the user's instruction. Never add outcome or result fields. " +
                    "For unsupported actions, return an invalid action so validation rejects it. No Markdown fences.";
                string body = "{\"model\":" + CommandJsonCodec.Quote(model) + ",\"temperature\":0,\"response_format\":{\"type\":\"json_object\"},\"messages\":[" +
                    "{\"role\":\"system\",\"content\":" + CommandJsonCodec.Quote(instructions) + "}," +
                    "{\"role\":\"user\",\"content\":" + CommandJsonCodec.Quote(prompt) + "}]}";
                request = new UnityWebRequest(endpoint, "POST")
                {
                    uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body)),
                    downloadHandler = new DownloadHandlerBuffer(), timeout = Mathf.Clamp(timeoutSeconds, 1, 60)
                };
                request.SetRequestHeader("Content-Type", "application/json");
                if (!string.IsNullOrWhiteSpace(apiKey)) request.SetRequestHeader("Authorization", "Bearer " + apiKey);
                operation = request.SendWebRequest();
            }
            catch (Exception exception) { setupError = exception.GetType().Name; }
            if (operation == null)
            {
                request?.Dispose();
                Complete(completed, Failed("Request setup failed: " + setupError));
                yield break;
            }
            ParseResult result = null;
            try
            {
                yield return operation;
                try
                {
                    if (request.result != UnityWebRequest.Result.Success)
                        result = Failed("LLM request failed (HTTP " + request.responseCode + ").");
                    else
                    {
                        result = ParseResponse(request.downloadHandler.text, target);
                        if (result.Success) result.command.prompt = prompt;
                    }
                }
                catch (Exception) { result = Failed("LLM response could not be read."); }
            }
            finally { request.Dispose(); }
            Complete(completed, result);
        }

        public static ParseResult ParseResponse(string responseBody, CommandTarget expectedTarget)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(responseBody) || responseBody.Length > 65536) return Failed("Invalid LLM response length.");
                var root = CommandJsonCodec.ReadJson(responseBody) as Dictionary<string, object>;
                if (root == null) return Failed("LLM response must be JSON.");
                string commandJson = responseBody;
                if (!root.ContainsKey("target"))
                {
                    object choicesValue;
                    if (!root.TryGetValue("choices", out choicesValue)) return Failed("LLM response has no command.");
                    var choices = choicesValue as List<object>;
                    if (choices == null || choices.Count != 1) return Failed("LLM response must have one choice.");
                    var choice = choices[0] as Dictionary<string, object>;
                    object messageValue;
                    if (choice == null || !choice.TryGetValue("message", out messageValue)) return Failed("LLM response has no message.");
                    var message = messageValue as Dictionary<string, object>;
                    object content;
                    if (message == null || !message.TryGetValue("content", out content) || !(content is string)) return Failed("LLM response has no structured content.");
                    commandJson = (string)content;
                }
                ParseResult result = CommandJsonCodec.Parse(commandJson);
                if (result.Success && result.command.target != expectedTarget) return Failed("LLM changed the selected target.");
                return result;
            }
            catch (Exception exception) when (exception is FormatException || exception is ArgumentException || exception is OverflowException)
            { return Failed("Malformed LLM JSON."); }
        }

        static bool ValidEndpoint(string value)
        {
            Uri parsed;
            return Uri.TryCreate(value, UriKind.Absolute, out parsed) && (parsed.Scheme == "https" || parsed.Scheme == "http");
        }
        static ParseResult Failed(string message) { return new ParseResult { error = "COMMAND PROCESSING FAILED: " + message }; }
        static void Complete(Action<ParseResult> completed, ParseResult result)
        {
            if (completed == null) return;
            try { completed(result); }
            catch (Exception exception) { Debug.LogException(exception); }
        }
    }
}
