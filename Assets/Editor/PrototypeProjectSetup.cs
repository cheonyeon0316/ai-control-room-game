using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace ControlRoom.Editor
{
    public static class PrototypeProjectSetup
    {
        [InitializeOnLoadMethod]
        private static void EnsureFirstOpen()
        {
            if (Application.isBatchMode) return;
            EditorApplication.delayCall += () =>
            {
                if (!EditorApplication.isPlayingOrWillChangePlaymode && !File.Exists("Assets/Settings/ControlRoomURP.asset")) PrepareProject();
            };
        }
        [MenuItem("Control Room/Prepare Prototype Scenes")]
        public static void PrepareProject()
        {
            foreach (var folder in new[] { "Art", "Audio", "Materials", "Prefabs", "Scenes", "ScriptableObjects", "Settings" })
                Directory.CreateDirectory("Assets/" + folder);
            AssetDatabase.Refresh();
            EditorSettings.serializationMode = SerializationMode.ForceText;
            PlayerSettings.companyName = "Control Room Prototype";
            PlayerSettings.productName = "AI 관제 지휘 / DATA LEAK";
            PlayerSettings.defaultScreenWidth = 1920;
            PlayerSettings.defaultScreenHeight = 1080;
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.runInBackground = true;
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, ScriptingImplementation.Mono2x);
            ConfigureRenderPipeline();
            CreateScene("Boot", typeof(SceneBoot));
            CreateScene("Mission_01", typeof(GameManager));
            CreateScene("Result", typeof(ResultSceneEntry));
            EditorBuildSettings.scenes = new[] {
                new EditorBuildSettingsScene("Assets/Scenes/Boot.unity", true),
                new EditorBuildSettingsScene("Assets/Scenes/Mission_01.unity", true),
                new EditorBuildSettingsScene("Assets/Scenes/Result.unity", true)
            };
            AssetDatabase.SaveAssets();
            EditorSceneManager.OpenScene("Assets/Scenes/Mission_01.unity");
            Debug.Log("Prototype prepared: Boot / Mission_01 / Result, URP, 1920×1080.");
        }

        private static void ConfigureRenderPipeline()
        {
            const string rendererPath = "Assets/Settings/ControlRoomRenderer.asset";
            const string pipelinePath = "Assets/Settings/ControlRoomURP.asset";
            var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(rendererPath);
            if (renderer == null)
            {
                renderer = ScriptableObject.CreateInstance<UniversalRendererData>();
                AssetDatabase.CreateAsset(renderer, rendererPath);
            }
            var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(pipelinePath);
            if (pipeline == null)
            {
                pipeline = UniversalRenderPipelineAsset.Create(renderer);
                AssetDatabase.CreateAsset(pipeline, pipelinePath);
            }
            pipeline.renderScale = 1f; pipeline.msaaSampleCount = 2;
            pipeline.supportsHDR = false;
            GraphicsSettings.defaultRenderPipeline = pipeline;
            QualitySettings.renderPipeline = pipeline;
            QualitySettings.vSyncCount = 0;
            EditorUtility.SetDirty(pipeline);
            Directory.CreateDirectory("Assets/Resources/Materials");
            AssetDatabase.Refresh();
            const string surfacePath = "Assets/Resources/Materials/PrototypeSurface.mat";
            if (AssetDatabase.LoadAssetAtPath<Material>(surfacePath) == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null) throw new Exception("URP Lit shader is not available.");
                AssetDatabase.CreateAsset(new Material(shader), surfacePath);
            }
            const string emissivePath = "Assets/Resources/Materials/PrototypeEmissive.mat";
            if (AssetDatabase.LoadAssetAtPath<Material>(emissivePath) == null)
            {
                var emissive = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                emissive.EnableKeyword("_EMISSION");
                emissive.SetColor("_EmissionColor", Color.white);
                AssetDatabase.CreateAsset(emissive, emissivePath);
            }
        }

        private static void CreateScene(string name, Type component)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            new GameObject(name + " Entry").AddComponent(component);
            EditorSceneManager.SaveScene(scene, "Assets/Scenes/" + name + ".unity");
        }

        [MenuItem("Control Room/Build Windows Prototype")]
        public static void BuildWindows()
        {
            PrepareProject();
            Directory.CreateDirectory("Builds/ControlRoom");
            var options = new BuildPlayerOptions
            {
                scenes = new[] { "Assets/Scenes/Boot.unity", "Assets/Scenes/Mission_01.unity", "Assets/Scenes/Result.unity" },
                locationPathName = "Builds/ControlRoom/ControlRoom.exe",
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None
            };
            var report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result != BuildResult.Succeeded)
                throw new Exception("Windows build failed: " + report.summary.result);
            Debug.Log("Windows prototype build complete: " + options.locationPathName);
        }

        [MenuItem("Control Room/Build Web Prototype")]
        public static void BuildWebGL()
        {
            PrepareProject();
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.WebGL, ScriptingImplementation.IL2CPP);
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Disabled;
            PlayerSettings.WebGL.decompressionFallback = false;
            PlayerSettings.WebGL.dataCaching = true;
            PlayerSettings.WebGL.initialMemorySize = 256;
            PlayerSettings.WebGL.showDiagnostics = false;
            PlayerSettings.WebGL.template = "APPLICATION:Default";
            PlayerSettings.colorSpace = ColorSpace.Linear;
            Directory.CreateDirectory("Builds/WebGL");
            var options = new BuildPlayerOptions
            {
                scenes = new[] { "Assets/Scenes/Boot.unity", "Assets/Scenes/Mission_01.unity", "Assets/Scenes/Result.unity" },
                locationPathName = "Builds/WebGL",
                target = BuildTarget.WebGL,
                options = BuildOptions.None
            };
            var report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result != BuildResult.Succeeded)
                throw new Exception("Web build failed: " + report.summary.result);
            Debug.Log("Web prototype build complete: " + options.locationPathName);
        }
    }
}
