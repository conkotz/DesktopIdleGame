#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Tabbed, level-sorted foldouts for <see cref="PlayerAbilityVfxController"/>.
/// </summary>
[CustomEditor(typeof(PlayerAbilityVfxController))]
[CanEditMultipleObjects]
public class PlayerAbilityVfxControllerEditor : Editor
{
    private const string FoldPrefsRoot = "PlayerAbilityVfxController.Foldouts.";
    private const string TabPrefsKey = FoldPrefsRoot + "SelectedSkillTab";

    private enum VfxSkillTab
    {
        Melee = 0,
        Magic = 1,
        Range = 2,
        Woodcutting = 3,
        Fishing = 4,
        Mining = 5
    }

    private static readonly string[] TabLabels =
    {
        "Melee",
        "Magic",
        "Range",
        "Woodcutting",
        "Fishing",
        "Mining"
    };

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

    private static readonly string[] CrusaderStrikeFieldNames =
    {
        "crusaderStrikeBeamOuterColor",
        "crusaderStrikeBeamInnerColor",
        "crusaderStrikeBeamFinalColor",
        "crusaderStrikeBeamDuration",
        "crusaderStrikeBeamSkyHeight",
        "crusaderStrikeBeamBehindPlayerX",
        "crusaderStrikeBeamBehindPlayerY",
        "crusaderStrikeBeamPointSize",
        "crusaderStrikeBeamTrailTime",
        "crusaderStrikeBeamTrailWidth",
        "crusaderStrikeBeamCenterOffset",
        "crusaderStrikeBeamFinalSizeScale"
    };

    private static readonly string[] ParryFieldNames =
    {
        "parrySlashColor",
        "parrySlashLineWidth",
        "parrySlashDuration",
        "parrySlashHeightOffset"
    };

    private static readonly string[] WhirlwindFieldNames =
    {
        "whirlingBladeColor",
        "whirlingBladeDuration",
        "whirlingBladeSpinDegrees",
        "whirlingBladeLineWidth",
        "whirlingBladeCenterOffset",
        "whirlingBladeSlashCount",
        "whirlingBladeLaneBaseYOffsets",
        "whirlingBladeFifthLaneBaseYOffset",
        "whirlingBladeLaneOrbitHeights",
        "whirlingBladeFifthLaneOrbitHeight",
        "whirlingBladeSpawnVerticalJitter",
        "whirlingBladeMinYOffset",
        "whirlingBladeMaxYOffset"
    };

    private static readonly string[] GaleforceTwisterFieldNames =
    {
        "galeforceTwisterColor",
        "galeforceTwisterDuration",
        "galeforceTwisterSpinDegrees",
        "galeforceTwisterLineWidthScale",
        "galeforceTwisterCenterOffset",
        "galeforceTwisterSlashCount",
        "galeforceTwisterLaneBaseYOffsets",
        "galeforceTwisterFifthLaneBaseYOffset",
        "galeforceTwisterLaneOrbitHeights",
        "galeforceTwisterFifthLaneOrbitHeight",
        "galeforceTwisterSizeScale",
        "galeforceTwisterLifetimeSeconds",
        "galeforceTwisterLingerAfterChannelSeconds",
        "galeforceTwisterDriftSpeed",
        "galeforceTwisterMinDirectionSeconds",
        "galeforceTwisterMaxDirectionSeconds"
    };

    private static readonly string[] CrescentFieldNames =
    {
        "crescentSlashColor",
        "crescentSlashVfxDuration",
        "crescentSlashLineWidth",
        "crescentSlashCenterOffset"
    };

