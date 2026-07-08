using PlaySuperUnity;
using UnityEngine;

/// <summary>
/// Minimal harness to test the in-game store's UPI Intent flow:
/// paste the game's API key, guest-login, open the store, buy something
/// with UPI. Everything is on-screen — no scene wiring needed.
/// </summary>
public class UpiTestHarness : MonoBehaviour
{
    // Optionally hardcode before building so the key is prefilled on device.
    private const string DefaultApiKey = "";

    private string apiKey;
    private string status = "Enter API key, then Init + Login.";
    private bool initialized;

    private GUIStyle labelStyle;
    private GUIStyle titleStyle;
    private GUIStyle buttonStyle;
    private GUIStyle fieldStyle;

    private void Awake()
    {
        apiKey = PlayerPrefs.GetString("test_apiKey", DefaultApiKey);
    }

    private void EnsureStyles()
    {
        if (labelStyle != null) return;
        labelStyle = new GUIStyle(GUI.skin.label) { fontSize = 13, wordWrap = true };
        titleStyle = new GUIStyle(GUI.skin.label) { fontSize = 20, fontStyle = FontStyle.Bold };
        buttonStyle = new GUIStyle(GUI.skin.button) { fontSize = 15 };
        fieldStyle = new GUIStyle(GUI.skin.textField) { fontSize = 13 };
    }

    private void OnGUI()
    {
        EnsureStyles();

        // Scale IMGUI for phone screens.
        float scale = Screen.dpi > 0 ? Screen.dpi / 96f : 3f;
        GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, Vector3.one * scale);
        float w = Screen.width / scale - 40;

        GUILayout.BeginArea(new Rect(20, 40, w, 600));
        GUILayout.Label("PlaySuper UPI Test", titleStyle);
        GUILayout.Space(8);

        GUILayout.Label("Game API key:", labelStyle);
        apiKey = GUILayout.TextField(apiKey ?? string.Empty, fieldStyle, GUILayout.Height(34));

        GUILayout.Space(8);
        if (GUILayout.Button("1) Init + Guest Login", buttonStyle, GUILayout.Height(44)))
        {
            InitAndLogin();
        }

        GUILayout.Space(6);
        if (GUILayout.Button("2) Open Store", buttonStyle, GUILayout.Height(44)))
        {
            OpenStore();
        }

        GUILayout.Space(10);
        GUILayout.Label($"Status: {status}", labelStyle);
        GUILayout.Label($"Logged in: {(initialized && PlaySuperUnitySDK.IsLoggedIn())}", labelStyle);
        GUILayout.EndArea();
    }

    private async void InitAndLogin()
    {
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            status = "API key is empty.";
            return;
        }
        PlayerPrefs.SetString("test_apiKey", apiKey.Trim());

        if (!initialized)
        {
            PlaySuperUnitySDK.Initialize(apiKey.Trim(), _isDev: false);
            initialized = true;
        }
        status = "Initialized. Logging in…";

        // login/federatedByStudio only signs in EXISTING players (404 for an
        // unknown uuid) — create the guest player first, then log in.
        string uuid = SystemInfo.deviceUniqueIdentifier;
        var login = await PlaySuperUnitySDK.Instance.LoginFederatedByStudio(uuid);
        if (login == null || !PlaySuperUnitySDK.IsLoggedIn())
        {
            status = "No existing player — creating guest player…";
            var created = await PlaySuperUnitySDK.Instance.CreatePlayerWithUuid(uuid);
            if (created != null)
            {
                login = await PlaySuperUnitySDK.Instance.LoginFederatedByStudio(uuid);
            }
        }
        status = login != null && PlaySuperUnitySDK.IsLoggedIn()
            ? "Guest login OK — open the store."
            : "Login FAILED — check the API key (must be a prod game key) and adb logcat -s Unity. Don't open the store yet.";
    }

    private void OpenStore()
    {
        if (!initialized)
        {
            status = "Init first (button 1).";
            return;
        }
        status = "Opening store…";
        PlaySuperUnitySDK.Instance.OpenStore();
    }
}
