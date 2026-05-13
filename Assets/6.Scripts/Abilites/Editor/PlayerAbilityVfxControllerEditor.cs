#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// Foldout groups for <see cref="PlayerAbilityVfxController"/> (moved from Player Ability Controller).
/// </summary>
[CustomEditor(typeof(PlayerAbilityVfxController))]
[CanEditMultipleObjects]
public class PlayerAbilityVfxControllerEditor : Editor
{
    private const string FoldPrefsRoot = "PlayerAbilityVfxController.Foldouts.";

    private static readonly string[] RefFieldNames =
    {
        "player",
        "combat"
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

    private static readonly string[] LumberFrenzyVfxFieldNames =
    {
        "lumberFrenzyOrbitVfxPrefab",
        "lumberFrenzyOrbitVfxLocalOffset",
        "lumberFrenzyOrbitRadius",
        "lumberFrenzySweepPeriodSeconds",
        "lumberFrenzyOrbitVfxColor"
    };

    private static readonly string[] CleavingChopFieldNames =
    {
        "cleavingChopShowRangeIndicator",
        "cleavingChopIndicatorColor",
        "cleavingChopIndicatorSegments",
        "cleavingChopIndicatorLineWidth",
        "cleavingChopIndicatorSortingOrder",
        "cleavingChopIndicatorSortingLayer"
    };

    private static readonly string[] SpectralAxeFieldNames =
    {
        "spectralAxeTint",
        "spectralAxeVisualLift",
        "spectralAxeSpinDegreesPerSecond",
        "spectralAxeSpinClockwise",
        "spectralAxeTravelSpeedUnitsPerSecond",
        "spectralAxeShowAreaIndicator",
        "spectralAxeAreaIndicatorColor",
        "spectralAxeAreaIndicatorSegments",
        "spectralAxeAreaIndicatorLineWidth",
        "spectralAxeAreaIndicatorSortingOrder",
        "spectralAxeAreaIndicatorSortingLayer"
    };

    private static readonly string[] SpectralAxeTrailFieldNames =
    {
        "spectralAxeTrailLifetimeSeconds",
        "spectralAxeTrailStartWidth",
        "spectralAxeTrailEndWidth",
        "spectralAxeTrailStartAlpha",
        "spectralAxeTrailAnchorXFrac",
        "spectralAxeTrailAnchorYFrac",
        "spectralAxeTrailColorStart",
        "spectralAxeTrailColorEnd"
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

        EditorGUILayout.Space(6f);

        DrawFoldoutPropertyBlock(serializedObject, "PowerSlashVfx", "Power Slash VFX", PowerSlashFieldNames);
        EditorGUILayout.Space(2f);
        DrawFoldoutPropertyBlock(serializedObject, "WhirlwindVfx", "Whirlwind VFX", WhirlwindFieldNames);
        EditorGUILayout.Space(2f);
        DrawFoldoutPropertyBlock(serializedObject, "CrescentSlashVfx", "Crescent Slash VFX", CrescentFieldNames);
        EditorGUILayout.Space(2f);
        DrawFoldoutPropertyBlock(serializedObject, "LumberFrenzyVfx", "Lumber Frenzy (Woodcutting Lv5) VFX", LumberFrenzyVfxFieldNames);
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
        EditorGUILayout.Space(2f);

        DrawFoldoutPropertyBlock(serializedObject, "CleavingChopVfx", "Cleaving Chop VFX", CleavingChopFieldNames);
        EditorGUILayout.Space(2f);
        DrawFoldoutPropertyBlock(serializedObject, "SpectralAxeVfx", "Spectral Axe VFX", SpectralAxeFieldNames);
        EditorGUILayout.Space(2f);
        DrawFoldoutPropertyBlock(serializedObject, "SpectralAxeTrailVfx", "Spectral Axe Blue Trail", SpectralAxeTrailFieldNames);

        serializedObject.ApplyModifiedProperties();
    }
}
#endif
