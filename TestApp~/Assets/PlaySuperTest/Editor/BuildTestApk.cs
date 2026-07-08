using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// One-click scene + APK builder for the UPI test harness.
/// PlaySuper → Build UPI Test APK. Output: Builds/ps-upi-test.apk
/// </summary>
public static class BuildTestApk
{
    private const string ScenePath = "Assets/PlaySuperTest/UpiTest.unity";
    private const string ApkPath = "Builds/ps-upi-test.apk";

    [MenuItem("PlaySuper/Create UPI Test Scene")]
    public static string CreateScene()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
        new GameObject("UpiTestHarness", typeof(UpiTestHarness));
        EditorSceneManager.SaveScene(scene, ScenePath);
        Debug.Log($"[PlaySuper] Test scene saved to {ScenePath}");
        return ScenePath;
    }

    [MenuItem("PlaySuper/Build UPI Test APK")]
    public static void Build()
    {
        string scenePath = File.Exists(ScenePath) ? ScenePath : CreateScene();

        if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
        {
            Debug.Log("[PlaySuper] Switching build target to Android…");
            if (!EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android))
            {
                Debug.LogError("[PlaySuper] Could not switch to Android — is Android Build Support installed?");
                return;
            }
        }

        PlayerSettings.companyName = "PlaySuper";
        PlayerSettings.productName = "PS UPI Test";
        PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.Android, "club.playsuper.upitest");
        PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel23;
        PlayerSettings.Android.forceInternetPermission = true;
        // IL2CPP + ARM64 so the APK installs on 64-bit-only devices (recent Pixels etc.).
        PlayerSettings.SetScriptingBackend(BuildTargetGroup.Android, ScriptingImplementation.IL2CPP);
        PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64 | AndroidArchitecture.ARMv7;

        Directory.CreateDirectory("Builds");
        var report = UnityEditor.BuildPipeline.BuildPlayer(
            new[] { scenePath }, ApkPath, BuildTarget.Android, BuildOptions.None);

        if (report.summary.result == UnityEditor.Build.Reporting.BuildResult.Succeeded)
        {
            Debug.Log($"[PlaySuper] APK built: {Path.GetFullPath(ApkPath)} " +
                      $"({report.summary.totalSize / (1024 * 1024)} MB)");
            if (!Application.isBatchMode)
            {
                EditorUtility.RevealInFinder(ApkPath);
            }
        }
        else
        {
            Debug.LogError($"[PlaySuper] Build failed: {report.summary.result}");
        }
    }
}
