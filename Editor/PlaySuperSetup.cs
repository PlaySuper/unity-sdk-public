#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public class PlaySuperSetup
{
    private const string ADVERTISING_ID_SYMBOL = "ENABLE_ADVERTISING_ID";
    
    static PlaySuperSetup()
    {
        // Show warning dialog on first import
        if (!SessionState.GetBool("PlaySuper_Setup_Complete", false))
        {
            ShowSetupDialog();
            SessionState.SetBool("PlaySuper_Setup_Complete", true);
        }
    }
    
    private static void ShowSetupDialog()
    {
        bool enableAdId = EditorUtility.DisplayDialog(
            "PlaySuper SDK Setup",
            "Enable Advertising ID collection for Android & iOS?\n\n" +
            "iOS: Requires App Store Connect IDFA declaration & ATT permission\n" +
            "Android: Uses Google Advertising ID (no special declarations needed)\n\n" +
            "Choose 'Enable' only if:\n" +
            "• You need advertising IDs for analytics/attribution\n" +
            "• You can handle iOS App Store IDFA requirements\n" +
            "• Your app will request appropriate permissions",
            "Enable for Both Platforms", 
            "Disable"
        );
        
        if (enableAdId)
        {
            AddDefineSymbol();
            Debug.Log("<color=yellow>[PlaySuper] Advertising ID enabled. Remember to declare IDFA usage in App Store Connect!</color>");
        }
        else
        {
            Debug.Log("<color=green>[PlaySuper] Advertising ID disabled. Safer for App Store submission.</color>");
        }
    }
    
    private static void AddDefineSymbol()
    {
        BuildTargetGroup[] targetGroups = { BuildTargetGroup.iOS, BuildTargetGroup.Android };
        
        foreach (var targetGroup in targetGroups)
        {
            string defines = PlayerSettings.GetScriptingDefineSymbolsForGroup(targetGroup);
            if (!defines.Contains(ADVERTISING_ID_SYMBOL))
            {
                defines = string.IsNullOrEmpty(defines) ? ADVERTISING_ID_SYMBOL : defines + ";" + ADVERTISING_ID_SYMBOL;
                PlayerSettings.SetScriptingDefineSymbolsForGroup(targetGroup, defines);
            }
        }
    }
}
#endif