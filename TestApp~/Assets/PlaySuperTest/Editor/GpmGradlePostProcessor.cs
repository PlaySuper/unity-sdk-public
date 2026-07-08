using System.IO;
using UnityEditor.Android;
using UnityEngine;

/// <summary>
/// GPM WebView's required Android Gradle setup (see GPM docs → Android →
/// Gradle settings): the GPM AARs are Kotlin but don't bundle the runtime,
/// so kotlin-stdlib must be added to the app's dependencies, plus AndroidX.
/// Games do this via a custom mainTemplate.gradle; injecting into the
/// generated project keeps this build independent of Unity template drift.
/// </summary>
public class GpmGradlePostProcessor : IPostGenerateGradleAndroidProject
{
    public int callbackOrder => 0;

    public void OnPostGenerateGradleAndroidProject(string path)
    {
        // path points at the unityLibrary module.
        string gradle = Path.Combine(path, "build.gradle");
        string text = File.ReadAllText(gradle);
        const string kotlinDep = "implementation 'org.jetbrains.kotlin:kotlin-stdlib:1.8.22'";
        if (!text.Contains("kotlin-stdlib"))
        {
            const string anchor = "dependencies {";
            text = text.Replace(anchor,
                anchor + "\n    " + kotlinDep +
                "\n    implementation 'androidx.browser:browser:1.3.0'");
            File.WriteAllText(gradle, text);
            Debug.Log("[PlaySuper] Injected kotlin-stdlib + androidx.browser into unityLibrary build.gradle");
        }

        string props = Path.Combine(path, "..", "gradle.properties");
        if (File.Exists(props) && !File.ReadAllText(props).Contains("android.useAndroidX"))
        {
            File.AppendAllText(props, "\nandroid.useAndroidX=true\n");
            Debug.Log("[PlaySuper] Enabled android.useAndroidX in gradle.properties");
        }
    }
}
