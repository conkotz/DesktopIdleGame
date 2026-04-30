#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// Preset <see cref="HelperPopupDefinition"/> assets. Menu path must differ from
/// <see cref="HelperPopupDefinition"/>'s <c>CreateAssetMenu</c> leaf name (Empty vs this item).
/// </summary>
public static class HelperPopupDefinitionMenu
{
    [MenuItem("Assets/Create/Desktop Idle Game/Helper Popup Definition/Game Start")]
    public static void CreateGameStartPreset()
    {
        var def = ScriptableObject.CreateInstance<HelperPopupDefinition>();
        def.helperId = "early_game_helper";
        def.title = "Welcome";
        def.bodyText =
            "Welcome to the game, let's start going through the basics. Firstly, seeing a yellow exclamation mark signifies a quest is available. Try clicking on the NPC here to get your first quest.\n\n" +
            "The Help feature can be disabled in Settings.";
        def.activationTrigger = HelperActivationTrigger.FirstVisitMapNode;
        def.requiredMapNodeId = "tutorial_1";
        def.priority = 0;
        def.dismissModes = HelperDismissMode.CloseButton |
            HelperDismissMode.CharacterPageOpened |
            HelperDismissMode.InteractWhitelistDismiss;
        def.whitelistedInteractionIds = new[] { "NPC_Tutorial_1" };

        const string dir = "Assets/3.ScriptableObjects/HelperDefinitions";
        if (!AssetDatabase.IsValidFolder("Assets/3.ScriptableObjects"))
            AssetDatabase.CreateFolder("Assets", "3.ScriptableObjects");
        if (!AssetDatabase.IsValidFolder(dir))
            AssetDatabase.CreateFolder("Assets/3.ScriptableObjects", "HelperDefinitions");

        string path = AssetDatabase.GenerateUniqueAssetPath($"{dir}/Helper_GameStart.asset");
        AssetDatabase.CreateAsset(def, path);
        AssetDatabase.SaveAssets();
        EditorGUIUtility.PingObject(def);
    }
}
#endif
