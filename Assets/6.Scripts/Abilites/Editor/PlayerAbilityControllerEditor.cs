#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// Foldout groups for ability VFX fields so the Player Ability Controller inspector stays compact.
/// </summary>
[CustomEditor(typeof(PlayerAbilityController))]
[CanEditMultipleObjects]
public class PlayerAbilityControllerEditor : Editor
{
    private const string FoldPrefsRoot = "PlayerAbilityController.Foldouts.";

    private static readonly string[] RefFieldNames =
    {
        "player",
        "stats",
        "combat",
        "abilityDatabase",
        "skillDatabase",
        "skillsManager",
        "equipment",
        "inventory",
        "buffController"
    };

    private static readonly string[] PowerSlashFieldNames =
    {
        "powerSlashTrailAnchor",
        "powerSlashAnchorNameCandidates",
        "powerSlashTrailColor",
        "powerSlashTrailTime",
        "powerSlashSwingDuration",
        "powerSlashTrailWidth",
        "powerSlashAngleRange",
        "powerSlashLocalOffset",
        "powerSlashTipLocalOffset",
        "powerSlashEdgeFollowSmoothing",
        "powerSlashUseDoubleSwipe",
        "powerSlashSecondSwipeDelay",
        "powerSlashSecondSwipeAngleOffset"
    };

    private static readonly string[] WhirlwindFieldNames =
    {
        "whirlingBladeColor",
        "whirlingBladeDuration",
        "whirlingBladeSpinDegrees",
        "whirlingBladeLineWidth",
        "whirlingBladeCenterOffset",
        "whirlingBladeUpwardDrift",
        "whirlingBladeVerticalWave"
    };

    private static readonly string[] CrescentFieldNames =
    {
        "crescentSlashColor",
        "crescentSlashVfxDuration",
        "crescentSlashLineWidth",
        "crescentSlashCenterOffset"
    };

    private static bool GetFold(string key, bool defaultExpanded = true)
    {
        return EditorPrefs.GetBool(FoldPrefsRoot + key, defaultExpanded);
    }

    private static void SetFold(string key, bool value)
    {
        EditorPrefs.SetBool(FoldPrefsRoot + key, value);
    }

    private static void DrawFoldoutPropertyBlock(SerializedObject so, string foldKey, string title, string[] fieldNames)
    {
        bool open = GetFold(foldKey);
        bool newOpen = EditorGUILayout.Foldout(open, title, true);
        if (newOpen != open)
            SetFold(foldKey, newOpen);
        open = newOpen;

        if (!open)
            return;

        EditorGUI.indentLevel++;
        foreach (string name in fieldNames)
        {
            SerializedProperty p = so.FindProperty(name);
            if (p != null)
                EditorGUILayout.PropertyField(p, true);
        }

        EditorGUI.indentLevel--;
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        SerializedProperty scriptProp = serializedObject.FindProperty("m_Script");
        if (scriptProp != null)
            EditorGUILayout.PropertyField(scriptProp);

        EditorGUILayout.Space(4f);
        EditorGUILayout.LabelField("Refs", EditorStyles.boldLabel);
        foreach (string name in RefFieldNames)
        {
            SerializedProperty p = serializedObject.FindProperty(name);
            if (p != null)
                EditorGUILayout.PropertyField(p);
        }

        EditorGUILayout.Space(4f);
        EditorGUILayout.LabelField("Global Cooldown", EditorStyles.boldLabel);
        SerializedProperty gcd = serializedObject.FindProperty("globalCooldownSeconds");
        if (gcd != null)
            EditorGUILayout.PropertyField(gcd);

        EditorGUILayout.Space(6f);

        DrawFoldoutPropertyBlock(serializedObject, "PowerSlashVfx", "Power Slash VFX", PowerSlashFieldNames);
        EditorGUILayout.Space(2f);
        DrawFoldoutPropertyBlock(serializedObject, "WhirlwindVfx", "Whirlwind VFX", WhirlwindFieldNames);
        EditorGUILayout.Space(2f);
        DrawFoldoutPropertyBlock(serializedObject, "CrescentSlashVfx", "Crescent Slash VFX", CrescentFieldNames);
        EditorGUILayout.Space(2f);

        bool sfOpen = GetFold("SoulforgedWeaponMinionVfx");
        bool sfNew = EditorGUILayout.Foldout(sfOpen, "Soulforged Weapon Minion VFX", true);
        if (sfNew != sfOpen)
            SetFold("SoulforgedWeaponMinionVfx", sfNew);
        sfOpen = sfNew;
        if (sfOpen)
        {
            EditorGUI.indentLevel++;
            SerializedProperty sf = serializedObject.FindProperty("soulforgedWeaponMinionPresentation");
            if (sf != null)
                EditorGUILayout.PropertyField(sf, true);
            EditorGUI.indentLevel--;
        }

        serializedObject.ApplyModifiedProperties();
    }
}
#endif
