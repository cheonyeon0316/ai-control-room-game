using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using NUnit.Framework;

/// <summary>
/// Executes the repository's actual NUnit domain cases on bundled Mono.
/// This process never starts Unity and cannot validate scenes, UI, navigation or native engine behavior.
/// </summary>
internal static class DomainTestRunner
{
    private sealed class CaseResult
    {
        public string Name, Fixture, Method, Status, Reason;
        public object[] Arguments;
        public double DurationMilliseconds;
    }

    private static readonly Dictionary<string, string> NativeEngineCases = new Dictionary<string, string>
    {
        {
            "ControlRoom.Tests.CommandDomainTests.UnconfiguredLlmFailsWithoutBlockingTheFrameLoop",
            "Unrun outside Unity: creates GameObject/AddComponent and calls Object.DestroyImmediate. Requires native Unity lifecycle; HTTP coroutine behavior is not verified by this runner."
        }
    };

    private static int Main(string[] arguments)
    {
        if (arguments.Length < 1)
        {
            Console.Error.WriteLine("Usage: DomainTestRunner.exe <results.json>");
            return 2;
        }
        var results = new List<CaseResult>();
        string started = DateTime.UtcNow.ToString("o");
        string infrastructureError = "";
        var watch = Stopwatch.StartNew();
        try
        {
            RunFixture(typeof(ControlRoom.Tests.CommandDomainTests), results);
            RunFixture(typeof(ControlRoom.Tests.AnalysisMissionTests), results);
        }
        catch (Exception exception)
        {
            infrastructureError = Unwrap(exception).ToString();
            Console.Error.WriteLine("DOMAIN RUNNER ERROR: " + infrastructureError);
        }
        watch.Stop();
        int passed = results.Count(result => result.Status == "Passed");
        int failed = results.Count(result => result.Status == "Failed");
        int skipped = results.Count(result => result.Status == "Unrun");
        try { WriteResults(arguments[0], results, started, watch.Elapsed.TotalMilliseconds, infrastructureError); }
        catch (Exception exception)
        {
            Console.Error.WriteLine("Failed to write domain results: " + exception);
            return 2;
        }
        Console.WriteLine("INDEPENDENT DOMAIN TESTS (NOT Unity acceptance / NOT PlayMode)");
        Console.WriteLine("Discovered " + results.Count + ": passed " + passed + ", failed " + failed + ", unrun " + skipped + ".");
        Console.WriteLine("Results: " + Path.GetFullPath(arguments[0]));
        return infrastructureError.Length > 0 || results.Count == 0 ? 2 : failed > 0 ? 1 : 0;
    }

