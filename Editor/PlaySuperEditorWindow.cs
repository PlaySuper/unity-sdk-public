using UnityEditor;
using UnityEngine;

public class PlaySuperEditorWindow : EditorWindow
{
    // public GameIdData gameIdData;

    [MenuItem("Assets/PlaySuper/Settings")]
    public static void ShowWindow()
    {
        GetWindow<PlaySuperEditorWindow>("PlaySuper Settings");
    }

    private void OnGUI()
    {
        GUILayout.Label("PlaySuper Settings", EditorStyles.boldLabel);

        // Select or create a GameIdData ScriptableObject
        // gameIdData = (GameIdData)EditorGUILayout.TextField("Game ID", gameIdData, typeof(GameIdData), false);

        // if (gameIdData != null)
        // {
        //     // Field for Game ID
        //     gameIdData.gameId = EditorGUILayout.TextField("Game ID", gameIdData.gameId);

        //     // Save changes
        //     EditorUtility.SetDirty(gameIdData);
        // }
        // else
        // {
        //     if (GUILayout.Button("Save Game ID Data"))
        //     {
        //         SaveGameIdData();
        //     }
        // }
    }

    // private void SaveGameIdData()
    // {
    //     gameIdData = ScriptableObject.CreateInstance<GameIdData>();
    //     AssetDatabase.CreateAsset(gameIdData, "Assets/PlaySuper/GameIdData.asset");
    //     AssetDatabase.SaveAssets();
    //     EditorUtility.FocusProjectWindow();
    //     Selection.activeObject = gameIdData;
    // }
}
