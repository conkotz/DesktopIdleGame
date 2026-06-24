using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(ItemDefinition))]
public class ItemDefinitionEditor : Editor
{
    // Core
    private SerializedProperty itemKind;
    private SerializedProperty maxStack, itemId, displayName, icon, description, rarity, value;
    private SerializedProperty equipSlot, handVisualKey, usedUpgradeSlots;

    // Visuals
    private SerializedProperty heldSprite;
    private SerializedProperty equippedSprite;
    private SerializedProperty equippedLocalOffset;
    private SerializedProperty equippedLocalRotationZ;
    private SerializedProperty useCustomEquippedPose;
    private SerializedProperty equippedFlipX;
    private SerializedProperty equippedFlipY;

    // Modules
    private SerializedProperty weaponStats;
    private SerializedProperty combatSupportStats;
    private SerializedProperty toolStats;
    private SerializedProperty armourStats;
    private SerializedProperty consumableStats;
    private SerializedProperty enhancementScrollStats;
    private SerializedProperty mapEnhancementStats;
    private SerializedProperty cookableStats;

    // Bonuses
    private SerializedProperty bonusStats;
    private SerializedProperty randomStatPool;
    private SerializedProperty miscEffects;

    private int _expandedStatPickerEntryIndex = -1;
    private string _statPickerSearch = "";
    private Vector2 _randomStatPoolScroll;
    private Vector2 _statPickerScroll;