    private static void RunFixture(Type fixtureType, List<CaseResult> results)
    {
        object fixture = Activator.CreateInstance(fixtureType);
        var setup = LifecycleMethods(fixtureType, "SetUpAttribute", false);
        var teardown = LifecycleMethods(fixtureType, "TearDownAttribute", true);
        var oneTimeSetup = LifecycleMethods(fixtureType, "OneTimeSetUpAttribute", false);
        var oneTimeTeardown = LifecycleMethods(fixtureType, "OneTimeTearDownAttribute", true);
        Exception fixtureSetupError = null;
        try { InvokeAll(oneTimeSetup, fixture); }
        catch (Exception exception) { fixtureSetupError = Unwrap(exception); }

        foreach (var method in fixtureType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static).OrderBy(method => method.Name))
        {
            var cases = method.GetCustomAttributes(typeof(TestCaseAttribute), true).Cast<TestCaseAttribute>().ToArray();
            bool plainTest = HasAttribute(method, "TestAttribute");
            bool sourceCases = HasAttribute(method, "TestCaseSourceAttribute");
            bool unityTest = HasAttribute(method, "UnityTestAttribute");
            if (cases.Length == 0 && !plainTest && !sourceCases && !unityTest) continue;
            var arguments = cases.Length > 0 ? cases.Select(testCase => testCase.Arguments).ToArray() : new[] { new object[0] };
            for (int index = 0; index < arguments.Length; index++)
            {
                var result = new CaseResult
                {
                    Fixture = fixtureType.FullName, Method = method.Name, Arguments = arguments[index],
                    Name = fixtureType.FullName + "." + method.Name + "(" + string.Join(", ", arguments[index].Select(value => Convert.ToString(value, CultureInfo.InvariantCulture))) + ")",
                    Status = "Passed", Reason = ""
                };
                results.Add(result);
                string skipReason;
                if (NativeEngineCases.TryGetValue(fixtureType.FullName + "." + method.Name, out skipReason))
                    MarkUnrun(result, skipReason);
                else if (HasAttribute(fixtureType, "IgnoreAttribute") || HasAttribute(method, "IgnoreAttribute"))
                    MarkUnrun(result, "NUnit Ignore attribute: " + IgnoreReason(fixtureType, method));
                else if (HasAttribute(method, "ExplicitAttribute") || HasAttribute(fixtureType, "ExplicitAttribute"))
                    MarkUnrun(result, "NUnit Explicit case requires a separate explicit invocation.");
                else if (sourceCases && cases.Length == 0)
                    MarkUnrun(result, "TestCaseSource is unsupported by this independent runner; run this fixture through Unity NUnit.");
                else if (unityTest || typeof(IEnumerator).IsAssignableFrom(method.ReturnType))
                    MarkUnrun(result, "Unity coroutine tests require the Unity frame loop.");
                else if (fixtureSetupError != null)
                {
                    result.Status = "Failed";
                    result.Reason = "OneTimeSetUp failed: " + fixtureSetupError;
                }
                else RunCase(fixture, method, result, cases.Length > 0 ? cases[index] : null, setup, teardown);
                if (result.Status == "Failed") Console.Error.WriteLine("FAIL " + result.Name + "\n" + result.Reason);
                else if (result.Status == "Unrun") Console.WriteLine("UNRUN " + result.Name + " — " + result.Reason);
            }
        }
        try { InvokeAll(oneTimeTeardown, fixture); }
        catch (Exception exception)
        {
            results.Add(new CaseResult { Fixture = fixtureType.FullName, Method = "OneTimeTearDown", Name = fixtureType.FullName + ".OneTimeTearDown", Arguments = new object[0], Status = "Failed", Reason = Unwrap(exception).ToString() });
        }
    }

    private static void RunCase(object fixture, MethodInfo method, CaseResult result, TestCaseAttribute testCase, MethodInfo[] setup, MethodInfo[] teardown)
    {
        var watch = Stopwatch.StartNew();
        try
        {
            InvokeAll(setup, fixture);
            object returned = Invoke(method, fixture, result.Arguments);
            if (testCase != null && testCase.HasExpectedResult)
                Assert.That(returned, Is.EqualTo(testCase.ExpectedResult), "NUnit TestCase ExpectedResult");
        }
        catch (Exception exception)
        {
            var original = Unwrap(exception);
            if (original.GetType().Name == "SuccessException") { result.Status = "Passed"; result.Reason = original.Message; }
            else if (original.GetType().Name == "IgnoreException" || original.GetType().Name == "InconclusiveException") MarkUnrun(result, original.ToString());
            else { result.Status = "Failed"; result.Reason = original.ToString(); }
        }
        finally
        {
            // Teardown is attempted even when setup/test assertions fail; its failures also fail the case.
            foreach (var cleanup in teardown)
            {
                try { Invoke(cleanup, fixture, new object[0]); }
                catch (Exception exception)
                {
                    result.Status = "Failed";
                    result.Reason += (result.Reason.Length > 0 ? "\n" : "") + "TearDown failed: " + Unwrap(exception);
                }
            }
            watch.Stop();
            result.DurationMilliseconds = watch.Elapsed.TotalMilliseconds;
        }
    }

    private static object Invoke(MethodInfo method, object fixture, object[] arguments)
    {
        object value = method.Invoke(method.IsStatic ? null : fixture, arguments);
        var task = value as Task;
        if (task != null)
        {
            task.GetAwaiter().GetResult();
            var resultProperty = task.GetType().GetProperty("Result");
            return resultProperty == null ? null : resultProperty.GetValue(task, null);
        }
        return value;
    }

    private static void InvokeAll(IEnumerable<MethodInfo> methods, object fixture)
    {
        foreach (var method in methods) Invoke(method, fixture, new object[0]);
    }

    private static MethodInfo[] LifecycleMethods(Type fixtureType, string attribute, bool reverse)
    {
        var hierarchy = new List<Type>();
        for (var type = fixtureType; type != null && type != typeof(object); type = type.BaseType) hierarchy.Add(type);
        if (!reverse) hierarchy.Reverse();
        return hierarchy.SelectMany(type => type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
            .Where(method => HasAttribute(method, attribute)).OrderBy(method => method.MetadataToken)).ToArray();
    }

    private static bool HasAttribute(MemberInfo member, string name) => member.GetCustomAttributes(true).Any(attribute => attribute.GetType().Name == name);
    private static string IgnoreReason(params MemberInfo[] members)
    {
        foreach (var member in members)
        {
            var attribute = member.GetCustomAttributes(true).FirstOrDefault(value => value.GetType().Name == "IgnoreAttribute");
            if (attribute == null) continue;
            var reason = attribute.GetType().GetProperty("Reason");
            return reason == null ? "Ignored by fixture/test" : Convert.ToString(reason.GetValue(attribute, null));
        }
        return "Ignored";
    }
    private static Exception Unwrap(Exception exception)
    {
        while (exception is TargetInvocationException && exception.InnerException != null) exception = exception.InnerException;
        return exception;
    }
    private static void MarkUnrun(CaseResult result, string reason) { result.Status = "Unrun"; result.Reason = reason; }

    private static void WriteResults(string path, List<CaseResult> results, string started, double elapsed, string error)
    {
        var text = new StringBuilder("{\n");
        text.AppendLine("  \"schemaVersion\": 1,");
        text.AppendLine("  \"kind\": \"independent-domain\",");
        text.AppendLine("  \"isUnityAcceptance\": false,");
        text.AppendLine("  \"isPlayMode\": false,");
        text.AppendLine("  \"description\": \"Actual repository NUnit domain tests on bundled Mono. Native scene, UI, NavMesh, CCTV, Unity object lifecycle and HTTP transport remain unverified.\",");
        text.AppendLine("  \"startedAtUtc\": " + Quote(started) + ",");
        text.AppendLine("  \"finishedAtUtc\": " + Quote(DateTime.UtcNow.ToString("o")) + ",");
        text.AppendLine("  \"durationMilliseconds\": " + elapsed.ToString("0.###", CultureInfo.InvariantCulture) + ",");
        text.AppendLine("  \"total\": " + results.Count + ",");
        text.AppendLine("  \"passed\": " + results.Count(result => result.Status == "Passed") + ",");
        text.AppendLine("  \"failed\": " + results.Count(result => result.Status == "Failed") + ",");
        text.AppendLine("  \"unrun\": " + results.Count(result => result.Status == "Unrun") + ",");
        text.AppendLine("  \"infrastructureError\": " + Quote(error) + ",");
        text.AppendLine("  \"tests\": [");
        for (int index = 0; index < results.Count; index++)
        {
            var result = results[index];
            text.Append("    {\"name\": ").Append(Quote(result.Name)).Append(", \"fixture\": ").Append(Quote(result.Fixture));
            text.Append(", \"method\": ").Append(Quote(result.Method)).Append(", \"status\": ").Append(Quote(result.Status));
            text.Append(", \"arguments\": [").Append(string.Join(", ", result.Arguments.Select(value => Quote(Convert.ToString(value, CultureInfo.InvariantCulture))))).Append(']');
            text.Append(", \"durationMilliseconds\": ").Append(result.DurationMilliseconds.ToString("0.###", CultureInfo.InvariantCulture));
            text.Append(", \"reason\": ").Append(Quote(result.Reason)).Append('}').AppendLine(index + 1 == results.Count ? "" : ",");
        }
        text.AppendLine("  ]\n}");
        var absolute = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(absolute));
        File.WriteAllText(absolute, text.ToString(), new UTF8Encoding(false));
    }

    private static string Quote(string value)
    {
        var text = new StringBuilder("\"");
        foreach (char character in value ?? "")
        {
            if (character == '"') text.Append("\\\"");
            else if (character == '\\') text.Append("\\\\");
            else if (character == '\n') text.Append("\\n");
            else if (character == '\r') text.Append("\\r");
            else if (character == '\t') text.Append("\\t");
            else if (character < 32) text.Append("\\u").Append(((int)character).ToString("x4"));
            else text.Append(character);
        }
        return text.Append('"').ToString();
    }
}
