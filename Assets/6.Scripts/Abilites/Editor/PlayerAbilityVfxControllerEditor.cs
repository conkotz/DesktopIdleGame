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
        "combat",
        "runtimeParticleMaterialTemplate"
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
        "whirlingBladeSlashCount",
        "whirlingBladeOrbitVerticalScale",
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

    private static readonly string[] ShadowStrikeVfxFieldNames =
    {
        "shadowStrikeBurstColor",
        "shadowStrikeBurstDuration",
        "shadowStrikeBurstRadius",
        "shadowStrikeBurstLineWidth",
        "shadowStrikeBurstOffset",
        "shadowStrikeDepartSmokeColor",
        "shadowStrikeDepartSmokeOffset",
        "shadowStrikeDepartSmokeLingerSeconds",
        "shadowStrikeDepartSmokeBurstCount",
        "shadowStrikeDepartSmokeWispEmitSeconds"
    };

    private static readonly string[] ExecutionersDescentVfxFieldNames =
    {
        "executionersDescentAxeSprite",
        "executionersDescentMarkSprite",
        "executionersDescentShockwaveSprite",
        "executionersDescentAxeTint",
        "executionersDescentMarkTint",
        "executionersDescentShockwaveColor",
        "executionersDescentAxeWorldScale",
        "executionersDescentMarkWorldScale",
        "executionersDescentMarkOffset",
        "executionersDescentHangHeightAboveTarget",
        "executionersDescentSpawnHeightAboveHang",
        "executionersDescentSpawnHoldSeconds",
        "executionersDescentDropDurationSeconds",
        "executionersDescentSortingLayer",
        "executionersDescentSortingOrder",
        "executionersDescentShockwaveDuration",
        "executionersDescentShockwaveMaxRadius",
        "executionersDescentShockwaveLineWidth",
        "executionersDescentShockwaveGroundOffset"
    };

    private static readonly string[] FinalSeveranceVfxFieldNames =
    {
        "finalSeveranceWindupStartColor",
        "finalSeveranceWindupEndColor",
        "finalSeveranceStrikeStartColor",
        "finalSeveranceStrikeEndColor",
        "finalSeveranceWindupVfxDuration",
        "finalSeveranceStrikeVfxDuration",
        "finalSeveranceLineWidth",
        "finalSeveranceCenterOffset",
        "finalSeveranceDiagonalRise",
        "finalSeveranceDiagonalDrop",
        "finalSeveranceStrikeDiagonalRise",
        "finalSeveranceStrikeDiagonalDrop"
    };

    private static readonly string[] LumberFrenzyVfxFieldNames =
    {
        "lumberFrenzyOrbitVfxPrefab",
        "lumberFrenzyOrbitVfxLocalOffset",
        "lumberFrenzyOrbitRadius",
        "lumberFrenzySweepPeriodSeconds",
        "lumberFrenzyOrbitVfxColor"
    };

    private static readonly string[] AvatarOfTheForestVfxFieldNames =
    {
        "avatarOfForestGlowLocalOffset",
        "avatarOfForestGlowColor",
        "avatarOfForestGlowSphereRadius",
        "avatarOfForestGlowConeAngle",
        "avatarOfForestGlowEmissionRate",
        "avatarOfForestParticleStartSizeMin",
        "avatarOfForestParticleStartSizeMax",
        "avatarOfForestTrailLifetime",
        "avatarOfForestTrailWidth"
    };

    private static readonly string[] EnergyInfusionVfxFieldNames =
    {
        "energyInfusionGlowLocalOffset",
        "energyInfusionGlowColor",
        "energyInfusionGlowSphereRadius",
        "energyInfusionGlowEmissionRate",
        "energyInfusionParticleStartSizeMin",
        "energyInfusionParticleStartSizeMax"
    };

    private static readonly string[] FlameChargeVfxFieldNames =
    {
        "flameChargePlayerGlowColor",
        "flameChargePlayerGlowLocalOffset",
        "flameChargePlayerGlowRadius",
        "flameChargePlayerGlowEmissionRate",
        "flameChargeGroundFireColor",
        "flameChargeGroundFireRadius",
        "flameChargeGroundFireEmissionRate",
        "flameChargeVolcanicBurstColor",
        "flameChargeVolcanicBurstDuration",
        "flameChargeVolcanicBurstMaxRadius",
        "flameChargeGroundFloorYOffset",
        "flameChargeGroundTrailHeight",
        "flameChargeGroundSortingLayer",
        "flameChargeGroundSortingOrder"
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

        DrawFoldoutPropertyBlock(serializedObject, "PowerSlashVfx", "Power Slash (Melee) VFX", PowerSlashFieldNames);
        EditorGUILayout.Space(2f);
        DrawFoldoutPropertyBlock(serializedObject, "WhirlwindVfx", "Whirlwind (Melee) VFX", WhirlwindFieldNames);
        EditorGUILayout.Space(2f);
        DrawFoldoutPropertyBlock(serializedObject, "CrescentSlashVfx", "Crescent Slash (Melee) VFX", CrescentFieldNames);
        EditorGUILayout.Space(2f);
        DrawFoldoutPropertyBlock(
            serializedObject,
            "ShadowStrikeVfx",
            "Shadow Strike (Melee Lv25) VFX",
            ShadowStrikeVfxFieldNames);
        EditorGUILayout.Space(2f);
        DrawFoldoutPropertyBlock(
            serializedObject,
            "EnergyInfusionVfx",
            "Energy Infusion / Arcane Battery (Melee Lv25) VFX",
            EnergyInfusionVfxFieldNames);
        EditorGUILayout.Space(2f);
        DrawFoldoutPropertyBlock(
            serializedObject,
            "FlameChargeVfx",
            "Flame Charge (Melee Lv25) VFX",
            FlameChargeVfxFieldNames);
        EditorGUILayout.Space(2f);
        DrawFoldoutPropertyBlock(
            serializedObject,
            "ExecutionersDescentVfx",
            "Executioner's Descent (Melee Lv45) VFX",
            ExecutionersDescentVfxFieldNames);
        EditorGUILayout.Space(2f);
        DrawFoldoutPropertyBlock(
            serializedObject,
            "FinalSeveranceVfx",
            "Final Severance (Melee Lv45) VFX",
            FinalSeveranceVfxFieldNames);
        EditorGUILayout.Space(2f);
        DrawFoldoutPropertyBlock(serializedObject, "LumberFrenzyVfx", "Lumber Frenzy (Woodcutting Lv5) VFX", LumberFrenzyVfxFieldNames);
        EditorGUILayout.Space(2f);
        DrawFoldoutPropertyBlock(serializedObject, "AvatarOfTheForestVfx", "Avatar of the Forest (Woodcutting Lv45) VFX", AvatarOfTheForestVfxFieldNames);
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

        DrawFoldoutPropertyBlock(serializedObject, "CleavingChopVfx", "Cleaving Chop (Woodcutting Lv25) VFX", CleavingChopFieldNames);
        EditorGUILayout.Space(2f);
        DrawFoldoutPropertyBlock(serializedObject, "SpectralAxeVfx", "Spectral Axe (Woodcutting Lv25) VFX", SpectralAxeFieldNames);
        EditorGUILayout.Space(2f);
        DrawFoldoutPropertyBlock(serializedObject, "SpectralAxeTrailVfx", "Spectral Axe Blue Trail (Woodcutting Lv25)", SpectralAxeTrailFieldNames);

        serializedObject.ApplyModifiedProperties();
    }
}
#endif
