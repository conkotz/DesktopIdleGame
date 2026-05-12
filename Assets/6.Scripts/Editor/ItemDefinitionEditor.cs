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
    private SerializedProperty armorStats;
    private SerializedProperty consumableStats;
    private SerializedProperty enhancementScrollStats;
    private SerializedProperty cookableStats;

    // Bonuses
    private SerializedProperty bonusStats;
    private SerializedProperty miscEffects;

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
        armorStats = serializedObject.FindProperty("armorStats");
        consumableStats = serializedObject.FindProperty("consumableStats");
        enhancementScrollStats = serializedObject.FindProperty("enhancementScrollStats");
        cookableStats = serializedObject.FindProperty("cookableStats");

        bonusStats = serializedObject.FindProperty("bonusStats");
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

        bool showEquipmentSection = kind != ItemKind.EnhancementScroll;
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
            kind == ItemKind.Armor ||
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

        bool isArmorVisualItem =
            kind == ItemKind.Armor &&
            (slot == EquipSlot.Helmet || slot == EquipSlot.Body || slot == EquipSlot.Boots);

        bool showEquippedVisual =
            isMainHandVisualItem || isOffHandSupportVisualItem || isArmorVisualItem;

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
        else if (kind == ItemKind.Armor)
        {
            DrawModuleHeader("Armour Stats");
            EditorGUILayout.PropertyField(armorStats, includeChildren: true);
            DrawBonusBlockIfPresent("Bonus Stats (Armour Extras)", show: true);
            DrawMiscEffectsBlockIfPresent(show: true);
        }
        else if (kind == ItemKind.Jewelry)
        {
            DrawBonusBlockIfPresent("Bonus Stats (Jewelry)", show: true);
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
        else
        {
            DrawBonusBlockIfPresent("Bonus Stats", show: false);
        }

        if (kind == ItemKind.Consumable)
            DrawCookableStatsBlock();

        serializedObject.ApplyModifiedProperties();
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
                    "Tier 1–3 (display name is on the item). Gated by matching combat skill: L1 / L20 / L40."
                )
            );
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
                    EditorGUILayout.PropertyField(burnExplosionMultiplierBonus, new GUIContent("Burn Damage Bonus"));
                if (chillSlowPerStackBonus != null)
                    EditorGUILayout.PropertyField(chillSlowPerStackBonus, new GUIContent("Chill Slow/Stack Bonus"));
                if (shockDamageTakenMultiplierBonus != null)
                    EditorGUILayout.PropertyField(shockDamageTakenMultiplierBonus, new GUIContent("Shock Amp Bonus"));
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

        SerializedProperty physicalDamagePercent = combatSupportStats.FindPropertyRelative("physicalDamagePercent");
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
        PropertyField(physicalDamagePercent, "All physical %");
        PropertyField(globalPhysicalDamagePercentCs, "Global physical %");
        PropertyField(rangedPhysicalDamagePercentCs, "Ranged physical %");
        PropertyField(magicDamagePercentCs, "All magic %");
        PropertyField(fireDamagePercent, "Fire skills %");
        PropertyField(iceDamagePercent, "Ice skills %");
        PropertyField(coldDamagePercent, "Cold skills %");
        PropertyField(corruptionDamagePercent, "Corruption %");

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
                    "Tier 1–3 (display name is on the item). Gated by Woodcutting / Mining / Fishing: L1 / L20 / L40."
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

        EditorGUILayout.PropertyField(consumableType);
        EditorGUILayout.Space(4);

        ConsumableType selectedType = consumableType != null
            ? (ConsumableType)consumableType.enumValueIndex
            : ConsumableType.None;

        bool isOpenable = selectedType == ConsumableType.Openable;

        // Heal / Energy / Granted Effect only matter for Food / Potion. Hiding them on Openable keeps the
        // inspector focused on the loot table for that mode.
        if (!isOpenable)
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
        }
        else
        {
            DrawOpenableLootTable(openableLoot);
        }

        EditorGUILayout.HelpBox(
            "Consumables can be assigned to the action bar and used by hotkey.\n\n" +
            "Food: usually instant healing.\n" +
            "Potion: can heal, restore energy, and/or apply a temporary effect.\n" +
            "Openable: double-click the item to open it. Each loot row rolls independently using its own % chance. " +
            "1 of the source item is always consumed on open.",
            MessageType.None
        );
    }

    private static void DrawOpenableLootTable(SerializedProperty openableLoot)
    {
        EditorGUILayout.LabelField("Loot Table", EditorStyles.boldLabel);

        if (openableLoot == null)
        {
            EditorGUILayout.HelpBox(
                "openableLoot property missing — re-import the script.",
                MessageType.Error);
            return;
        }

        EditorGUILayout.HelpBox(
            "Each row is rolled independently when the player double-clicks the item.\n" +
            "100% = guaranteed drop, 25% = rolls about 1 in 4 opens. Set Min/Max Amount for a stack range.",
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

        if (enhancementScrollStats == null)
        {
            EditorGUILayout.HelpBox("enhancementScrollStats property not found.", MessageType.Error);
            return;
        }

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
        EditorGUILayout.PropertyField(successChance, new GUIContent("Success Chance"));
        EditorGUILayout.PropertyField(targetStat, new GUIContent("Stat Modifier Applied"));
        EditorGUILayout.PropertyField(modifierKind, new GUIContent("Modifier Type"));
        EditorGUILayout.PropertyField(modifierValue, new GUIContent("Modifier Value"));

        if (successChance != null)
            successChance.floatValue = Mathf.Clamp01(successChance.floatValue);

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
        bool armor = (current & EnhancementScrollGearMask.Armor) != 0;
        bool tool = (current & EnhancementScrollGearMask.Tool) != 0;

        anyWeapon = EditorGUILayout.Toggle(new GUIContent("Any Weapon"), anyWeapon);
        using (new EditorGUI.DisabledScope(anyWeapon))
        {
            melee = EditorGUILayout.Toggle(new GUIContent("Melee Weapon"), melee);
            ranged = EditorGUILayout.Toggle(new GUIContent("Ranged Weapon"), ranged);
            magic = EditorGUILayout.Toggle(new GUIContent("Magic Weapon"), magic);
        }

        armor = EditorGUILayout.Toggle(new GUIContent("Armor"), armor);
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

        if (armor) next |= EnhancementScrollGearMask.Armor;
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

        SerializedProperty isCookable = cookableStats.FindPropertyRelative("isCookable");
        SerializedProperty cookedResultItemId = cookableStats.FindPropertyRelative("cookedResultItemId");
        SerializedProperty cookedResultAmount = cookableStats.FindPropertyRelative("cookedResultAmount");
        SerializedProperty requiredCookingLevel = cookableStats.FindPropertyRelative("requiredCookingLevel");
        SerializedProperty cookingXp = cookableStats.FindPropertyRelative("cookingXp");

        EditorGUILayout.PropertyField(isCookable);

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
        else if (kind == ItemKind.Armor)
            msg = "Armour should use Equip Slot: Helmet / Body / Boots.";
        else if (kind == ItemKind.Jewelry)
            msg = "Jewelry should use Equip Slot: Ring / Pendant / Trinket.\nRings can be equipped into Ring1 or Ring2 in UI.";
        else if (kind == ItemKind.Consumable)
            msg = "Consumables should use Equip Slot: None. They are used from inventory/action bar, not equipped.";
        else
            msg = "Non-equippables should use Equip Slot: None.";

        EditorGUILayout.HelpBox(msg, MessageType.None);
    }

    private static void EnforceSlotRules(ItemKind kind, SerializedProperty equipSlotProp, ref EquipSlot slot)
    {
        bool isWeapon = kind == ItemKind.Weapon;
        bool isCombatSupport = kind == ItemKind.CombatSupport;
        bool isTool = kind == ItemKind.Tool;
        bool isArmor = kind == ItemKind.Armor;
        bool isJewelry = kind == ItemKind.Jewelry;
        bool isConsumable = kind == ItemKind.Consumable;

        if (!isWeapon && !isCombatSupport && !isTool && !isArmor && !isJewelry && !isConsumable)
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

        if (isArmor)
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

        SerializedProperty armor = bonusStats.FindPropertyRelative("armor");
        SerializedProperty magicResist = bonusStats.FindPropertyRelative("magicResist");
        SerializedProperty corruptionResistBonus = bonusStats.FindPropertyRelative("corruptionResist");
        SerializedProperty physBlockChance = bonusStats.FindPropertyRelative("physBlockChance");

        SerializedProperty lifeRegen = bonusStats.FindPropertyRelative("lifeRegen");
        SerializedProperty energyRegen = bonusStats.FindPropertyRelative("energyRegen");
        SerializedProperty manaRegen = bonusStats.FindPropertyRelative("manaRegen");
        SerializedProperty lifeSteal = bonusStats.FindPropertyRelative("lifeSteal");

        SerializedProperty moveSpeedPercent = bonusStats.FindPropertyRelative("moveSpeedPercent");

        SerializedProperty physicalDamage = bonusStats.FindPropertyRelative("physicalDamage");
        SerializedProperty physicalDamagePercent = bonusStats.FindPropertyRelative("physicalDamagePercent");
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

        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField("Vitals", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(bonusHealth);
        EditorGUILayout.PropertyField(bonusEnergy);
        EditorGUILayout.PropertyField(bonusMana);

        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField("Defence", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(armor);
        EditorGUILayout.PropertyField(magicResist);
        if (corruptionResistBonus != null)
            EditorGUILayout.PropertyField(corruptionResistBonus, new GUIContent("Corruption Resist"));
        EditorGUILayout.PropertyField(physBlockChance);

        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField("Sustain", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(lifeRegen);
        EditorGUILayout.PropertyField(energyRegen);
        EditorGUILayout.PropertyField(manaRegen);
        EditorGUILayout.PropertyField(lifeSteal);

        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField("Mobility", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(moveSpeedPercent);

        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField("Offense", EditorStyles.boldLabel);
        PropertyField(physicalDamage, "Physical damage");
        PropertyField(physicalDamagePercent, "All physical %");
        PropertyField(globalPhysicalDamagePercentBonus, "Global physical %");
        PropertyField(rangedPhysicalDamagePercentBonus, "Ranged physical %");
        PropertyField(magicDamage, "Magic damage");
        PropertyField(magicDamagePercent, "All magic %");
        PropertyField(fireSkillDamagePercent, "Fire skills %");
        PropertyField(iceSkillDamagePercent, "Ice skills %");
        PropertyField(lightningSkillDamagePercent, "Lightning skills %");
        PropertyField(corruptionDamage, "Corruption damage");
        PropertyField(abilityPower, "Ability Power");
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
        EditorGUILayout.PropertyField(burnExplosionMultiplierBonus, new GUIContent("Burn Damage Bonus"));
        EditorGUILayout.PropertyField(chillSlowPerStackBonus, new GUIContent("Chill Slow/Stack Bonus"));
        EditorGUILayout.PropertyField(shockDamageTakenMultiplierBonus, new GUIContent("Shock Amp Bonus"));

        EditorGUILayout.Space(6);
        EditorGUILayout.HelpBox(
            "Bonus Stats are additive modifiers.\n" +
            "Use these for Armour, Jewelry, and optionally Weapons/Tools.\n\n" +
            "Examples:\n" +
            "- Physical / Magic / Corruption Damage\n" +
            "- Ability Power\n" +
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
        DrawModuleHeader("Misc (unique effects)");
        EditorGUILayout.PropertyField(miscEffects, includeChildren: true);
        EditorGUILayout.HelpBox(
            "Expand this section for effects that are not standard bonus stats (e.g. spawn / world modifiers). " +
            "Enemy respawn reduction stacks across all equipped items and subtracts from the map node's Enemy Respawn Delay (MapNodeDefinition).",
            MessageType.None
        );
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