    private static readonly string[] GuardiansHammerFieldNames =
    {
        "guardiansHammerSprite",
        "guardiansHammerHammerTint",
        "guardiansHammerShockwaveColor",
        "guardiansHammerBurnFlareColor",
        "guardiansHammerPivotOffset",
        "guardiansHammerOrbitRadius",
        "guardiansHammerSwingDuration",
        "guardiansHammerStartAngle",
        "guardiansHammerEndAngle",
        "guardiansHammerWorldScale",
        "guardiansHammerShockwaveGroundOffset",
        "guardiansHammerShockwaveDuration",
        "guardiansHammerShockwaveLineWidth",
        "guardiansHammerShockwaveVerticalScale",
        "guardiansHammerBurnFlareDuration",
        "guardiansHammerBurnFlareRadius"
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

    private static readonly string[] SoulforgedWeaponFieldNames =
    {
        "soulforgedWeaponMinionPresentation"
    };

    private static readonly string[] WarBannerVfxFieldNames =
    {
        "warBannerSprite",
        "warBannerTint",
        "warBannerWorldScale",
        "warBannerSpawnHeightAboveTarget",
        "warBannerSpawnHoldSeconds",
        "warBannerMinimumHeightAboveTarget",
        "warBannerGroundYOffset",
        "warBannerSortingLayer",
        "warBannerSortingOrderOffsetFromPlayer",
        "warBannerSortingOrder"
    };

    private static readonly string[] HammerTempestVfxFieldNames =
    {
        "hammerTempestHammerSprite",
        "hammerTempestHammerTint",
        "hammerTempestCenterOffset",
        "hammerTempestOrbitRadius",
        "hammerTempestWorldScale",
        "hammerTempestMainOrbitDegreesPerSecond",
        "hammerTempestHammerSpinDegreesPerSecond",
        "hammerTempestRingRadiusInset",
        "hammerTempestRingColor",
        "hammerTempestRingEdgeOpacity"
    };

    private static readonly string[] AshenRebirthVfxFieldNames =
    {
        "ashenRebirthPhoenixSprite",
        "ashenRebirthPhoenixTint",
        "ashenRebirthPhoenixWorldScale",
        "ashenRebirthPhoenixSpawnHeightAbovePlayer",
        "ashenRebirthPhoenixSpawnHoldSeconds",
        "ashenRebirthPhoenixSortingLayer",
        "ashenRebirthPhoenixSortingOrder"
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
        "executionersDescentSpawnHeightAboveTarget",
        "executionersDescentSpawnHoldSeconds",
        "executionersDescentMinimumHeightAboveTarget",
        "executionersDescentSortingLayer",
        "executionersDescentSortingOrder",
        "executionersDescentShockwaveDuration",
        "executionersDescentShockwaveMaxRadius",
        "executionersDescentShockwaveLineWidth",
        "executionersDescentShockwaveGroundOffset"
    };

    private static readonly string[] BladestormVfxFieldNames =
    {
        "bladestormHitSlashColor",
        "bladestormHitSlashLineWidth",
        "bladestormHitSlashDuration",
        "bladestormHitSlashCenterOffset",
        "bladestormHitSlashSpawnJitter",
        "bladestormHitSlashAngleJitterDegrees",
        "bladestormHitSlashHalfLength",
        "bladestormFinaleSlashWidthScale",
        "bladestormFinaleSlashDurationScale",
        "bladestormFinaleSlashLengthScale"
    };

    private static readonly string[] GatheringFrenzySharedFieldNames =
    {
        "lumberFrenzyOrbitVfxPrefab",
        "lumberFrenzyOrbitVfxLocalOffset",
        "gatheringFrenzyBehindDistance"
    };

    private static readonly string[] LumberFrenzyFieldNames =
    {
        "lumberFrenzyOrbitVfxColor"
    };

    private static readonly string[] FishingFrenzyFieldNames =
    {
        "fishingFrenzyOrbitVfxColor"
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

    private readonly struct VfxFoldoutSection
    {
        public readonly VfxSkillTab Tab;
        public readonly string FoldKey;
        public readonly string AbilityName;
        public readonly int Level;
        public readonly string[] FieldNames;
        public readonly string[] ExtraFieldNames;

        public VfxFoldoutSection(
            VfxSkillTab tab,
            string foldKey,
            int level,
            string abilityName,
            string[] fieldNames,
            string[] extraFieldNames = null)
        {
            Tab = tab;
            FoldKey = foldKey;
            Level = level;
            AbilityName = abilityName;
            FieldNames = fieldNames;
            ExtraFieldNames = extraFieldNames;
        }
    }

    private static readonly VfxFoldoutSection[] AllSections =
    {
        new(VfxSkillTab.Melee, "PowerSlashVfx", 5, "Power Slash", PowerSlashFieldNames),
        new(VfxSkillTab.Melee, "CrusaderStrikeVfx", 5, "Crusader Strike", CrusaderStrikeFieldNames),
        new(VfxSkillTab.Melee, "ParryVfx", 10, "Parry", ParryFieldNames),
        new(VfxSkillTab.Melee, "WhirlwindVfx", 15, "Whirlwind", WhirlwindFieldNames),
        new(VfxSkillTab.Melee, "GaleforceTwisterVfx", 18, "Whirlwind — Galeforce Twisters", GaleforceTwisterFieldNames),
        new(VfxSkillTab.Melee, "CrescentSlashVfx", 15, "Crescent Slash", CrescentFieldNames),
        new(VfxSkillTab.Melee, "GuardiansHammerVfx", 15, "Guardian's Hammer", GuardiansHammerFieldNames),
        new(VfxSkillTab.Melee, "ShadowStrikeVfx", 25, "Shadow Strike", ShadowStrikeVfxFieldNames),
        new(VfxSkillTab.Melee, "EnergyInfusionVfx", 25, "Energy Infusion / Arcane Battery", EnergyInfusionVfxFieldNames),
        new(VfxSkillTab.Melee, "FlameChargeVfx", 25, "Flame Charge", FlameChargeVfxFieldNames),
        new(VfxSkillTab.Melee, "SoulforgedWeaponMinionVfx", 35, "Soulforged Weapon", SoulforgedWeaponFieldNames),
        new(VfxSkillTab.Melee, "WarBannerVfx", 35, "War Banner", WarBannerVfxFieldNames),
        new(VfxSkillTab.Melee, "HammerTempestVfx", 35, "Hammer Tempest", HammerTempestVfxFieldNames),
        new(VfxSkillTab.Melee, "AshenRebirthVfx", 40, "Phoenix Soul — Ashen Rebirth", AshenRebirthVfxFieldNames),
        new(VfxSkillTab.Melee, "FinalSeveranceVfx", 45, "Final Severance", FinalSeveranceVfxFieldNames),
        new(VfxSkillTab.Melee, "ExecutionersDescentVfx", 45, "Executioner's Descent", ExecutionersDescentVfxFieldNames),
        new(VfxSkillTab.Melee, "BladestormVfx", 45, "Bladestorm", BladestormVfxFieldNames),

        new(VfxSkillTab.Woodcutting, "LumberFrenzyVfx", 5, "Lumber Frenzy",
            LumberFrenzyFieldNames, GatheringFrenzySharedFieldNames),
        new(VfxSkillTab.Woodcutting, "CleavingChopVfx", 25, "Cleaving Chop", CleavingChopFieldNames),
        new(VfxSkillTab.Woodcutting, "SpectralAxeVfx", 25, "Spectral Axe", SpectralAxeFieldNames),
        new(VfxSkillTab.Woodcutting, "SpectralAxeTrailVfx", 25, "Spectral Axe — Blue Trail", SpectralAxeTrailFieldNames),
        new(VfxSkillTab.Woodcutting, "AvatarOfTheForestVfx", 45, "Avatar of the Forest", AvatarOfTheForestVfxFieldNames),

        new(VfxSkillTab.Fishing, "FishingFrenzyVfx", 5, "Fishing Frenzy",
            FishingFrenzyFieldNames, GatheringFrenzySharedFieldNames)
    };

    private static bool GetFold(string key, bool defaultExpanded = true)
    {
        return EditorPrefs.GetBool(FoldPrefsRoot + key, defaultExpanded);
    }

    private static void SetFold(string key, bool value)
    {
        EditorPrefs.SetBool(FoldPrefsRoot + key, value);
    }

    private static string FormatSectionTitle(VfxFoldoutSection section) =>
        $"Lv{section.Level} — {section.AbilityName}";

    private static void DrawFoldoutPropertyBlock(SerializedObject so, VfxFoldoutSection section)
    {
        bool open = GetFold(section.FoldKey);
        bool newOpen = EditorGUILayout.Foldout(open, FormatSectionTitle(section), true);
        if (newOpen != open)
            SetFold(section.FoldKey, newOpen);
        open = newOpen;

        if (!open)
            return;

        EditorGUI.indentLevel++;
        DrawFieldNameList(so, section.ExtraFieldNames);
        DrawFieldNameList(so, section.FieldNames);
        EditorGUI.indentLevel--;
    }

    private static void DrawFieldNameList(SerializedObject so, string[] fieldNames)
    {
        if (fieldNames == null || fieldNames.Length == 0)
            return;

        foreach (string name in fieldNames)
        {
            SerializedProperty p = so.FindProperty(name);
            if (p != null)
                EditorGUILayout.PropertyField(p, true);
        }
    }

    private static void DrawSkillTab(SerializedObject so, VfxSkillTab tab)
    {
        VfxFoldoutSection[] sections = AllSections
            .Where(s => s.Tab == tab)
            .OrderBy(s => s.Level)
            .ThenBy(s => s.AbilityName, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (sections.Length == 0)
        {
            EditorGUILayout.HelpBox(
                "No ability VFX fields for this skill yet. New VFX will appear here when added to PlayerAbilityVfxController.",
                MessageType.Info);
            return;
        }

        for (int i = 0; i < sections.Length; i++)
        {
            DrawFoldoutPropertyBlock(so, sections[i]);
            if (i < sections.Length - 1)
                EditorGUILayout.Space(2f);
        }
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

        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("Ability VFX", EditorStyles.boldLabel);

        int tabIndex = EditorPrefs.GetInt(TabPrefsKey, 0);
        tabIndex = GUILayout.Toolbar(tabIndex, TabLabels);
        EditorPrefs.SetInt(TabPrefsKey, tabIndex);

        EditorGUILayout.Space(4f);
        DrawSkillTab(serializedObject, (VfxSkillTab)Mathf.Clamp(tabIndex, 0, TabLabels.Length - 1));

        serializedObject.ApplyModifiedProperties();
    }
}
#endif