    private void OnEnable()
    {
        itemKind = serializedObject.FindProperty("itemKind");

        maxStack = serializedObject.FindProperty("maxStack");
        itemId = serializedObject.FindProperty("itemId");
        displayName = serializedObject.FindProperty("displayName");
        icon = serializedObject.FindProperty("icon");
        description = serializedObject.FindProperty("description");
        rarity = serializedObject.FindProperty("rarity");
        value = serializedObject.FindProperty("value");

        equipSlot = serializedObject.FindProperty("equipSlot");
        handVisualKey = serializedObject.FindProperty("handVisualKey");
        usedUpgradeSlots = serializedObject.FindProperty("usedUpgradeSlots");

        heldSprite = serializedObject.FindProperty("heldSprite");
        equippedSprite = serializedObject.FindProperty("equippedSprite");
        equippedLocalOffset = serializedObject.FindProperty("equippedLocalOffset");
        equippedLocalRotationZ = serializedObject.FindProperty("equippedLocalRotationZ");
        useCustomEquippedPose = serializedObject.FindProperty("useCustomEquippedPose");
        equippedFlipX = serializedObject.FindProperty("equippedFlipX");
        equippedFlipY = serializedObject.FindProperty("equippedFlipY");

        weaponStats = serializedObject.FindProperty("weaponStats");
        combatSupportStats = serializedObject.FindProperty("combatSupportStats");
        toolStats = serializedObject.FindProperty("toolStats");
        armourStats = serializedObject.FindProperty("armourStats");
        consumableStats = serializedObject.FindProperty("consumableStats");
        enhancementScrollStats = serializedObject.FindProperty("enhancementScrollStats");
        mapEnhancementStats = serializedObject.FindProperty("mapEnhancementStats");
        cookableStats = serializedObject.FindProperty("cookableStats");

        bonusStats = serializedObject.FindProperty("bonusStats");
        randomStatPool = serializedObject.FindProperty("randomStatPool");
        miscEffects = serializedObject.FindProperty("miscEffects");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.LabelField("Classification", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(itemKind);

        var kind = (ItemKind)itemKind.enumValueIndex;

        EditorGUILayout.Space(6);
        EditorGUILayout.LabelField("Stacking", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(maxStack);

        EditorGUILayout.Space(6);
        EditorGUILayout.LabelField("Identity", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(itemId);

        EditorGUILayout.Space(6);
        EditorGUILayout.LabelField("Display", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(displayName);
        EditorGUILayout.PropertyField(icon);

        EditorGUILayout.Space(6);
        EditorGUILayout.LabelField("Details", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(description);
        EditorGUILayout.PropertyField(rarity);

        EditorGUILayout.Space(6);
        EditorGUILayout.LabelField("Economy", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(value);

        bool showEquipmentSection = kind != ItemKind.EnhancementScroll && kind != ItemKind.MapEnhancement;
        var slot = EquipSlot.None;

        if (showEquipmentSection)
        {
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Equipment", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(equipSlot);

            slot = (EquipSlot)equipSlot.enumValueIndex;

            DrawEquipSlotHint(kind, slot);
            EnforceSlotRules(kind, equipSlot, ref slot);
        }
        else
        {
            if (equipSlot != null)
                equipSlot.enumValueIndex = (int)EquipSlot.None;
            if (handVisualKey != null)
                handVisualKey.enumValueIndex = (int)ToolKey.None;
        }

        bool hasUpgradeSlots =
            kind == ItemKind.Weapon ||
            kind == ItemKind.Armour ||
            kind == ItemKind.Tool;

        if (hasUpgradeSlots && usedUpgradeSlots != null)
        {
            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("Upgrades", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(
                usedUpgradeSlots,
                new GUIContent("Used Upgrade Slots", "Current filled slots. Max slots are derived from item type and equipment tier."));
            if (usedUpgradeSlots.intValue < 0)
                usedUpgradeSlots.intValue = 0;
        }
        else if (usedUpgradeSlots != null)
        {
            usedUpgradeSlots.intValue = 0;
        }

        bool showHandVisualKey =
            slot == EquipSlot.MainHand &&
            (kind == ItemKind.Weapon || kind == ItemKind.Tool);

        if (showHandVisualKey)
        {
            EditorGUILayout.PropertyField(handVisualKey, new GUIContent("Hand Visual Key"));
        }
        else
        {
            if (handVisualKey != null)
                handVisualKey.enumValueIndex = (int)ToolKey.None;
        }

        bool showHeldVisualSprite =
            (slot == EquipSlot.MainHand && (kind == ItemKind.Weapon || kind == ItemKind.Tool))
            || (slot == EquipSlot.OffHand && kind == ItemKind.CombatSupport);

        if (showHeldVisualSprite)
        {
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Held Visual", EditorStyles.boldLabel);

            if (heldSprite != null)
            {
                EditorGUILayout.PropertyField(
                    heldSprite,
                    new GUIContent("Held Sprite", "Optional sprite used only when held. Leave empty to use Icon.")
                );

                EditorGUILayout.HelpBox(
                    "Use Held Sprite for items that need a different orientation when held. Leave empty to use Icon.",
                    MessageType.Info
                );
            }
        }

        bool isMainHandVisualItem =
            (slot == EquipSlot.MainHand && (kind == ItemKind.Weapon || kind == ItemKind.Tool));

        bool isOffHandSupportVisualItem =
            (slot == EquipSlot.OffHand && kind == ItemKind.CombatSupport);

        bool isArmourVisualItem =
            kind == ItemKind.Armour &&
            (slot == EquipSlot.Helmet || slot == EquipSlot.Body || slot == EquipSlot.Boots);

        bool showEquippedVisual =
            isMainHandVisualItem || isOffHandSupportVisualItem || isArmourVisualItem;

        if (showEquippedVisual)
        {
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Equipped Visual", EditorStyles.boldLabel);

            if (equippedSprite != null)
            {
                EditorGUILayout.PropertyField(
                    equippedSprite,
                    new GUIContent("Equipped Sprite", "Sprite shown on the player while equipped. Leave empty to fall back to Held Sprite.")
                );
            }

            bool supportsCustomPose = isMainHandVisualItem || isOffHandSupportVisualItem;

            if (supportsCustomPose)
            {
                if (useCustomEquippedPose != null)
                {
                    EditorGUILayout.PropertyField(
                        useCustomEquippedPose,
                        new GUIContent("Use Custom Equipped Pose", "Enable this only for items that need custom held position/rotation/flip.")
                    );
                }

                if (useCustomEquippedPose != null && useCustomEquippedPose.boolValue)
                {
                    if (equippedLocalOffset != null)
                    {
                        EditorGUILayout.PropertyField(
                            equippedLocalOffset,
                            new GUIContent("Equipped Offset", "Small local X/Y adjustment for the equipped visual.")
                        );
                    }

                    if (equippedLocalRotationZ != null)
                    {
                        EditorGUILayout.PropertyField(
                            equippedLocalRotationZ,
                            new GUIContent("Equipped Rotation Z", "Local Z rotation for the equipped visual.")
                        );
                    }

                    if (equippedFlipX != null)
                    {
                        EditorGUILayout.PropertyField(
                            equippedFlipX,
                            new GUIContent("Flip X", "Flip the equipped sprite horizontally.")
                        );
                    }

                    if (equippedFlipY != null)
                    {
                        EditorGUILayout.PropertyField(
                            equippedFlipY,
                            new GUIContent("Flip Y", "Flip the equipped sprite vertically.")
                        );
                    }
                }
            }
            else
            {
                if (equippedLocalOffset != null)
                {
                    EditorGUILayout.PropertyField(
                        equippedLocalOffset,
                        new GUIContent("Equipped Offset", "Small local X/Y adjustment for the equipped visual.")
                    );
                }
            }

            EditorGUILayout.HelpBox(
                "Use Equipped Visual settings to control how the item sits on the player while equipped.",
                MessageType.Info
            );
        }

        EditorGUILayout.Space(12);

        if (kind == ItemKind.Weapon)
        {
            DrawWeaponStatsBlock();
            DrawBonusBlockIfPresent("Bonus Stats (optional)", show: true);
            DrawRandomStatPoolBlock();
            DrawMiscEffectsBlockIfPresent(show: true);
        }
        else if (kind == ItemKind.CombatSupport)
        {
            DrawCombatSupportStatsBlock();
            DrawMiscEffectsBlockIfPresent(show: true);
        }
        else if (kind == ItemKind.Tool)
        {
            DrawModuleHeader("Tool Stats");
            DrawToolStatsBlock();
            DrawBonusBlockIfPresent("Bonus Stats (optional)", show: true);
            DrawMiscEffectsBlockIfPresent(show: true);
        }
        else if (kind == ItemKind.Armour)
        {
            DrawModuleHeader("Armour Stats");
            EditorGUILayout.PropertyField(armourStats, includeChildren: true);
            DrawBonusBlockIfPresent("Bonus Stats (Armour Extras)", show: true);
            DrawRandomStatPoolBlock();
            DrawMiscEffectsBlockIfPresent(show: true);
        }
        else if (kind == ItemKind.Jewelry)
        {
            DrawBonusBlockIfPresent("Bonus Stats (Jewelry)", show: true);
            DrawRandomStatPoolBlock();
            DrawMiscEffectsBlockIfPresent(show: true);
        }
        else if (kind == ItemKind.Consumable)
        {
            DrawConsumableStatsBlock();
        }
        else if (kind == ItemKind.EnhancementScroll)
        {
            DrawEnhancementScrollStatsBlock();
        }
        else if (kind == ItemKind.MapEnhancement)
        {
            DrawMapEnhancementItemStatsBlock();
        }
        else
        {
            DrawBonusBlockIfPresent("Bonus Stats", show: false);
        }

        if (kind == ItemKind.Resource || kind == ItemKind.Consumable)
            DrawCookableStatsBlock();

        serializedObject.ApplyModifiedProperties();
        if (GUI.changed)
            EditorUtility.SetDirty(target);
    }

    private void DrawWeaponStatsBlock()
    {
        DrawModuleHeader("Weapon Stats");

        if (weaponStats == null)
        {
            EditorGUILayout.HelpBox("weaponStats property not found.", MessageType.Error);
            return;
        }

        SerializedProperty minPhysicalDamage = weaponStats.FindPropertyRelative("minPhysicalDamage");
        SerializedProperty maxPhysicalDamage = weaponStats.FindPropertyRelative("maxPhysicalDamage");

        SerializedProperty minFireDamage = weaponStats.FindPropertyRelative("minFireDamage");
        SerializedProperty maxFireDamage = weaponStats.FindPropertyRelative("maxFireDamage");
        SerializedProperty minIceDamage = weaponStats.FindPropertyRelative("minIceDamage");
        SerializedProperty maxIceDamage = weaponStats.FindPropertyRelative("maxIceDamage");
        SerializedProperty minLightningDamage = weaponStats.FindPropertyRelative("minLightningDamage");
        SerializedProperty maxLightningDamage = weaponStats.FindPropertyRelative("maxLightningDamage");

        SerializedProperty minCorruptionDamage = weaponStats.FindPropertyRelative("minCorruptionDamage");
        SerializedProperty maxCorruptionDamage = weaponStats.FindPropertyRelative("maxCorruptionDamage");

        SerializedProperty attacksPerSecond = weaponStats.FindPropertyRelative("attacksPerSecond");
        SerializedProperty critChance = weaponStats.FindPropertyRelative("critChance");
        SerializedProperty critMultiplier = weaponStats.FindPropertyRelative("critMultiplier");
        SerializedProperty handedness = weaponStats.FindPropertyRelative("handedness");
        SerializedProperty attackRange = weaponStats.FindPropertyRelative("attackRange");
        SerializedProperty attackSkill = weaponStats.FindPropertyRelative("attackSkill");
        SerializedProperty mainHandArchetype = weaponStats.FindPropertyRelative("mainHandArchetype");
        SerializedProperty rangedBowType = weaponStats.FindPropertyRelative("rangedBowType");
        SerializedProperty magicAttackType = weaponStats.FindPropertyRelative("magicAttackType");
        SerializedProperty manaCostPerAttack = weaponStats.FindPropertyRelative("manaCostPerAttack");
        SerializedProperty magicAilmentApplyChance = weaponStats.FindPropertyRelative("magicAilmentApplyChance");
        SerializedProperty canEquipInOffHand = weaponStats.FindPropertyRelative("canEquipInOffHand");

        SerializedProperty requiresOffhandSupport = weaponStats.FindPropertyRelative("requiresOffhandSupport");
        SerializedProperty requiredSupportType = weaponStats.FindPropertyRelative("requiredSupportType");
        SerializedProperty weaponEquipmentTier = weaponStats.FindPropertyRelative("equipmentTier");
        SerializedProperty weaponWeight = weaponStats.FindPropertyRelative("weaponWeight");

        EditorGUILayout.LabelField("Damage", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(minPhysicalDamage, new GUIContent("Min Physical Damage"));
        EditorGUILayout.PropertyField(maxPhysicalDamage, new GUIContent("Max Physical Damage"));

        EditorGUILayout.LabelField("Elemental (total = magic hit; Magic % scales sum)", EditorStyles.miniLabel);
        EditorGUILayout.PropertyField(minFireDamage, new GUIContent("Min Fire Damage"));
        EditorGUILayout.PropertyField(maxFireDamage, new GUIContent("Max Fire Damage"));
        EditorGUILayout.PropertyField(minIceDamage, new GUIContent("Min Ice Damage"));
        EditorGUILayout.PropertyField(maxIceDamage, new GUIContent("Max Ice Damage"));
        EditorGUILayout.PropertyField(minLightningDamage, new GUIContent("Min Lightning Damage"));
        EditorGUILayout.PropertyField(maxLightningDamage, new GUIContent("Max Lightning Damage"));

        EditorGUILayout.PropertyField(minCorruptionDamage, new GUIContent("Min Corruption Damage"));
        EditorGUILayout.PropertyField(maxCorruptionDamage, new GUIContent("Max Corruption Damage"));

        ClampMinMax(minPhysicalDamage, maxPhysicalDamage);
        ClampMinMax(minFireDamage, maxFireDamage);
        ClampMinMax(minIceDamage, maxIceDamage);
        ClampMinMax(minLightningDamage, maxLightningDamage);
        ClampMinMax(minCorruptionDamage, maxCorruptionDamage);

        EditorGUILayout.Space(6);
        EditorGUILayout.LabelField("Speed", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(attacksPerSecond);

        EditorGUILayout.Space(6);
        EditorGUILayout.LabelField("Critical", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(critChance);
        EditorGUILayout.PropertyField(critMultiplier);

        EditorGUILayout.Space(6);
        EditorGUILayout.LabelField("Handling", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(handedness);
        EditorGUILayout.PropertyField(attackRange);
        EditorGUILayout.PropertyField(attackSkill);
        if (mainHandArchetype != null)
            EditorGUILayout.PropertyField(mainHandArchetype, new GUIContent("Main-hand Archetype"));
        if (attackSkill != null &&
            (AttackSkill)attackSkill.enumValueIndex == AttackSkill.Ranged &&
            rangedBowType != null)
        {
            EditorGUILayout.PropertyField(
                rangedBowType,
                new GUIContent(
                    "Ranged bow type",
                    "Swiftbow: default targeting. Longbow: auto-battle picks the furthest living enemy on the map first."));
        }

        if (weaponEquipmentTier != null)
        {
            EditorGUILayout.PropertyField(
                weaponEquipmentTier,
                new GUIContent(
                    "Equipment Tier",
                    "Tier 1–5 (display name is on the item). Gated by matching combat skill: L1 / L10 / L20 / L30 / L50."
                )
            );
        }

        if (weaponWeight != null)
        {
            EditorGUILayout.PropertyField(
                weaponWeight,
                new GUIContent(
                    "Weapon Weight",
                    "Light: daggers, swiftbows, wands. Medium: swords, spears, maces. Heavy: polearms, longbows. " +
                    "Enhancement flat damage and ailment multiplier scrolls scale by weight."));
        }

        if (attackSkill != null &&
            (AttackSkill)attackSkill.enumValueIndex == AttackSkill.Magic &&
            magicAttackType != null)
        {
            EditorGUILayout.PropertyField(magicAttackType, new GUIContent("Magic Type"));
            if (manaCostPerAttack != null)
                EditorGUILayout.PropertyField(manaCostPerAttack, new GUIContent("Mana Cost Per Attack"));
            if (magicAilmentApplyChance != null)
                EditorGUILayout.PropertyField(magicAilmentApplyChance, new GUIContent("Magic Ailment Apply Chance"));

            // Convenience: show elemental scaling bonuses here as well (stored in BonusStats).
            if (bonusStats != null)
            {
                SerializedProperty burnExplosionMultiplierBonus = bonusStats.FindPropertyRelative("burnExplosionMultiplierBonus");
                SerializedProperty chillSlowPerStackBonus = bonusStats.FindPropertyRelative("chillSlowPerStackBonus");
                SerializedProperty shockDamageTakenMultiplierBonus = bonusStats.FindPropertyRelative("shockDamageTakenMultiplierBonus");

                EditorGUILayout.Space(4);
                EditorGUILayout.LabelField("Magic Ailment Scaling (Bonus Stats)", EditorStyles.boldLabel);
                if (burnExplosionMultiplierBonus != null)
                    EditorGUILayout.PropertyField(burnExplosionMultiplierBonus, new GUIContent("Burn Multiplier"));
                if (chillSlowPerStackBonus != null)
                    EditorGUILayout.PropertyField(chillSlowPerStackBonus, new GUIContent("Chill Effect"));
                if (shockDamageTakenMultiplierBonus != null)
                    EditorGUILayout.PropertyField(shockDamageTakenMultiplierBonus, new GUIContent("Shock Effect"));
            }
        }
        EditorGUILayout.PropertyField(canEquipInOffHand);

        EditorGUILayout.Space(6);
        EditorGUILayout.LabelField("Support Requirement", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(requiresOffhandSupport);

        if (requiresOffhandSupport != null && requiresOffhandSupport.boolValue)
        {
            EditorGUILayout.PropertyField(requiredSupportType);

            if (requiredSupportType != null &&
                (CombatSupportType)requiredSupportType.enumValueIndex == CombatSupportType.None)
            {
                EditorGUILayout.HelpBox(
                    "This weapon requires offhand support, but Required Support Type is None.",
                    MessageType.Warning
                );
            }
        }
        else if (requiredSupportType != null)
        {
            requiredSupportType.enumValueIndex = (int)CombatSupportType.None;
        }

        EditorGUILayout.HelpBox(
            "Weapons can now deal Physical, Magic, and/or Corruption damage at the same time.\n" +
            "Examples:\n" +
            "- Sword: Physical only\n" +
            "- Wand: Magic only\n" +
            "- Hybrid blade: Physical + Magic\n" +
            "- Rare cursed weapon: includes Corruption damage\n\n" +
            "Use Support Requirement for weapons like:\n" +
            "- Bow -> Arrows\n" +
            "- Staff -> Runes",
            MessageType.None
        );
    }

    private void DrawCombatSupportStatsBlock()
    {
        DrawModuleHeader("Combat Support Stats");

        if (combatSupportStats == null)
        {
            EditorGUILayout.HelpBox("combatSupportStats property not found.", MessageType.Error);
            return;
        }

        SerializedProperty supportType = combatSupportStats.FindPropertyRelative("supportType");
        SerializedProperty requiredMainHandArchetype = combatSupportStats.FindPropertyRelative("requiredMainHandArchetype");

        SerializedProperty bonusPhysicalDamage = combatSupportStats.FindPropertyRelative("bonusPhysicalDamage");
        SerializedProperty bonusMagicDamage = combatSupportStats.FindPropertyRelative("bonusMagicDamage");
        SerializedProperty bonusCorruptionDamage = combatSupportStats.FindPropertyRelative("bonusCorruptionDamage");

        SerializedProperty critChanceBonus = combatSupportStats.FindPropertyRelative("critChanceBonus");
        SerializedProperty critMultiplierBonus = combatSupportStats.FindPropertyRelative("critMultiplierBonus");
        SerializedProperty attackSpeedPercent = combatSupportStats.FindPropertyRelative("attackSpeedPercent");

        SerializedProperty globalPhysicalDamagePercentCs = combatSupportStats.FindPropertyRelative("globalPhysicalDamagePercent");
        SerializedProperty rangedPhysicalDamagePercentCs = combatSupportStats.FindPropertyRelative("rangedPhysicalDamagePercent");
        SerializedProperty magicDamagePercentCs = combatSupportStats.FindPropertyRelative("magicDamagePercent");
        SerializedProperty fireDamagePercent = combatSupportStats.FindPropertyRelative("fireDamagePercent");
        SerializedProperty iceDamagePercent = combatSupportStats.FindPropertyRelative("iceDamagePercent");
        SerializedProperty coldDamagePercent = combatSupportStats.FindPropertyRelative("coldDamagePercent");
        SerializedProperty corruptionDamagePercent = combatSupportStats.FindPropertyRelative("corruptionDamagePercent");

        SerializedProperty consumableOnAttack = combatSupportStats.FindPropertyRelative("consumableOnAttack");
        SerializedProperty consumeAmountPerAttack = combatSupportStats.FindPropertyRelative("consumeAmountPerAttack");

        EditorGUILayout.LabelField("Type", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(supportType);
        if (requiredMainHandArchetype != null)
            EditorGUILayout.PropertyField(requiredMainHandArchetype, new GUIContent("Required Main-hand Archetype"));

        EditorGUILayout.Space(6);
        EditorGUILayout.LabelField("Bonuses", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(bonusPhysicalDamage);
        EditorGUILayout.PropertyField(bonusMagicDamage);
        EditorGUILayout.PropertyField(bonusCorruptionDamage);
        EditorGUILayout.PropertyField(critChanceBonus);
        EditorGUILayout.PropertyField(critMultiplierBonus);
        EditorGUILayout.PropertyField(attackSpeedPercent);

        EditorGUILayout.Space(6);
        EditorGUILayout.LabelField("Damage % (0.1 = +10%)", EditorStyles.boldLabel);
        PropertyField(globalPhysicalDamagePercentCs, "Physical damage %");
        PropertyField(rangedPhysicalDamagePercentCs, "Ranged damage %");
        PropertyField(magicDamagePercentCs, "Magic %");
        PropertyField(fireDamagePercent, "Fire damage %");
        PropertyField(iceDamagePercent, "Ice damage %");
        PropertyField(coldDamagePercent, "Cold damage %");
        PropertyField(corruptionDamagePercent, "Corruption damage %");

        EditorGUILayout.Space(6);
        EditorGUILayout.LabelField("Consumption", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(consumableOnAttack);

        if (consumableOnAttack != null && consumableOnAttack.boolValue)
        {
            EditorGUILayout.PropertyField(consumeAmountPerAttack);
            if (consumeAmountPerAttack != null && consumeAmountPerAttack.intValue < 1)
                consumeAmountPerAttack.intValue = 1;
        }
        else if (consumeAmountPerAttack != null)
        {
            consumeAmountPerAttack.intValue = 0;
        }

        EditorGUILayout.HelpBox(
            "Combat Support items are offhand requirements for certain weapons.\n\n" +
            "Examples:\n" +
            "- Arrows for bows\n" +
            "- Runes for staffs\n" +
            "- Focus for special magic weapons\n\n" +
            "These can also add bonus damage, crit, or attack speed.",
            MessageType.None
        );
    }

    private void DrawToolStatsBlock()
    {
        if (toolStats == null)
        {
            EditorGUILayout.HelpBox("toolStats property not found.", MessageType.Error);
            return;
        }

        SerializedProperty toolType = toolStats.FindPropertyRelative("toolType");
        SerializedProperty toolEquipmentTier = toolStats.FindPropertyRelative("equipmentTier");
        SerializedProperty gatherSpeedMultiplier = toolStats.FindPropertyRelative("gatherSpeedMultiplier");
        SerializedProperty gatheringGrit = toolStats.FindPropertyRelative("gatheringGrit");
        SerializedProperty bonusResourceFindChance = toolStats.FindPropertyRelative("bonusResourceFindChance");
        SerializedProperty staminaEfficiency = toolStats.FindPropertyRelative("staminaEfficiency");

        EditorGUILayout.PropertyField(toolType, new GUIContent("Tool Type"));
        if (toolEquipmentTier != null)
        {
            EditorGUILayout.PropertyField(
                toolEquipmentTier,
                new GUIContent(
                    "Equipment Tier",
                    "Tier 1–5 (display name is on the item). Gated by Woodcutting / Mining / Fishing: L1 / L10 / L20 / L30 / L50."
                )
            );
        }

        EditorGUILayout.PropertyField(gatherSpeedMultiplier, new GUIContent("Gather Speed Multiplier"));
        EditorGUILayout.PropertyField(gatheringGrit, new GUIContent("Gathering Grit"));
        EditorGUILayout.PropertyField(bonusResourceFindChance, new GUIContent("Bonus Resource Find Chance"));
        EditorGUILayout.PropertyField(staminaEfficiency, new GUIContent("Stamina Efficiency"));

        if (gatherSpeedMultiplier != null && gatherSpeedMultiplier.floatValue <= 0f)
            gatherSpeedMultiplier.floatValue = 1f;
        if (bonusResourceFindChance != null && bonusResourceFindChance.floatValue < 0f)
            bonusResourceFindChance.floatValue = 0f;

        EditorGUILayout.HelpBox(
            "Tool gathering model:\n" +
            "- Gather Speed Multiplier: gather tick speed scaling\n" +
            "- Gathering Grit: chance to double BASE resource only\n" +
            "- Bonus Resource Find Chance: multiplier to bonus drop chance\n" +
            "- Stamina Efficiency: future stamina cost reduction",
            MessageType.None
        );
    }

    private void DrawConsumableStatsBlock()
    {
        DrawModuleHeader("Consumable Stats");

        if (consumableStats == null)
        {
            EditorGUILayout.HelpBox("consumableStats property not found.", MessageType.Error);
            return;
        }

        SerializedProperty consumableType = consumableStats.FindPropertyRelative("consumableType");
        SerializedProperty healAmount = consumableStats.FindPropertyRelative("healAmount");
        SerializedProperty energyAmount = consumableStats.FindPropertyRelative("energyAmount");
        SerializedProperty cooldownSeconds = consumableStats.FindPropertyRelative("cooldownSeconds");
        SerializedProperty consumeOnUse = consumableStats.FindPropertyRelative("consumeOnUse");
        SerializedProperty grantedEffect = consumableStats.FindPropertyRelative("grantedEffect");
        SerializedProperty openableLoot = consumableStats.FindPropertyRelative("openableLoot");
        SerializedProperty openRequiredAmount = consumableStats.FindPropertyRelative("openRequiredAmount");
        SerializedProperty baitTier = consumableStats.FindPropertyRelative("baitTier");
        SerializedProperty fishingSpeedPercentBonus = consumableStats.FindPropertyRelative("fishingSpeedPercentBonus");
        SerializedProperty mapEnhancementTier = consumableStats.FindPropertyRelative("mapEnhancementTier");
        SerializedProperty mapEnhancementModRolls = consumableStats.FindPropertyRelative("mapEnhancementModRolls");

        EditorGUILayout.PropertyField(consumableType);
        EditorGUILayout.Space(4);

        ConsumableType selectedType = consumableType != null
            ? (ConsumableType)consumableType.enumValueIndex
            : ConsumableType.None;

        bool isOpenable = selectedType == ConsumableType.Openable;
        bool isFishingBait = selectedType == ConsumableType.FishingBait;
        bool isMapEnhancement = selectedType == ConsumableType.MapEnhancement;

        // Heal / Energy / Granted Effect only matter for Food / Potion. Hiding them on Openable keeps the
        // inspector focused on the loot table for that mode.
        if (isFishingBait)
        {
            EditorGUILayout.LabelField("Fishing Bait", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(baitTier);
            EditorGUILayout.PropertyField(
                fishingSpeedPercentBonus,
                new GUIContent("Fishing Speed Bonus %", "Applied as +X% fishing speed while this bait is consumed for a swing."));
            if (fishingSpeedPercentBonus != null && fishingSpeedPercentBonus.floatValue < 0f)
                fishingSpeedPercentBonus.floatValue = 0f;
        }
        else if (isMapEnhancement)
        {
            EditorGUILayout.HelpBox(
                "Map enhancements now use Item Kind = Map Enhancement. Use Tools → Items → Migrate Legacy Map Enhancement Items to convert this asset.",
                MessageType.Warning);
            EditorGUILayout.LabelField("Map Enhancement (legacy consumable)", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(
                mapEnhancementTier,
                new GUIContent(
                    "Tier",
                    "Tier 1 rolls 1 permanent modifier when dropped on a map. Tier 2 rolls 2 modifiers."));
            EditorGUILayout.Space(4);
            DrawMapEnhancementModRolls(mapEnhancementModRolls);
            EditorGUILayout.HelpBox(
                "Template item only. When dropped as special loot on a combat map, a rolled instance is created " +
                "with the map name appended (e.g. \"Spider Lair Map Enhancement\") and random modifiers.",
                MessageType.Info);
        }
        else if (!isOpenable)
        {
            EditorGUILayout.LabelField("Use", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(healAmount);
            EditorGUILayout.PropertyField(energyAmount);
            EditorGUILayout.PropertyField(cooldownSeconds);
            EditorGUILayout.PropertyField(consumeOnUse);

            if (healAmount != null && healAmount.intValue < 0) healAmount.intValue = 0;
            if (energyAmount != null && energyAmount.intValue < 0) energyAmount.intValue = 0;
            if (cooldownSeconds != null && cooldownSeconds.floatValue < 0f) cooldownSeconds.floatValue = 0f;

            if (selectedType == ConsumableType.Potion)
            {
                EditorGUILayout.Space(6);
                EditorGUILayout.LabelField("Granted Effect", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(grantedEffect, includeChildren: true);
            }
            else if (selectedType == ConsumableType.Food)
            {
                EditorGUILayout.Space(6);
                EditorGUILayout.LabelField("Timed Food Buffs", EditorStyles.boldLabel);
                SerializedProperty foodEffectDurationSeconds =
                    consumableStats.FindPropertyRelative("foodEffectDurationSeconds");
                SerializedProperty foodEnableRegen = consumableStats.FindPropertyRelative("foodEnableRegen");
                SerializedProperty foodRegenTotalHeal = consumableStats.FindPropertyRelative("foodRegenTotalHeal");
                SerializedProperty foodEnableSwiftness = consumableStats.FindPropertyRelative("foodEnableSwiftness");
                SerializedProperty foodSwiftnessPercentBonus =
                    consumableStats.FindPropertyRelative("foodSwiftnessPercentBonus");
                SerializedProperty foodEnableOverheal = consumableStats.FindPropertyRelative("foodEnableOverheal");
                SerializedProperty foodOverhealMaxAboveMaxHp =
                    consumableStats.FindPropertyRelative("foodOverhealMaxAboveMaxHp");
                SerializedProperty foodOverhealInstantHeal =
                    consumableStats.FindPropertyRelative("foodOverhealInstantHeal");
                SerializedProperty foodEnableFocused = consumableStats.FindPropertyRelative("foodEnableFocused");
                SerializedProperty foodFocusedDamageBonusFraction =
                    consumableStats.FindPropertyRelative("foodFocusedDamageBonusFraction");

                EditorGUILayout.PropertyField(
                    foodEffectDurationSeconds,
                    new GUIContent(
                        "Effect Duration (s)",
                        "How long enabled toggles last on the buff bar. Can be shorter or longer than cooldown; re-use refreshes each buff type."));

                if (foodEffectDurationSeconds != null && foodEffectDurationSeconds.floatValue < 0f)
                    foodEffectDurationSeconds.floatValue = 0f;

                EditorGUILayout.Space(4);
                EditorGUILayout.PropertyField(foodEnableRegen, new GUIContent("Regen (HoT)"));
                if (foodEnableRegen != null && foodEnableRegen.boolValue)
                    EditorGUILayout.PropertyField(
                        foodRegenTotalHeal,
                        new GUIContent(
                            "Regen Total Heal",
                            "Total HP restored evenly over Effect Duration (separate from instant Heal Amount above)."));
                if (foodRegenTotalHeal != null && foodRegenTotalHeal.intValue < 0)
                    foodRegenTotalHeal.intValue = 0;

                EditorGUILayout.Space(4);
                EditorGUILayout.PropertyField(foodEnableSwiftness, new GUIContent("Swiftness"));
                if (foodEnableSwiftness != null && foodEnableSwiftness.boolValue)
                    EditorGUILayout.PropertyField(
                        foodSwiftnessPercentBonus,
                        new GUIContent("Move Speed +%", "Percent bonus to move speed while active (e.g. 10 = +10%)."));
                if (foodSwiftnessPercentBonus != null && foodSwiftnessPercentBonus.floatValue < 0f)
                    foodSwiftnessPercentBonus.floatValue = 0f;

                EditorGUILayout.Space(4);
                EditorGUILayout.PropertyField(foodEnableOverheal, new GUIContent("Overheal"));
                if (foodEnableOverheal != null && foodEnableOverheal.boolValue)
                {
                    EditorGUILayout.PropertyField(
                        foodOverhealMaxAboveMaxHp,
                        new GUIContent(
                            "Max HP Above Max",
                            "While active, effective max HP is MaxHP + this value (HP bar fill stays capped at real max)."));
                    EditorGUILayout.PropertyField(
                        foodOverhealInstantHeal,
                        new GUIContent(
                            "Instant Overheal Heal",
                            "Extra heal on use that can use the overheal ceiling (0 = cap only)."));
                }

                if (foodOverhealMaxAboveMaxHp != null && foodOverhealMaxAboveMaxHp.intValue < 0)
                    foodOverhealMaxAboveMaxHp.intValue = 0;
                if (foodOverhealInstantHeal != null && foodOverhealInstantHeal.intValue < 0)
                    foodOverhealInstantHeal.intValue = 0;

                EditorGUILayout.Space(4);
                EditorGUILayout.PropertyField(foodEnableFocused, new GUIContent("Focused"));
                if (foodEnableFocused != null && foodEnableFocused.boolValue)
                    EditorGUILayout.PropertyField(
                        foodFocusedDamageBonusFraction,
                        new GUIContent(
                            "Min/Max Hit Bonus",
                            "Fraction added to basic-attack min and max damage (0 = default +15%)."));

                if (foodFocusedDamageBonusFraction != null && foodFocusedDamageBonusFraction.floatValue < 0f)
                    foodFocusedDamageBonusFraction.floatValue = 0f;
            }
        }
        else
        {
            DrawOpenableLootTable(openableLoot, openRequiredAmount);
        }

        EditorGUILayout.HelpBox(
            "Consumables can be assigned to the action bar and used by hotkey.\n\n" +
            "Food: instant heal/energy (above) plus optional timed buffs (Regen, Swiftness, Overheal, Focused) when Effect Duration > 0.\n" +
            "Potion: can heal, restore energy, and/or apply a temporary effect.\n" +
            "Fishing Bait: consumed automatically while fishing; higher bait tier is prioritized first.\n" +
            "Openable: double-click the item to open it. Each loot row rolls independently using its own % chance. " +
            "Required Amount To Open controls how many copies are consumed per open (e.g. 5 shards → 1 open).",
            MessageType.None
        );
    }

    private void DrawMapEnhancementItemStatsBlock()
    {
        DrawModuleHeader("Map Enhancement Stats");

        if (mapEnhancementStats == null)
        {
            EditorGUILayout.HelpBox("mapEnhancementStats property not found.", MessageType.Error);
            return;
        }

        SerializedProperty tier = mapEnhancementStats.FindPropertyRelative("tier");
        SerializedProperty modRolls = mapEnhancementStats.FindPropertyRelative("modRolls");

        EditorGUILayout.PropertyField(
            tier,
            new GUIContent(
                "Tier",
                "Tier 1 rolls 1 permanent modifier when dropped on a map. Tier 2 rolls 2 modifiers."));
        EditorGUILayout.Space(4);
        DrawMapEnhancementModRolls(modRolls);
        EditorGUILayout.HelpBox(
            "Template item only. When dropped as special loot on a combat map, a rolled instance is created " +
            "with the map name appended (e.g. \"Spider Lair Map Enhancement\") and random modifiers.",
            MessageType.Info);
    }

    private static void DrawMapEnhancementModRolls(SerializedProperty modRolls)
    {
        EditorGUILayout.LabelField("Modifier Rolls", EditorStyles.boldLabel);

        if (modRolls == null)
        {
            EditorGUILayout.HelpBox("mapEnhancementModRolls property not found.", MessageType.Error);
            return;
        }

        if (modRolls.arraySize == 0)
        {
            EditorGUILayout.HelpBox(
                "No modifier rows configured. Use Reset to Defaults or add entries below.",
                MessageType.Warning);
        }

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Reset Modifier Defaults", GUILayout.Height(22f)))
        {
            MapEnhancementModRollConfig[] defaults = MapEnhancementRollDefaults.CreateDefaultRollConfigs();
            modRolls.arraySize = defaults.Length;
            for (int i = 0; i < defaults.Length; i++)
            {
                SerializedProperty element = modRolls.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("modType").enumValueIndex = (int)defaults[i].modType;
                element.FindPropertyRelative("rollWeight").floatValue = defaults[i].rollWeight;
                element.FindPropertyRelative("minValue").floatValue = defaults[i].minValue;
                element.FindPropertyRelative("maxValue").floatValue = defaults[i].maxValue;
            }
        }

        if (GUILayout.Button("Add Missing Mod Types", GUILayout.Height(22f)))
            EnsureAllMapEnhancementModTypesPresent(modRolls);
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(4);

        for (int i = 0; i < modRolls.arraySize; i++)
            DrawMapEnhancementModRollRow(modRolls.GetArrayElementAtIndex(i), i);

        EditorGUILayout.Space(2);
        EditorGUILayout.HelpBox(
            "Roll Weight controls how often each modifier is picked (relative to others). " +
            "Set weight to 0 to disable a modifier. " +
            "Respawn uses seconds; extra spawns uses count; loot/gold/damage/elite health use fraction (0.10 = 10%). " +
            "Elite spawn chance uses relative bonus (0.10 = +10% of base chance). Elite double uses flat chance (0.05 = 5%).",
            MessageType.None);
    }

    private static void EnsureAllMapEnhancementModTypesPresent(SerializedProperty modRolls)
    {
        MapEnhancementModType[] allTypes = MapEnhancementRollDefaults.AllModTypes;
        for (int t = 0; t < allTypes.Length; t++)
        {
            MapEnhancementModType type = allTypes[t];
            bool found = false;
            for (int i = 0; i < modRolls.arraySize; i++)
            {
                SerializedProperty element = modRolls.GetArrayElementAtIndex(i);
                if ((MapEnhancementModType)element.FindPropertyRelative("modType").enumValueIndex == type)
                {
                    found = true;
                    break;
                }
            }

            if (found)
                continue;

            int index = modRolls.arraySize;
            modRolls.InsertArrayElementAtIndex(index);
            SerializedProperty row = modRolls.GetArrayElementAtIndex(index);
            MapEnhancementModRollConfig[] defaults = MapEnhancementRollDefaults.CreateDefaultRollConfigs();
            for (int d = 0; d < defaults.Length; d++)
            {
                if (defaults[d].modType != type)
                    continue;

                row.FindPropertyRelative("modType").enumValueIndex = (int)defaults[d].modType;
                row.FindPropertyRelative("rollWeight").floatValue = defaults[d].rollWeight;
                row.FindPropertyRelative("minValue").floatValue = defaults[d].minValue;
                row.FindPropertyRelative("maxValue").floatValue = defaults[d].maxValue;
                break;
            }
        }
    }

    private static void DrawMapEnhancementModRollRow(SerializedProperty row, int index)
    {
        if (row == null)
            return;

        SerializedProperty modType = row.FindPropertyRelative("modType");
        SerializedProperty rollWeight = row.FindPropertyRelative("rollWeight");
        SerializedProperty minValue = row.FindPropertyRelative("minValue");
        SerializedProperty maxValue = row.FindPropertyRelative("maxValue");

        MapEnhancementModType type = modType != null
            ? (MapEnhancementModType)modType.enumValueIndex
            : MapEnhancementModType.LootBonus;

        string title = GetMapEnhancementModLabel(type);
        EditorGUILayout.LabelField(title, EditorStyles.boldLabel);

        EditorGUI.indentLevel++;
        EditorGUILayout.PropertyField(rollWeight, new GUIContent("Roll Weight"));

        switch (type)
        {
            case MapEnhancementModType.RespawnTimeReduction:
                EditorGUILayout.PropertyField(minValue, new GUIContent("Min Seconds"));
                EditorGUILayout.PropertyField(maxValue, new GUIContent("Max Seconds"));
                break;
            case MapEnhancementModType.ExtraEnemySpawns:
                EditorGUILayout.PropertyField(minValue, new GUIContent("Min Extra Spawns"));
                EditorGUILayout.PropertyField(maxValue, new GUIContent("Max Extra Spawns"));
                break;
            case MapEnhancementModType.LootBonus:
                EditorGUILayout.PropertyField(minValue, new GUIContent("Min Loot Bonus (fraction)"));
                EditorGUILayout.PropertyField(maxValue, new GUIContent("Max Loot Bonus (fraction)"));
                break;
            case MapEnhancementModType.EnemyDamageReduction:
                EditorGUILayout.PropertyField(minValue, new GUIContent("Min Damage Reduction (fraction)"));
                EditorGUILayout.PropertyField(maxValue, new GUIContent("Max Damage Reduction (fraction)"));
                break;
            case MapEnhancementModType.GoldBonus:
                EditorGUILayout.PropertyField(minValue, new GUIContent("Min Gold Bonus (fraction)"));
                EditorGUILayout.PropertyField(maxValue, new GUIContent("Max Gold Bonus (fraction)"));
                break;
            case MapEnhancementModType.EliteSpawnChanceBonus:
                EditorGUILayout.PropertyField(minValue, new GUIContent("Min Relative Bonus (fraction)"));
                EditorGUILayout.PropertyField(maxValue, new GUIContent("Max Relative Bonus (fraction)"));
                break;
            case MapEnhancementModType.EliteSpawnDouble:
                EditorGUILayout.PropertyField(minValue, new GUIContent("Min Double Chance (fraction)"));
                EditorGUILayout.PropertyField(maxValue, new GUIContent("Max Double Chance (fraction)"));
                break;
            case MapEnhancementModType.EliteHealthReduction:
                EditorGUILayout.PropertyField(minValue, new GUIContent("Min Health Reduction (fraction)"));
                EditorGUILayout.PropertyField(maxValue, new GUIContent("Max Health Reduction (fraction)"));
                break;
        }

        if (rollWeight != null && rollWeight.floatValue < 0f)
            rollWeight.floatValue = 0f;

        if (minValue != null && maxValue != null && maxValue.floatValue < minValue.floatValue)
            maxValue.floatValue = minValue.floatValue;

        EditorGUI.indentLevel--;
        EditorGUILayout.Space(6);
    }

    private static string GetMapEnhancementModLabel(MapEnhancementModType type)
    {
        return type switch
        {
            MapEnhancementModType.RespawnTimeReduction => "Respawn Time Reduction",
            MapEnhancementModType.ExtraEnemySpawns => "Extra Enemy Spawns",
            MapEnhancementModType.LootBonus => "Loot Bonus",
            MapEnhancementModType.EnemyDamageReduction => "Enemy Damage Reduction",
            MapEnhancementModType.GoldBonus => "Gold Bonus",
            MapEnhancementModType.EliteSpawnChanceBonus => "Elite Spawn Chance Bonus",
            MapEnhancementModType.EliteSpawnDouble => "Elite Spawn Double",
            MapEnhancementModType.EliteHealthReduction => "Elite Health Reduction",
            _ => type.ToString()
        };
    }

    private static void DrawOpenableLootTable(SerializedProperty openableLoot, SerializedProperty openRequiredAmount)
    {
        EditorGUILayout.LabelField("Open Requirements", EditorStyles.boldLabel);

        if (openRequiredAmount != null)
        {
            EditorGUILayout.PropertyField(
                openRequiredAmount,
                new GUIContent(
                    "Required Amount To Open",
                    "Minimum amount in the stack needed to open this item, and the amount consumed each open. " +
                    "1 behaves like a normal lootbox; e.g. set to 5 for 'combine 5 Shards into a reward'."));

            if (openRequiredAmount.intValue < 1)
                openRequiredAmount.intValue = 1;

            EditorGUILayout.Space(4);
        }

        EditorGUILayout.LabelField("Loot Table", EditorStyles.boldLabel);

        if (openableLoot == null)
        {
            EditorGUILayout.HelpBox(
                "openableLoot property missing — re-import the script.",
                MessageType.Error);
            return;
        }

        int requiredAmount = openRequiredAmount != null ? Mathf.Max(1, openRequiredAmount.intValue) : 1;
        string requiredAmountLine = requiredAmount > 1
            ? $"\nThis item requires {requiredAmount} in a stack per open — that many are consumed at once."
            : string.Empty;

        EditorGUILayout.HelpBox(
            "Each row is rolled independently when the player double-clicks the item.\n" +
            "100% = guaranteed drop, 25% = rolls about 1 in 4 opens. Set Min/Max Amount for a stack range." +
            requiredAmountLine,
            MessageType.Info);

        for (int i = 0; i < openableLoot.arraySize; i++)
        {
            SerializedProperty entry = openableLoot.GetArrayElementAtIndex(i);
            SerializedProperty itemId = entry.FindPropertyRelative("itemId");
            SerializedProperty chancePercent = entry.FindPropertyRelative("chancePercent");
            SerializedProperty minAmount = entry.FindPropertyRelative("minAmount");
            SerializedProperty maxAmount = entry.FindPropertyRelative("maxAmount");

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField($"Drop #{i + 1}", EditorStyles.miniBoldLabel, GUILayout.Width(60f));
                    EditorGUILayout.LabelField(BuildOpenableRowPreview(itemId, chancePercent, minAmount, maxAmount), EditorStyles.miniLabel);
                    GUILayout.FlexibleSpace();

                    using (new EditorGUI.DisabledScope(i == 0))
                    {
                        if (GUILayout.Button(new GUIContent("▲", "Move this drop up in the list."), GUILayout.Width(22f)))
                        {
                            openableLoot.MoveArrayElement(i, i - 1);
                            return;
                        }
                    }
                    using (new EditorGUI.DisabledScope(i == openableLoot.arraySize - 1))
                    {
                        if (GUILayout.Button(new GUIContent("▼", "Move this drop down in the list."), GUILayout.Width(22f)))
                        {
                            openableLoot.MoveArrayElement(i, i + 1);
                            return;
                        }
                    }

                    if (GUILayout.Button("✕", GUILayout.Width(22f)))
                    {
                        openableLoot.DeleteArrayElementAtIndex(i);
                        return;
                    }
                }

                EditorGUILayout.PropertyField(itemId, new GUIContent("Item ID"));
                EditorGUILayout.PropertyField(chancePercent, new GUIContent("Chance %"));
                EditorGUILayout.PropertyField(
                    minAmount,
                    new GUIContent("Min Amount", "Lowest quantity granted when this row rolls. Increase to drop multiple of this item per open."));
                EditorGUILayout.PropertyField(
                    maxAmount,
                    new GUIContent("Max Amount", "Highest quantity granted when this row rolls. Set equal to Min for a fixed amount."));

                if (chancePercent != null)
                    chancePercent.floatValue = Mathf.Clamp(chancePercent.floatValue, 0f, 100f);
                if (minAmount != null && minAmount.intValue < 1)
                    minAmount.intValue = 1;
                if (maxAmount != null && minAmount != null && maxAmount.intValue < minAmount.intValue)
                    maxAmount.intValue = minAmount.intValue;

                if (itemId != null && string.IsNullOrWhiteSpace(itemId.stringValue))
                {
                    EditorGUILayout.HelpBox(
                        "Item ID is empty — this row will be skipped at runtime.",
                        MessageType.Warning);
                }
            }

            EditorGUILayout.Space(2);
        }

        if (GUILayout.Button("+ Add Drop"))
        {
            int newIndex = openableLoot.arraySize;
            openableLoot.InsertArrayElementAtIndex(newIndex);
            SerializedProperty added = openableLoot.GetArrayElementAtIndex(newIndex);
            SerializedProperty addedItemId = added.FindPropertyRelative("itemId");
            SerializedProperty addedChance = added.FindPropertyRelative("chancePercent");
            SerializedProperty addedMin = added.FindPropertyRelative("minAmount");
            SerializedProperty addedMax = added.FindPropertyRelative("maxAmount");
            if (addedItemId != null) addedItemId.stringValue = string.Empty;
            if (addedChance != null) addedChance.floatValue = 100f;
            if (addedMin != null) addedMin.intValue = 1;
            if (addedMax != null) addedMax.intValue = 1;
        }
    }

    /// <summary>
    /// Compact at-a-glance summary of a loot row drawn next to the "Drop #N" header. Helps see whether each row
    /// produces a single item or a stack range, and at what % chance — without having to open every row.
    /// </summary>
    private static string BuildOpenableRowPreview(
        SerializedProperty itemId,
        SerializedProperty chancePercent,
        SerializedProperty minAmount,
        SerializedProperty maxAmount)
    {
        string id = itemId != null ? itemId.stringValue : string.Empty;
        if (string.IsNullOrWhiteSpace(id))
            id = "(no item)";

        int lo = minAmount != null ? Mathf.Max(1, minAmount.intValue) : 1;
        int hi = maxAmount != null ? Mathf.Max(lo, maxAmount.intValue) : lo;
        float pct = chancePercent != null ? Mathf.Clamp(chancePercent.floatValue, 0f, 100f) : 100f;

        string amountText = lo == hi ? (lo > 1 ? $" ×{lo}" : string.Empty) : $" ×{lo}-{hi}";
        string chanceText = pct >= 99.9999f ? "100%" : $"{pct:0.#}%";

        return $"→ {id}{amountText} @ {chanceText}";
    }

    private void DrawEnhancementScrollStatsBlock()
    {
        DrawModuleHeader("Enhancement Scroll Stats");

        SerializedProperty optionId = serializedObject.FindProperty("enhancementOptionId");
        if (optionId != null)
            EditorGUILayout.PropertyField(optionId, new GUIContent("Enhancement Option Id"));

        EnhancementOptionEntry linkedOption = null;
        if (optionId != null && !string.IsNullOrWhiteSpace(optionId.stringValue))
            linkedOption = EnhancementOptionResolver.GetOptionById(optionId.stringValue);

        if (linkedOption != null)
        {
            EditorGUILayout.HelpBox(
                $"Stats resolve from EnhancementOptionDatabase entry '{linkedOption.optionId}' ({linkedOption.displayName}). " +
                "Use Tools/Create Enhancement Scroll Items to sync cached fields below.",
                MessageType.Info);

            EnhancementScrollStats resolved = linkedOption.ToScrollStats();
            EditorGUILayout.LabelField("Resolved Stat", ItemDefinition.GetEnhancementScrollTargetStatDisplayName(resolved.targetStat));
            EditorGUILayout.LabelField("Resolved Modifier", $"{resolved.modifierKind} {resolved.modifierValue}");
            EditorGUILayout.LabelField("Linked Scroll Item Id", linkedOption.linkedScrollItemId ?? "(none)");
        }

        if (enhancementScrollStats == null)
        {
            EditorGUILayout.HelpBox("enhancementScrollStats property not found.", MessageType.Error);
            return;
        }

        using (new EditorGUI.DisabledScope(linkedOption != null))
        {
            DrawEnhancementScrollStatsFields();
        }
    }

    private void DrawEnhancementScrollStatsFields()
    {
        SerializedProperty successChance = enhancementScrollStats.FindPropertyRelative("successChance");
        SerializedProperty targetStat = enhancementScrollStats.FindPropertyRelative("targetStat");
        SerializedProperty modifierKind = enhancementScrollStats.FindPropertyRelative("modifierKind");
        SerializedProperty modifierValue = enhancementScrollStats.FindPropertyRelative("modifierValue");
        SerializedProperty consumeSlotOnFailure = enhancementScrollStats.FindPropertyRelative("consumeSlotOnFailure");
        SerializedProperty failureOutcome = enhancementScrollStats.FindPropertyRelative("failureOutcome");
        SerializedProperty destroyChanceOnFailure = enhancementScrollStats.FindPropertyRelative("destroyChanceOnFailure");
        SerializedProperty cursed = enhancementScrollStats.FindPropertyRelative("cursed");
        SerializedProperty allowedGearTypes = enhancementScrollStats.FindPropertyRelative("allowedGearTypes");

        EditorGUILayout.LabelField("Success Behaviour", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Success chance is resolved from EnhancementOptionDatabase based on the target item's successful enhancements. " +
            "Chaos gambles stay at 35%. Slot Reduction uses the database option value.",
            MessageType.Info);
        EditorGUILayout.PropertyField(targetStat, new GUIContent("Stat Modifier Applied"));
        EditorGUILayout.PropertyField(modifierKind, new GUIContent("Modifier Type"));
        EditorGUILayout.PropertyField(modifierValue, new GUIContent("Modifier Value"));

        if (successChance != null)
            successChance.floatValue = 0f;

        EditorGUILayout.Space(6);
        EditorGUILayout.LabelField("Failure Behaviour", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(
            consumeSlotOnFailure,
            new GUIContent("Consumes Slot On Use", "Default scroll behaviour. Turn this off for special scrolls like slot reduction."));
        EditorGUILayout.PropertyField(failureOutcome, new GUIContent("Failure Outcome"));
        EditorGUILayout.PropertyField(cursed, new GUIContent("Cursed"));

        bool showDestroyChance =
            (failureOutcome != null &&
             (EnhancementScrollFailureOutcome)failureOutcome.enumValueIndex == EnhancementScrollFailureOutcome.DestroyItem) ||
            (cursed != null && cursed.boolValue);

        if (showDestroyChance)
        {
            EditorGUILayout.PropertyField(destroyChanceOnFailure, new GUIContent("Destroy Chance On Failure"));
            if (destroyChanceOnFailure != null)
                destroyChanceOnFailure.floatValue = Mathf.Clamp01(destroyChanceOnFailure.floatValue);
        }
        else if (destroyChanceOnFailure != null)
        {
            destroyChanceOnFailure.floatValue = 0f;
        }

        EditorGUILayout.Space(6);
        EditorGUILayout.LabelField("Gear Restrictions", EditorStyles.boldLabel);
        DrawEnhancementAllowedGearTypes(allowedGearTypes);

        EditorGUILayout.HelpBox(
            "Scroll design: one clear effect, one success chance, and simple risk.\n\n" +
            "Example: Basic Attack Scroll -> 80% success, +2 Physical Damage, allowed on Weapon, consumes slot on use, no destruction.",
            MessageType.None
        );
    }

    private static void DrawEnhancementAllowedGearTypes(SerializedProperty allowedGearTypes)
    {
        if (allowedGearTypes == null)
            return;

        EnhancementScrollGearMask current = (EnhancementScrollGearMask)allowedGearTypes.intValue;

        bool anyWeapon = (current & EnhancementScrollGearMask.Weapon) != 0;
        bool melee = (current & EnhancementScrollGearMask.MeleeWeapon) != 0;
        bool ranged = (current & EnhancementScrollGearMask.RangedWeapon) != 0;
        bool magic = (current & EnhancementScrollGearMask.MagicWeapon) != 0;
        EnhancementScrollGearMask normalized = EnhancementScrollGearRules.NormalizeMask(current);
        bool head = (normalized & EnhancementScrollGearMask.Helmet) != 0;
        bool body = (normalized & EnhancementScrollGearMask.Body) != 0;
        bool feet = (normalized & EnhancementScrollGearMask.Boots) != 0;
        bool tool = (current & EnhancementScrollGearMask.Tool) != 0;

        anyWeapon = EditorGUILayout.Toggle(new GUIContent("Any Weapon"), anyWeapon);
        using (new EditorGUI.DisabledScope(anyWeapon))
        {
            melee = EditorGUILayout.Toggle(new GUIContent("Melee Weapon"), melee);
            ranged = EditorGUILayout.Toggle(new GUIContent("Ranged Weapon"), ranged);
            magic = EditorGUILayout.Toggle(new GUIContent("Magic Weapon"), magic);
        }

        head = EditorGUILayout.Toggle(new GUIContent("Head"), head);
        body = EditorGUILayout.Toggle(new GUIContent("Body"), body);
        feet = EditorGUILayout.Toggle(new GUIContent("Feet"), feet);
        tool = EditorGUILayout.Toggle(new GUIContent("Tool"), tool);

        EnhancementScrollGearMask next = EnhancementScrollGearMask.None;
        if (anyWeapon)
            next |= EnhancementScrollGearMask.Weapon;
        else
        {
            if (melee) next |= EnhancementScrollGearMask.MeleeWeapon;
            if (ranged) next |= EnhancementScrollGearMask.RangedWeapon;
            if (magic) next |= EnhancementScrollGearMask.MagicWeapon;
        }

        if (head) next |= EnhancementScrollGearMask.Helmet;
        if (body) next |= EnhancementScrollGearMask.Body;
        if (feet) next |= EnhancementScrollGearMask.Boots;
        if (tool) next |= EnhancementScrollGearMask.Tool;

        allowedGearTypes.intValue = (int)next;
    }

    private void DrawCookableStatsBlock()
    {
        DrawModuleHeader("Cookable Stats");

        if (cookableStats == null)
        {
            EditorGUILayout.HelpBox("cookableStats property not found.", MessageType.Error);
            return;
        }

        EditorGUILayout.LabelField("Cooking", EditorStyles.boldLabel);

        SerializedProperty isCookable = cookableStats.FindPropertyRelative("isCookable");
        SerializedProperty cookedResultItemId = cookableStats.FindPropertyRelative("cookedResultItemId");
        SerializedProperty cookedResultAmount = cookableStats.FindPropertyRelative("cookedResultAmount");
        SerializedProperty requiredCookingLevel = cookableStats.FindPropertyRelative("requiredCookingLevel");
        SerializedProperty cookingXp = cookableStats.FindPropertyRelative("cookingXp");

        EditorGUILayout.PropertyField(
            isCookable,
            new GUIContent("Is Cookable", isCookable != null ? isCookable.tooltip : null));

        if (isCookable != null && isCookable.boolValue)
        {
            EditorGUILayout.PropertyField(cookedResultItemId);
            EditorGUILayout.PropertyField(cookedResultAmount);
            EditorGUILayout.PropertyField(requiredCookingLevel);
            EditorGUILayout.PropertyField(cookingXp);

            if (cookedResultAmount != null && cookedResultAmount.intValue < 1)
                cookedResultAmount.intValue = 1;

            if (requiredCookingLevel != null && requiredCookingLevel.intValue < 0)
                requiredCookingLevel.intValue = 0;

            if (cookingXp != null && cookingXp.intValue < 0)
                cookingXp.intValue = 0;

            if (cookedResultItemId != null && string.IsNullOrWhiteSpace(cookedResultItemId.stringValue))
            {
                EditorGUILayout.HelpBox(
                    "This item is marked cookable but has no Cooked Result Item ID.",
                    MessageType.Warning
                );
            }
        }

        EditorGUILayout.HelpBox(
            "Cookable data is a future hook for your cooking system.\n\n" +
            "Example:\n" +
            "- Raw Fish -> Cooked Fish\n" +
            "- Raw Meat -> Cooked Meat",
            MessageType.None
        );
    }

    private static void ClampMinMax(SerializedProperty minProp, SerializedProperty maxProp)
    {
        if (minProp == null || maxProp == null) return;

        if (minProp.intValue < 0) minProp.intValue = 0;
        if (maxProp.intValue < 0) maxProp.intValue = 0;

        if (maxProp.intValue < minProp.intValue)
            maxProp.intValue = minProp.intValue;
    }

    private static void DrawEquipSlotHint(ItemKind kind, EquipSlot slot)
    {
        string msg = null;

        if (kind == ItemKind.Weapon)
            msg = "Weapons should usually use Equip Slot: MainHand (or OffHand if dual-wield weapons are allowed).";
        else if (kind == ItemKind.CombatSupport)
            msg = "Combat Support items should use Equip Slot: OffHand.";
        else if (kind == ItemKind.Tool)
            msg = "Tools should use Equip Slot: MainHand. (Toolbelt is UI-managed, not an EquipSlot.)";
        else if (kind == ItemKind.Armour)
            msg = "Armour should use Equip Slot: Head / Body / Feet.";
        else if (kind == ItemKind.Jewelry)
            msg = "Jewelry should use Equip Slot: Ring / Neck / Trinket.\nRings can be equipped into Ring1 or Ring2 in UI.";
        else if (kind == ItemKind.Consumable)
            msg = "Consumables should use Equip Slot: None. They are used from inventory/action bar, not equipped.";
        else if (kind == ItemKind.MapEnhancement)
            msg = "Map enhancements should use Equip Slot: None. They are applied from inventory onto a combat map node.";
        else
            msg = "Non-equippables should use Equip Slot: None.";

        EditorGUILayout.HelpBox(msg, MessageType.None);
    }

    private static void EnforceSlotRules(ItemKind kind, SerializedProperty equipSlotProp, ref EquipSlot slot)
    {
        bool isWeapon = kind == ItemKind.Weapon;
        bool isCombatSupport = kind == ItemKind.CombatSupport;
        bool isTool = kind == ItemKind.Tool;
        bool isArmour = kind == ItemKind.Armour;
        bool isJewelry = kind == ItemKind.Jewelry;
        bool isConsumable = kind == ItemKind.Consumable;

        if (!isWeapon && !isCombatSupport && !isTool && !isArmour && !isJewelry && !isConsumable)
        {
            if (slot != EquipSlot.None)
            {
                equipSlotProp.enumValueIndex = (int)EquipSlot.None;
                slot = EquipSlot.None;
            }
            return;
        }

        if (isWeapon)
        {
            if (slot != EquipSlot.MainHand && slot != EquipSlot.OffHand)
            {
                equipSlotProp.enumValueIndex = (int)EquipSlot.MainHand;
                slot = EquipSlot.MainHand;
            }
            return;
        }

        if (isCombatSupport)
        {
            if (slot != EquipSlot.OffHand)
            {
                equipSlotProp.enumValueIndex = (int)EquipSlot.OffHand;
                slot = EquipSlot.OffHand;
            }
            return;
        }

        if (isTool)
        {
            if (slot != EquipSlot.MainHand)
            {
                equipSlotProp.enumValueIndex = (int)EquipSlot.MainHand;
                slot = EquipSlot.MainHand;
            }
            return;
        }

        if (isArmour)
        {
            if (slot != EquipSlot.Helmet && slot != EquipSlot.Body && slot != EquipSlot.Boots && slot != EquipSlot.OffHand)
            {
                equipSlotProp.enumValueIndex = (int)EquipSlot.Body;
                slot = EquipSlot.Body;
            }
            return;
        }

        if (isJewelry)
        {
            if (slot != EquipSlot.Ring && slot != EquipSlot.Pendant && slot != EquipSlot.Trinket)
            {
                equipSlotProp.enumValueIndex = (int)EquipSlot.Ring;
                slot = EquipSlot.Ring;
            }
            return;
        }

        if (isConsumable)
        {
            if (slot != EquipSlot.None)
            {
                equipSlotProp.enumValueIndex = (int)EquipSlot.None;
                slot = EquipSlot.None;
            }
        }
    }

    private void DrawBonusBlockIfPresent(string title, bool show)
    {
        if (bonusStats == null || !show)
            return;

        DrawModuleHeader(title);

        SerializedProperty bonusHealth = bonusStats.FindPropertyRelative("bonusHealth");
        SerializedProperty bonusEnergy = bonusStats.FindPropertyRelative("bonusEnergy");
        SerializedProperty bonusMana = bonusStats.FindPropertyRelative("bonusMana");

        SerializedProperty armour = bonusStats.FindPropertyRelative("armour");
        SerializedProperty magicResist = bonusStats.FindPropertyRelative("magicResist");
        SerializedProperty corruptionResistBonus = bonusStats.FindPropertyRelative("corruptionResist");
        SerializedProperty physBlockChance = bonusStats.FindPropertyRelative("physBlockChance");

        SerializedProperty lifeRegen = bonusStats.FindPropertyRelative("lifeRegen");
        SerializedProperty energyRegen = bonusStats.FindPropertyRelative("energyRegen");
        SerializedProperty manaRegen = bonusStats.FindPropertyRelative("manaRegen");
        SerializedProperty energyEfficiency = bonusStats.FindPropertyRelative("energyEfficiency");
        SerializedProperty lifeSteal = bonusStats.FindPropertyRelative("lifeSteal");

        SerializedProperty moveSpeedPercent = bonusStats.FindPropertyRelative("moveSpeedPercent");

        SerializedProperty physicalDamage = bonusStats.FindPropertyRelative("physicalDamage");
        SerializedProperty meleePhysicalDamagePercent = bonusStats.FindPropertyRelative("meleePhysicalDamagePercent");
        SerializedProperty globalPhysicalDamagePercentBonus = bonusStats.FindPropertyRelative("globalPhysicalDamagePercent");
        SerializedProperty rangedPhysicalDamagePercentBonus = bonusStats.FindPropertyRelative("rangedPhysicalDamagePercent");
        SerializedProperty magicDamage = bonusStats.FindPropertyRelative("magicDamage");
        SerializedProperty magicDamagePercent = bonusStats.FindPropertyRelative("magicDamagePercent");
        SerializedProperty fireSkillDamagePercent = bonusStats.FindPropertyRelative("fireSkillDamagePercent");
        SerializedProperty iceSkillDamagePercent = bonusStats.FindPropertyRelative("iceSkillDamagePercent");
        SerializedProperty lightningSkillDamagePercent = bonusStats.FindPropertyRelative("lightningSkillDamagePercent");
        SerializedProperty corruptionDamage = bonusStats.FindPropertyRelative("corruptionDamage");
        SerializedProperty abilityPower = bonusStats.FindPropertyRelative("abilityPower");

        SerializedProperty attackSpeedPercent = bonusStats.FindPropertyRelative("attackSpeedPercent");
        SerializedProperty minionDamagePercent = bonusStats.FindPropertyRelative("minionDamagePercent");
        SerializedProperty minionAttackSpeedPercent = bonusStats.FindPropertyRelative("minionAttackSpeedPercent");
        SerializedProperty minionCritChance = bonusStats.FindPropertyRelative("minionCritChance");
        SerializedProperty minionMaxLifePercent = bonusStats.FindPropertyRelative("minionMaxLifePercent");
        SerializedProperty critChanceBonus = bonusStats.FindPropertyRelative("critChanceBonus");
        SerializedProperty critMultiplierBonus = bonusStats.FindPropertyRelative("critMultiplierBonus");
        SerializedProperty attackRangeBonus = bonusStats.FindPropertyRelative("attackRangeBonus");

        SerializedProperty bleedChance = bonusStats.FindPropertyRelative("bleedChance");
        SerializedProperty bleedMultiplier = bonusStats.FindPropertyRelative("bleedMultiplier");
        SerializedProperty poisonChance = bonusStats.FindPropertyRelative("poisonChance");
        SerializedProperty poisonMultiplier = bonusStats.FindPropertyRelative("poisonMultiplier");
        SerializedProperty poisonDurationBonus = bonusStats.FindPropertyRelative("poisonDurationBonus");
        SerializedProperty poisonMaxStacksBonus = bonusStats.FindPropertyRelative("poisonMaxStacksBonus");
        SerializedProperty burnExplosionMultiplierBonus = bonusStats.FindPropertyRelative("burnExplosionMultiplierBonus");
        SerializedProperty chillSlowPerStackBonus = bonusStats.FindPropertyRelative("chillSlowPerStackBonus");
        SerializedProperty shockDamageTakenMultiplierBonus = bonusStats.FindPropertyRelative("shockDamageTakenMultiplierBonus");
        SerializedProperty bonusBurnChance = bonusStats.FindPropertyRelative("burnChance");
        SerializedProperty bonusChillChance = bonusStats.FindPropertyRelative("chillChance");
        SerializedProperty bonusShockChance = bonusStats.FindPropertyRelative("shockChance");
        SerializedProperty parryChance = bonusStats.FindPropertyRelative("parryChance");
        SerializedProperty stunChance = bonusStats.FindPropertyRelative("stunChance");

        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField("Vitals", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(bonusHealth);
        EditorGUILayout.PropertyField(bonusEnergy);
        EditorGUILayout.PropertyField(bonusMana);

        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField("Defence", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(armour);
        EditorGUILayout.PropertyField(magicResist);
        if (corruptionResistBonus != null)
            EditorGUILayout.PropertyField(corruptionResistBonus, new GUIContent("Corruption Resist"));
        EditorGUILayout.PropertyField(physBlockChance);

        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField("Sustain", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(lifeRegen);
        EditorGUILayout.PropertyField(energyRegen);
        EditorGUILayout.PropertyField(manaRegen);
        ItemKind kind = (ItemKind)itemKind.enumValueIndex;
        if (kind == ItemKind.Armour || kind == ItemKind.Jewelry)
            EditorGUILayout.PropertyField(energyEfficiency, new GUIContent("Energy Efficiency"));
        EditorGUILayout.PropertyField(lifeSteal);

        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField("Mobility", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(moveSpeedPercent);

        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField("Offense", EditorStyles.boldLabel);
        PropertyField(physicalDamage, "Physical damage (flat)");
        PropertyField(meleePhysicalDamagePercent, "Melee damage %");
        PropertyField(globalPhysicalDamagePercentBonus, "Physical damage %");
        PropertyField(rangedPhysicalDamagePercentBonus, "Ranged damage %");
        PropertyField(magicDamage, "Magic damage (flat)");
        PropertyField(magicDamagePercent, "Magic damage %");
        PropertyField(fireSkillDamagePercent, "Fire damage %");
        PropertyField(iceSkillDamagePercent, "Ice damage %");
        PropertyField(lightningSkillDamagePercent, "Lightning damage %");
        SerializedProperty corruptionDamagePercent = bonusStats.FindPropertyRelative("corruptionDamagePercent");
        if (corruptionDamagePercent != null)
            PropertyField(corruptionDamagePercent, "Corruption damage %");
        PropertyField(corruptionDamage, "Corruption damage (flat)");
        PropertyField(abilityPower, "Ability power %");
        PropertyField(attackSpeedPercent, "Attack speed %");
        PropertyField(critChanceBonus, "Crit chance bonus");
        PropertyField(critMultiplierBonus, "Crit multiplier bonus");
        PropertyField(attackRangeBonus, "Attack range bonus");

        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField("Minion", EditorStyles.boldLabel);
        PropertyField(minionDamagePercent, "Minion damage %");
        PropertyField(minionAttackSpeedPercent, "Minion attack speed %");
        PropertyField(minionCritChance, "Minion crit chance");
        PropertyField(minionMaxLifePercent, "Minion health %");

        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField("Ailments", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(bleedChance);
        EditorGUILayout.PropertyField(bleedMultiplier);
        EditorGUILayout.PropertyField(poisonChance);
        EditorGUILayout.PropertyField(poisonMultiplier);
        EditorGUILayout.PropertyField(poisonDurationBonus);
        EditorGUILayout.PropertyField(poisonMaxStacksBonus);
        if (bonusBurnChance != null)
            EditorGUILayout.PropertyField(bonusBurnChance, new GUIContent("Burn Chance (bonus)"));
        if (bonusChillChance != null)
            EditorGUILayout.PropertyField(bonusChillChance, new GUIContent("Chill Chance (bonus)"));
        if (bonusShockChance != null)
            EditorGUILayout.PropertyField(bonusShockChance, new GUIContent("Shock Chance (bonus)"));
        EditorGUILayout.PropertyField(burnExplosionMultiplierBonus, new GUIContent("Burn Multiplier"));
        EditorGUILayout.PropertyField(chillSlowPerStackBonus, new GUIContent("Chill Effect"));
        EditorGUILayout.PropertyField(shockDamageTakenMultiplierBonus, new GUIContent("Shock Effect"));

        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField("Combat Procs", EditorStyles.boldLabel);
        if (parryChance != null)
            EditorGUILayout.PropertyField(parryChance);
        if (stunChance != null)
            EditorGUILayout.PropertyField(stunChance);

        EditorGUILayout.Space(6);
        EditorGUILayout.HelpBox(
            "Bonus Stats are additive modifiers.\n" +
            "Use these for Armour, Jewelry, and optionally Weapons/Tools.\n\n" +
            "Examples:\n" +
            "- Physical / Magic / Corruption Damage\n" +
            "- Ability power %\n" +
            "- Crit / attack speed / range\n" +
            "- Minion damage, attack speed, crit chance, and health (max life %)\n" +
            "- Bleed or poison chance and multiplier\n" +
            "- Poison duration and poison max stacks\n" +
            "- Burn/Chill/Shock elemental ailment scaling",
            MessageType.None
        );
    }

    private void DrawMiscEffectsBlockIfPresent(bool show)
    {
        if (miscEffects == null || !show)
            return;

        EditorGUILayout.Space(8);
        EditorGUILayout.PropertyField(miscEffects, includeChildren: true);
        EditorGUILayout.HelpBox(
            "Expand this section for effects that are not standard bonus stats (e.g. spawn / world modifiers). " +
            "Enemy respawn reduction stacks across all equipped items and subtracts from the map node's Enemy Respawn Delay (MapNodeDefinition).",
            MessageType.None
        );
    }

    private void DrawRandomStatPoolBlock()
    {
        if (randomStatPool == null)
        {
            EditorGUILayout.HelpBox("randomStatPool property not found on ItemDefinition.", MessageType.Error);
            return;
        }

        DrawModuleHeader("Additional Random Stat Pool");

        EditorGUILayout.HelpBox(
            "Optional affixes rolled when this item enters the player's inventory.\n" +
            "Common/Uncommon = 1 roll, Rare = 2, Epic = 3, Legendary = 4.\n" +
            "Rolled values add to existing base/bonus stats. Shop tooltips show ?? until purchased.\n" +
            "Value Kind: Flat Integer (health/damage), Flat Float (regen/range), " +
            "Percent Points (enter 5 for +5% crit/stun/etc.; weapon APS rolls add flat APS — 2 = +0.02 APS on a 0.6 weapon → 0.62). " +
            "Ability power stores points as-is. Legacy Fraction APS entries still multiply.",
            MessageType.Info
        );

        if (randomStatPool.arraySize == 0)
            EditorGUILayout.LabelField("No pool entries yet. Add one below.", EditorStyles.centeredGreyMiniLabel);

        _randomStatPoolScroll = EditorGUILayout.BeginScrollView(_randomStatPoolScroll, GUILayout.MaxHeight(420f));

        int removeAt = -1;
        for (int i = 0; i < randomStatPool.arraySize; i++)
        {
            SerializedProperty entry = randomStatPool.GetArrayElementAtIndex(i);
            if (entry == null)
                continue;

            SerializedProperty stat = entry.FindPropertyRelative("stat");
            SerializedProperty weight = entry.FindPropertyRelative("weight");
            SerializedProperty minValue = entry.FindPropertyRelative("minValue");
            SerializedProperty maxValue = entry.FindPropertyRelative("maxValue");
            SerializedProperty valueKind = entry.FindPropertyRelative("valueKind");
            SerializedProperty rollSecondary = entry.FindPropertyRelative("rollSecondaryValue");
            SerializedProperty secondaryMin = entry.FindPropertyRelative("secondaryMinValue");
            SerializedProperty secondaryMax = entry.FindPropertyRelative("secondaryMaxValue");

            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"Pool Entry {i + 1}", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Remove", GUILayout.Width(72)))
                removeAt = i;
            EditorGUILayout.EndHorizontal();

            if (stat != null)
                DrawScrollableRandomStatPicker(stat, i);
            if (weight != null) EditorGUILayout.PropertyField(weight, new GUIContent("Weight"));
            if (valueKind != null) EditorGUILayout.PropertyField(valueKind, new GUIContent("Value Kind"));
            if (minValue != null) EditorGUILayout.PropertyField(minValue, new GUIContent("Min Value"));
            if (maxValue != null) EditorGUILayout.PropertyField(maxValue, new GUIContent("Max Value"));

            if (stat != null)
            {
                RandomItemStatType statType = (RandomItemStatType)stat.enumValueIndex;
                if (statType == RandomItemStatType.WeaponCorruptionDamageRange)
                {
                    if (rollSecondary != null)
                        EditorGUILayout.PropertyField(rollSecondary, new GUIContent("Roll Min/Max Separately"));
                    if (rollSecondary != null && rollSecondary.boolValue)
                    {
                        if (secondaryMin != null)
                            EditorGUILayout.PropertyField(secondaryMin, new GUIContent("Max Corruption Min"));
                        if (secondaryMax != null)
                            EditorGUILayout.PropertyField(secondaryMax, new GUIContent("Max Corruption Max"));
                    }
                }
            }

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(4);
        }

        if (removeAt >= 0)
            randomStatPool.DeleteArrayElementAtIndex(removeAt);

        EditorGUILayout.EndScrollView();

        EditorGUILayout.Space(2);
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Generate template stats"))
            GenerateTemplateRandomStatPool();

        if (GUILayout.Button("+ Add Pool Entry"))
            AddEmptyRandomStatPoolEntry();

        EditorGUILayout.EndHorizontal();
    }

    private void AddEmptyRandomStatPoolEntry()
    {
        int newIndex = randomStatPool.arraySize;
        randomStatPool.InsertArrayElementAtIndex(newIndex);
        SerializedProperty newEntry = randomStatPool.GetArrayElementAtIndex(newIndex);
        if (newEntry == null)
            return;

        SerializedProperty weight = newEntry.FindPropertyRelative("weight");
        if (weight != null)
            weight.floatValue = 1f;

        SerializedProperty valueKind = newEntry.FindPropertyRelative("valueKind");
        SerializedProperty stat = newEntry.FindPropertyRelative("stat");
        if (valueKind != null && stat != null)
        {
            RandomItemStatType statType = (RandomItemStatType)stat.enumValueIndex;
            valueKind.enumValueIndex = (int)ItemRandomStatRoller.GetDefaultValueKind(statType);
        }
    }

    private void GenerateTemplateRandomStatPool()
    {
        var item = (ItemDefinition)target;
        if (!item)
            return;

        List<RandomStatPoolEntry> templates = ItemRandomStatRoller.BuildTemplatePoolEntries(item);
        if (templates.Count == 0)
        {
            EditorUtility.DisplayDialog(
                "Generate template stats",
                "No non-zero stats found on this item to turn into pool entries.",
                "OK");
            return;
        }

        if (randomStatPool.arraySize > 0 &&
            !EditorUtility.DisplayDialog(
                "Generate template stats",
                $"Replace {randomStatPool.arraySize} existing pool entr{(randomStatPool.arraySize == 1 ? "y" : "ies")} " +
                $"with {templates.Count} template entr{(templates.Count == 1 ? "y" : "ies")} from current item stats?",
                "Replace",
                "Cancel"))
            return;

        randomStatPool.ClearArray();
        for (int i = 0; i < templates.Count; i++)
            WriteRandomStatPoolEntry(randomStatPool, i, templates[i]);

        serializedObject.ApplyModifiedProperties();
        EditorUtility.SetDirty(item);
    }

    private static void WriteRandomStatPoolEntry(
        SerializedProperty pool,
        int index,
        RandomStatPoolEntry entry)
    {
        pool.InsertArrayElementAtIndex(index);
        SerializedProperty element = pool.GetArrayElementAtIndex(index);
        if (element == null)
            return;

        SerializedProperty stat = element.FindPropertyRelative("stat");
        SerializedProperty weight = element.FindPropertyRelative("weight");
        SerializedProperty minValue = element.FindPropertyRelative("minValue");
        SerializedProperty maxValue = element.FindPropertyRelative("maxValue");
        SerializedProperty valueKind = element.FindPropertyRelative("valueKind");
        SerializedProperty rollSecondary = element.FindPropertyRelative("rollSecondaryValue");
        SerializedProperty secondaryMin = element.FindPropertyRelative("secondaryMinValue");
        SerializedProperty secondaryMax = element.FindPropertyRelative("secondaryMaxValue");

        if (stat != null)
            stat.enumValueIndex = (int)entry.stat;
        if (weight != null)
            weight.floatValue = entry.weight;
        if (minValue != null)
            minValue.floatValue = entry.minValue;
        if (maxValue != null)
            maxValue.floatValue = entry.maxValue;
        if (valueKind != null)
            valueKind.enumValueIndex = (int)entry.valueKind;
        if (rollSecondary != null)
            rollSecondary.boolValue = entry.rollSecondaryValue;
        if (secondaryMin != null)
            secondaryMin.floatValue = entry.secondaryMinValue;
        if (secondaryMax != null)
            secondaryMax.floatValue = entry.secondaryMaxValue;
    }

    private void DrawScrollableRandomStatPicker(SerializedProperty statProp, int entryIndex)
    {
        if (statProp == null)
            return;

        RandomItemStatType current = (RandomItemStatType)statProp.enumValueIndex;
        string currentLabel = ItemStatDisplayNames.ForRandomPoolStat(current, (ItemDefinition)target);

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.PrefixLabel("Stat");
        if (GUILayout.Button(currentLabel, EditorStyles.popup))
        {
            _expandedStatPickerEntryIndex = _expandedStatPickerEntryIndex == entryIndex ? -1 : entryIndex;
            _statPickerSearch = "";
        }
        EditorGUILayout.EndHorizontal();

        if (_expandedStatPickerEntryIndex != entryIndex)
            return;

        EditorGUILayout.BeginVertical("box");
        _statPickerSearch = EditorGUILayout.TextField("Search", _statPickerSearch ?? "");

        _statPickerScroll = EditorGUILayout.BeginScrollView(_statPickerScroll, GUILayout.Height(220f));
        string search = (_statPickerSearch ?? "").Trim();

        foreach (RandomItemStatType value in Enum.GetValues(typeof(RandomItemStatType)))
        {
            string raw = value.ToString();
            string label = ItemStatDisplayNames.ForRandomPoolStat(value, (ItemDefinition)target);
            if (!string.IsNullOrEmpty(search) &&
                raw.IndexOf(search, StringComparison.OrdinalIgnoreCase) < 0 &&
                label.IndexOf(search, StringComparison.OrdinalIgnoreCase) < 0)
            {
                continue;
            }

            if (GUILayout.Button(label, EditorStyles.miniButton))
            {
                statProp.enumValueIndex = (int)value;
                _expandedStatPickerEntryIndex = -1;
                _statPickerSearch = "";
                GUI.FocusControl(null);
            }
        }

        EditorGUILayout.EndScrollView();
        EditorGUILayout.EndVertical();
    }

    private static void DrawModuleHeader(string title)
    {
        EditorGUILayout.Space(2);
        EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
        EditorGUILayout.Space(2);
    }

    /// <summary>Inspector label + tooltip from <see cref="TooltipAttribute"/> (plain PropertyField with custom label drops tooltips).</summary>
    private static GUIContent Prop(SerializedProperty prop, string label)
    {
        string tip = prop.tooltip;
        return string.IsNullOrEmpty(tip) ? new GUIContent(label) : new GUIContent(label, tip);
    }

    private static void PropertyField(SerializedProperty prop, string label)
    {
        if (prop != null)
            EditorGUILayout.PropertyField(prop, Prop(prop, label));
    }
}