#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Creates the 3-column SkillNodeDetailsPanelUI prefab matching the approved mockup (placeholders only).</summary>
public static class SkillNodeDetailsPanelPrefabCreator
{
    private const string PrefabPath = "Assets/2.Prefabs/UI/SkillsAbilityNew/SkillNodeDetailsPanelUI.prefab";

    private const float PanelWidth = SkillNodeDetailsPanelUI.PanelWidth;
    private const float PanelHeight = SkillNodeDetailsPanelUI.PanelHeight;
    private const float ColumnSpacing = 0f;

    private static readonly Color PanelBg = new(0.11f, 0.1f, 0.09f, 0.98f);
    private static readonly Color GoldTitle = new(0.83f, 0.72f, 0.45f, 1f);
    private static readonly Color BodyText = new(0.93f, 0.9f, 0.84f, 1f);
    private static readonly Color MutedText = new(0.72f, 0.68f, 0.6f, 1f);
    private static readonly Color DividerColor = new(0.55f, 0.48f, 0.36f, 0.65f);
    private static readonly Color CardBg = new(0.15f, 0.13f, 0.11f, 1f);
    private static readonly Color IconFrame = new(0.45f, 0.38f, 0.28f, 1f);

    [MenuItem("Assets/Create/Skills/Skill Node Details Panel Prefab")]
    public static void CreatePrefabAsset() => SavePrefab();

    [MenuItem("Tools/Skills/Rebuild Skill Node Details Panel Prefab")]
    public static void RebuildPrefabAsset() => SavePrefab();

    [MenuItem("Tools/Skills/Install Skill Node Details Panel (Active Scene)")]
    public static void InstallInActiveScene()
    {
        if (EditorApplication.isPlaying)
        {
            Debug.LogError("[SkillNodeDetailsPanelPrefabCreator] Exit Play Mode before installing.");
            return;
        }

        Transform content = SkillsAbilityPageNewEditorPaths.FindCurrentSelectionContent();
        if (content == null && SkillsAbilityPageNewEditorPaths.EnsureGamePlaySceneLoaded())
            content = SkillsAbilityPageNewEditorPaths.FindCurrentSelectionContent();

        SkillsAbilityPageNewUI page = SkillsAbilityPageNewEditorPaths.FindPage();
        if (content == null)
        {
            string sceneHint = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            Debug.LogError(
                "[SkillNodeDetailsPanelPrefabCreator] Could not find ViewDetailsContent (details host).\n" +
                $"• Active scene: {sceneHint}\n" +
                "• Select ViewDetailsContent in the Hierarchy, then run this menu again.\n" +
                "• Path: SkillsAbilityPageNEW → BottomPanelBar → DetailsPanel → ViewDetailsContent\n" +
                "• Or drag Assets/2.Prefabs/UI/SkillsAbilityNew/SkillNodeDetailsPanelUI.prefab into that object.",
                page);
            return;
        }

        SkillNodeDetailsPanelUI existing = content.GetComponentInChildren<SkillNodeDetailsPanelUI>(true);
        if (existing != null && existing.transform.parent == content)
            Object.DestroyImmediate(existing.gameObject);

        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        GameObject instance;
        if (prefab != null)
            instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, content);
        else
        {
            instance = BuildHierarchy();
            instance.transform.SetParent(content, false);
        }

        instance.name = "SkillNodeDetailsPanelUI";
        ApplyFixedPanelSize(instance.GetComponent<RectTransform>());

        TMP_Text placeholder = content.GetComponent<TMP_Text>();
        if (placeholder != null)
            Object.DestroyImmediate(placeholder);

        SkillNodeDetailsPanelUI panel = instance.GetComponent<SkillNodeDetailsPanelUI>();
        panel.ApplyPanelSize();
        WireHorizontalScaffold(panel);
        EditorUtility.SetDirty(instance);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(instance.scene);
        Selection.activeGameObject = instance;
        Debug.Log("[SkillNodeDetailsPanelPrefabCreator] Installed details panel.", panel);
    }

    [MenuItem("Tools/Skills/Reset Skill Node Details Panel Layout (Active Scene)")]
    public static void ResetScenePanelLayout()
    {
        if (EditorApplication.isPlaying)
        {
            Debug.LogWarning("[SkillNodeDetailsPanelPrefabCreator] Exit Play Mode to reset editor layout overrides.");
            return;
        }

        SkillNodeDetailsPanelUI panel = Object.FindFirstObjectByType<SkillNodeDetailsPanelUI>(FindObjectsInactive.Include);
        if (panel == null)
        {
            Debug.LogError("[SkillNodeDetailsPanelPrefabCreator] No SkillNodeDetailsPanelUI in the active scene.");
            return;
        }

        Transform columnsRoot = panel.transform.Find("ContentRoot/ColumnsRoot");
        if (columnsRoot != null)
        {
            RevertPrefabLayoutOverrides(columnsRoot);
            for (int i = 0; i < columnsRoot.childCount; i++)
                RevertPrefabLayoutOverrides(columnsRoot.GetChild(i));
        }

        panel.ApplyPanelSize();
        panel.RefreshColumnsLayout();
        // RefreshColumnsLayout applies fixed 1/3 column widths in editor.
        EditorUtility.SetDirty(panel);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(panel.gameObject.scene);
        Debug.Log("[SkillNodeDetailsPanelPrefabCreator] Reset details panel column layout.", panel);
    }

    private static void RevertPrefabLayoutOverrides(Transform target)
    {
        if (target == null)
            return;

        GameObject go = target.gameObject;
        if (PrefabUtility.IsPartOfPrefabInstance(go))
            PrefabUtility.RevertObjectOverride(go, InteractionMode.AutomatedAction);
    }

    private static void SavePrefab()
    {
        GameObject root = BuildHierarchy();
        PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);
        AssetDatabase.SaveAssets();
        Selection.activeObject = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        Debug.Log($"[SkillNodeDetailsPanelPrefabCreator] Saved {PrefabPath}");
    }

    private static GameObject BuildHierarchy()
    {
        var root = new GameObject("SkillNodeDetailsPanelUI", typeof(RectTransform), typeof(SkillNodeDetailsPanelUI), typeof(Image));
        var rootRt = (RectTransform)root.transform;
        ApplyFixedPanelSize(rootRt);
        root.GetComponent<Image>().color = PanelBg;

        var empty = CreateChild(rootRt, "EmptyStateRoot");
        Stretch(empty);
        CreateBodyText(empty, "EmptyStateText", DefaultEmptyMessage, SkillNodeDetailsPanelUI.FontBody, FontStyles.Italic, TextAlignmentOptions.TopLeft, MutedText);

        var content = CreateChild(rootRt, "ContentRoot");
        Stretch(content);
        var contentVlg = content.gameObject.AddComponent<VerticalLayoutGroup>();
        contentVlg.padding = new RectOffset(8, 8, 6, 6);
        contentVlg.spacing = 0;
        contentVlg.childAlignment = TextAnchor.UpperLeft;
        contentVlg.childControlWidth = true;
        contentVlg.childControlHeight = true;
        contentVlg.childForceExpandWidth = true;
        contentVlg.childForceExpandHeight = true;

        var columns = CreateChild(content, "ColumnsRoot");
        Stretch(columns);
        AddLayoutElement(columns, flexibleHeight: 1, minHeight: PanelHeight - 12);
        var columnsHlg = columns.gameObject.AddComponent<HorizontalLayoutGroup>();
        columnsHlg.spacing = ColumnSpacing;
        columnsHlg.childAlignment = TextAnchor.UpperLeft;
        columnsHlg.childControlWidth = true;
        columnsHlg.childControlHeight = true;
        columnsHlg.childForceExpandWidth = true;
        columnsHlg.childForceExpandHeight = true;

        LeftSectionRefs leftRefs = BuildLeftSection(columns);
        AddColumnDivider(columns);
        MiddleSectionRefs middleRefs = BuildMiddleSection(columns);
        AddColumnDivider(columns);
        RightSectionRefs rightRefs = BuildRightSection(columns);

        SkillNodeDetailsPanelUI panel = root.GetComponent<SkillNodeDetailsPanelUI>();
        WireComponent(
            panel,
            empty.gameObject,
            content.gameObject,
            leftRefs,
            middleRefs,
            rightRefs);

        panel.ApplyPanelSize();
        panel.RefreshColumnsLayout();
        return root;
    }

    private static LeftSectionRefs BuildLeftSection(RectTransform columns)
    {
        var left = CreateEqualSection(columns, "LeftSection");
        AddVerticalSection(left, spacing: 6);

        var topRow = CreateChild(left, "TopRow");
        AddLayoutElement(topRow, preferredHeight: 76);
        var topHlg = topRow.gameObject.AddComponent<HorizontalLayoutGroup>();
        topHlg.spacing = 8;
        topHlg.childAlignment = TextAnchor.UpperLeft;
        topHlg.childControlWidth = false;
        topHlg.childControlHeight = true;

        Image skillIcon = CreateIcon(topRow, "SkillIcon", 60);

        var nameBlock = CreateChild(topRow, "NameBlock");
        AddLayoutElement(nameBlock, flexibleWidth: 1);
        var nameVlg = nameBlock.gameObject.AddComponent<VerticalLayoutGroup>();
        nameVlg.spacing = 2;
        nameVlg.childAlignment = TextAnchor.UpperLeft;
        nameVlg.childControlWidth = true;
        nameVlg.childControlHeight = true;

        TMP_Text nameText = CreateBodyText(nameBlock, "NameText", "Power Slash Lv 5", SkillNodeDetailsPanelUI.FontNameTitle, FontStyles.Bold, TextAlignmentOptions.TopLeft, BodyText);
        TMP_Text typeText = CreateBodyText(nameBlock, "TypeText", "Ability", SkillNodeDetailsPanelUI.FontMeta, FontStyles.Normal, TextAlignmentOptions.TopLeft, MutedText);
        TMP_Text unlockStateText = CreateBodyText(nameBlock, "UnlockStateText", "Node Unlocked", SkillNodeDetailsPanelUI.FontMeta, FontStyles.Normal, TextAlignmentOptions.TopLeft, new Color(0.35f, 0.85f, 0.45f));

        AddDivider(left);
        TMP_Text descriptionText = AddDescriptionSection(left, "Primes your next hit to perform a powerful slash.");
        AddDivider(left);
        SectionRefs requirements = AddRequirementsSection(left);
        AddDivider(left);
        SectionRefs typeSection = AddTypeSection(left, "Type: Active");

        return new LeftSectionRefs
        {
            skillIcon = skillIcon,
            nameText = nameText,
            typeText = typeText,
            unlockStateText = unlockStateText,
            descriptionText = descriptionText,
            requirementsSectionRoot = requirements.root,
            requirementWeaponText = requirements.valueText,
            typeSectionRoot = typeSection.root,
            typeValueText = typeSection.valueText
        };
    }

    private struct LeftSectionRefs
    {
        public Image skillIcon;
        public TMP_Text nameText;
        public TMP_Text typeText;
        public TMP_Text unlockStateText;
        public TMP_Text descriptionText;
        public GameObject requirementsSectionRoot;
        public TMP_Text requirementWeaponText;
        public GameObject typeSectionRoot;
        public TMP_Text typeValueText;
    }

    private struct SectionRefs
    {
        public GameObject root;
        public TMP_Text valueText;
    }

    private static MiddleSectionRefs BuildMiddleSection(RectTransform columns)
    {
        var middle = CreateEqualSection(columns, "MiddleSection");
        AddVerticalSection(middle, spacing: 6);

        SectionRefs scaling = AddSimpleSection(middle, "ScalingSection", "SCALING", "ScalingText",
            "<color=#B0C8DD>Deals 150% of your weapon damage</color>");
        AddDivider(middle);
        SectionRefs effect = AddSimpleSection(middle, "EffectSection", "EFFECT", "EffectText",
            "20 Physical damage on hit");
        AddDivider(middle);
        RectTransform costCooldownRow = CreateChild(middle, "CostCooldownRow");
        AddHorizontalSectionRow(costCooldownRow, spacing: 8);
        SectionRefs cost = AddSimpleSection(costCooldownRow, "CostSection", "COST", "CostText", "40 Energy");
        SectionRefs cooldown = AddSimpleSection(costCooldownRow, "CooldownSection", "COOLDOWN", "CooldownText", "10s");

        return new MiddleSectionRefs
        {
            scalingSectionRoot = scaling.root,
            scalingText = scaling.valueText,
            effectSectionRoot = effect.root,
            effectText = effect.valueText,
            costSectionRoot = cost.root,
            costText = cost.valueText,
            cooldownSectionRoot = cooldown.root,
            cooldownText = cooldown.valueText
        };
    }

    private struct MiddleSectionRefs
    {
        public GameObject scalingSectionRoot;
        public TMP_Text scalingText;
        public GameObject effectSectionRoot;
        public TMP_Text effectText;
        public GameObject costSectionRoot;
        public TMP_Text costText;
        public GameObject cooldownSectionRoot;
        public TMP_Text cooldownText;
    }

    private static RightSectionRefs BuildRightSection(RectTransform columns)
    {
        var right = CreateEqualSection(columns, "RightSection");
        var rightVlg = AddVerticalSection(right, spacing: 4);
        rightVlg.childAlignment = TextAnchor.UpperLeft;
        rightVlg.childForceExpandHeight = false;

        var enhHeader = CreateChild(right, "EnhancementsHeader");
        AddLayoutElement(enhHeader, flexibleHeight: 0);
        var enhHeaderVlg = enhHeader.gameObject.AddComponent<VerticalLayoutGroup>();
        enhHeaderVlg.spacing = 2;
        enhHeaderVlg.padding = new RectOffset(0, 0, 0, 0);
        enhHeaderVlg.childAlignment = TextAnchor.UpperLeft;
        enhHeaderVlg.childControlWidth = true;
        enhHeaderVlg.childControlHeight = true;
        enhHeaderVlg.childForceExpandHeight = false;
        var enhHeaderCsf = enhHeader.gameObject.AddComponent<ContentSizeFitter>();
        enhHeaderCsf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        enhHeaderCsf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        TMP_Text enhTitle = CreateBodyText(enhHeader, "TitleText", "ENHANCEMENTS", SkillNodeDetailsPanelUI.FontEnhancementHeader, FontStyles.Bold, TextAlignmentOptions.TopLeft, GoldTitle);
        AddLayoutElement(enhTitle.rectTransform, preferredHeight: 18, flexibleHeight: 0);
        TMP_Text enhSubtitle = CreateBodyText(enhHeader, "SubtitleText", "Unlocked at higher levels", SkillNodeDetailsPanelUI.FontEnhancementSubtitle, FontStyles.Italic, TextAlignmentOptions.TopLeft, MutedText);
        AddLayoutElement(enhSubtitle.rectTransform, preferredHeight: 17, flexibleHeight: 0);

        Image lockedOverlay = CreateEnhancementsLockedOverlay(right);

        var buttonsContainer = CreateChild(right, "EnhancementButtonsContainer");
        AddLayoutElement(buttonsContainer, preferredHeight: 88, flexibleHeight: 0, minHeight: 88);
        var buttonsHlg = buttonsContainer.gameObject.AddComponent<HorizontalLayoutGroup>();
        buttonsHlg.spacing = 8;
        buttonsHlg.childAlignment = TextAnchor.UpperCenter;
        buttonsHlg.childControlWidth = true;
        buttonsHlg.childControlHeight = true;
        buttonsHlg.childForceExpandWidth = true;
        buttonsHlg.childForceExpandHeight = false;

        SkillNodeDetailsEnhancementCardUI buttonTemplate = BuildEnhancementButton(
            buttonsContainer,
            "EnhancementButtonTemplate",
            "Relentless Flow",
            "Lv 13");
        buttonTemplate.gameObject.SetActive(false);

        var detailRoot = CreateChild(right, "EnhancementDetailRoot");
        AddLayoutElement(detailRoot, flexibleHeight: 1, flexibleWidth: 1, minHeight: 32);
        var detailVlg = detailRoot.gameObject.AddComponent<VerticalLayoutGroup>();
        detailVlg.padding = new RectOffset(2, 2, 4, 2);
        detailVlg.childAlignment = TextAnchor.UpperLeft;
        detailVlg.childControlWidth = true;
        detailVlg.childControlHeight = true;
        detailVlg.childForceExpandWidth = true;
        detailVlg.childForceExpandHeight = true;
        TMP_Text detailText = CreateBodyText(detailRoot, "EnhancementDetailText", string.Empty, SkillNodeDetailsPanelUI.FontEnhancementDetail, FontStyles.Normal, TextAlignmentOptions.TopLeft, BodyText);
        detailText.textWrappingMode = TextWrappingModes.Normal;
        AddLayoutElement(detailText.rectTransform, flexibleHeight: 1, flexibleWidth: 1);

        Button changeBtn = CreateButton(right, "ChangeEnhancementButton", "SELECT ENHANCEMENT", 0, 28);
        AddLayoutElement(changeBtn.GetComponent<RectTransform>(), preferredHeight: 28, flexibleHeight: 0);

        return new RightSectionRefs
        {
            enhancementsSectionRoot = right,
            enhancementsTitleText = enhTitle,
            enhancementsSubtitleText = enhSubtitle,
            enhancementsLockedOverlay = lockedOverlay,
            enhancementButtonsContainer = buttonsContainer,
            enhancementButtonTemplate = buttonTemplate,
            enhancementDetailRoot = detailRoot.gameObject,
            enhancementDetailText = detailText,
            changeEnhancementButton = changeBtn
        };
    }

    private static Image CreateEnhancementsLockedOverlay(RectTransform rightSection)
    {
        var overlay = CreateChild(rightSection, "EnhancementsLockedOverlay");
        Stretch(overlay);
        var ignoreLayout = overlay.gameObject.AddComponent<LayoutElement>();
        ignoreLayout.ignoreLayout = true;
        var img = overlay.gameObject.AddComponent<Image>();
        img.color = new Color(0.72f, 0.1f, 0.08f, 0.48f);
        img.raycastTarget = false;
        overlay.gameObject.SetActive(false);
        return img;
    }

    private struct RightSectionRefs
    {
        public RectTransform enhancementsSectionRoot;
        public TMP_Text enhancementsTitleText;
        public TMP_Text enhancementsSubtitleText;
        public Image enhancementsLockedOverlay;
        public RectTransform enhancementButtonsContainer;
        public SkillNodeDetailsEnhancementCardUI enhancementButtonTemplate;
        public GameObject enhancementDetailRoot;
        public TMP_Text enhancementDetailText;
        public Button changeEnhancementButton;
    }

    private static SkillNodeDetailsEnhancementCardUI BuildEnhancementButton(
        RectTransform parent,
        string name,
        string title,
        string levelLabel)
    {
        var root = CreateChild(parent, name);
        AddLayoutElement(root, flexibleWidth: 1, minWidth: 76, preferredWidth: 96, preferredHeight: SkillNodeDetailsPanelUI.EnhancementCardHeight, flexibleHeight: 0);

        var highlight = root.gameObject.AddComponent<Image>();
        highlight.color = new Color(0.15f, 0.13f, 0.11f, 0.35f);

        var btn = root.gameObject.AddComponent<Button>();
        btn.targetGraphic = highlight;

        var vlg = root.gameObject.AddComponent<VerticalLayoutGroup>();
        vlg.padding = new RectOffset(4, 4, 6, 4);
        vlg.spacing = 2;
        vlg.childAlignment = TextAnchor.UpperCenter;
        vlg.childControlWidth = true;
        vlg.childControlHeight = true;
        vlg.childForceExpandHeight = false;

        Image icon = CreateIcon(root, "EnhancementIcon", 50);

        var levelTagRt = CreateChild(root, "LevelTag");
        AddLayoutElement(levelTagRt, preferredHeight: 16, preferredWidth: 52);
        var levelBg = levelTagRt.gameObject.AddComponent<Image>();
        levelBg.color = new Color(0.22f, 0.14f, 0.32f, 0.95f);
        TMP_Text levelTag = CreateBodyText(levelTagRt, "LevelTagText", levelLabel, SkillNodeDetailsPanelUI.FontEnhancementCardLevel, FontStyles.Bold, TextAlignmentOptions.Center, BodyText);
        Stretch(levelTag.rectTransform);

        TMP_Text cardName = CreateBodyText(root, "EnhancementName", title, SkillNodeDetailsPanelUI.FontEnhancementCardName, FontStyles.Bold, TextAlignmentOptions.Center, GoldTitle);

        var cardUi = root.gameObject.AddComponent<SkillNodeDetailsEnhancementCardUI>();
        WireEnhancementButton(cardUi, btn, highlight, icon, levelTag, cardName);
        return cardUi;
    }

    private static void WireEnhancementButton(
        SkillNodeDetailsEnhancementCardUI cardUi,
        Button button,
        Image highlightFrame,
        Image icon,
        TMP_Text levelTag,
        TMP_Text name)
    {
        SerializedObject so = new SerializedObject(cardUi);
        so.FindProperty("button").objectReferenceValue = button;
        so.FindProperty("highlightFrame").objectReferenceValue = highlightFrame;
        so.FindProperty("iconImage").objectReferenceValue = icon;
        so.FindProperty("levelTagText").objectReferenceValue = levelTag;
        so.FindProperty("nameText").objectReferenceValue = name;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void WireComponent(
        SkillNodeDetailsPanelUI panel,
        GameObject emptyRoot,
        GameObject contentRoot,
        LeftSectionRefs left,
        MiddleSectionRefs middle,
        RightSectionRefs right)
    {
        SerializedObject so = new SerializedObject(panel);
        so.FindProperty("emptyStateRoot").objectReferenceValue = emptyRoot;
        so.FindProperty("contentRoot").objectReferenceValue = contentRoot;
        so.FindProperty("skillIconImage").objectReferenceValue = left.skillIcon;
        so.FindProperty("nameText").objectReferenceValue = left.nameText;
        so.FindProperty("typeText").objectReferenceValue = left.typeText;
        so.FindProperty("unlockStateText").objectReferenceValue = left.unlockStateText;
        so.FindProperty("descriptionText").objectReferenceValue = left.descriptionText;
        so.FindProperty("requirementsSectionRoot").objectReferenceValue = left.requirementsSectionRoot;
        so.FindProperty("requirementWeaponText").objectReferenceValue = left.requirementWeaponText;
        so.FindProperty("typeSectionRoot").objectReferenceValue = left.typeSectionRoot;
        so.FindProperty("typeValueText").objectReferenceValue = left.typeValueText;
        so.FindProperty("scalingSectionRoot").objectReferenceValue = middle.scalingSectionRoot;
        so.FindProperty("scalingText").objectReferenceValue = middle.scalingText;
        so.FindProperty("effectSectionRoot").objectReferenceValue = middle.effectSectionRoot;
        so.FindProperty("effectText").objectReferenceValue = middle.effectText;
        so.FindProperty("costSectionRoot").objectReferenceValue = middle.costSectionRoot;
        so.FindProperty("costText").objectReferenceValue = middle.costText;
        so.FindProperty("cooldownSectionRoot").objectReferenceValue = middle.cooldownSectionRoot;
        so.FindProperty("cooldownText").objectReferenceValue = middle.cooldownText;
        so.FindProperty("enhancementsSectionRoot").objectReferenceValue = right.enhancementsSectionRoot;
        so.FindProperty("enhancementsTitleText").objectReferenceValue = right.enhancementsTitleText;
        so.FindProperty("enhancementsSubtitleText").objectReferenceValue = right.enhancementsSubtitleText;
        so.FindProperty("enhancementsLockedOverlay").objectReferenceValue = right.enhancementsLockedOverlay;
        so.FindProperty("enhancementButtonsContainer").objectReferenceValue = right.enhancementButtonsContainer;
        so.FindProperty("enhancementButtonTemplate").objectReferenceValue = right.enhancementButtonTemplate;
        so.FindProperty("enhancementDetailRoot").objectReferenceValue = right.enhancementDetailRoot;
        so.FindProperty("enhancementDetailText").objectReferenceValue = right.enhancementDetailText;
        so.FindProperty("changeEnhancementButton").objectReferenceValue = right.changeEnhancementButton;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static TMP_Text AddDescriptionSection(RectTransform parent, string body)
    {
        var section = CreateChild(parent, "DescriptionSection");
        AddSectionLayout(section);
        CreateBodyText(section, "SectionTitle", "DESCRIPTION", SkillNodeDetailsPanelUI.FontSectionHeader, FontStyles.Bold, TextAlignmentOptions.TopLeft, GoldTitle);
        return CreateBodyText(section, "DescriptionText", body, SkillNodeDetailsPanelUI.FontBody, FontStyles.Normal, TextAlignmentOptions.TopLeft, BodyText);
    }

    private static SectionRefs AddRequirementsSection(RectTransform parent)
    {
        var section = CreateChild(parent, "RequirementsSection");
        AddSectionLayout(section);
        CreateBodyText(section, "SectionTitle", "REQUIREMENTS", SkillNodeDetailsPanelUI.FontSectionHeader, FontStyles.Bold, TextAlignmentOptions.TopLeft, GoldTitle);
        TMP_Text weaponReq = CreateBodyText(section, "RequirementWeaponText",
            "<color=#55DD55>Required: Melee weapon</color>", SkillNodeDetailsPanelUI.FontBody, FontStyles.Normal, TextAlignmentOptions.TopLeft, BodyText);
        weaponReq.richText = true;
        return new SectionRefs { root = section.gameObject, valueText = weaponReq };
    }

    private static SectionRefs AddTypeSection(RectTransform parent, string value)
    {
        var section = CreateChild(parent, "TypeSection");
        AddSectionLayout(section);
        CreateBodyText(section, "SectionTitle", "TYPE", SkillNodeDetailsPanelUI.FontSectionHeader, FontStyles.Bold, TextAlignmentOptions.TopLeft, GoldTitle);
        TMP_Text typeValue = CreateBodyText(section, "TypeValueText", value, SkillNodeDetailsPanelUI.FontBody, FontStyles.Normal, TextAlignmentOptions.TopLeft, BodyText);
        return new SectionRefs { root = section.gameObject, valueText = typeValue };
    }

    private static SectionRefs AddSimpleSection(RectTransform parent, string sectionName, string title, string valueFieldName, string body)
    {
        var section = CreateChild(parent, sectionName);
        AddSectionLayout(section);
        CreateBodyText(section, "SectionTitle", title, 11, FontStyles.Bold, TextAlignmentOptions.TopLeft, GoldTitle);
        TMP_Text value = CreateBodyText(section, valueFieldName, body, 12, FontStyles.Normal, TextAlignmentOptions.TopLeft, BodyText);
        value.richText = true;
        return new SectionRefs { root = section.gameObject, valueText = value };
    }

    private static void AddSectionLayout(RectTransform section)
    {
        var vlg = section.gameObject.AddComponent<VerticalLayoutGroup>();
        vlg.spacing = 4;
        vlg.childAlignment = TextAnchor.UpperLeft;
        vlg.childControlWidth = true;
        vlg.childControlHeight = true;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;
    }

    private static RectTransform CreateEqualSection(RectTransform parent, string name)
    {
        var section = CreateChild(parent, name);
        float columnWidth = (PanelWidth - 2f) / 3f;
        AddLayoutElement(section, preferredWidth: columnWidth, flexibleWidth: 0, flexibleHeight: 1);
        var img = section.gameObject.AddComponent<Image>();
        img.color = new Color(0.13f, 0.12f, 0.1f, 0.28f);
        return section;
    }

    private static void AddColumnDivider(RectTransform parent)
    {
        var divider = CreateChild(parent, "ColumnDivider");
        SkillNodeDetailsPanelUI.PrepareHorizontalLayoutDivider(divider);
        var img = divider.gameObject.AddComponent<Image>();
        img.color = DividerColor;
    }

    private static VerticalLayoutGroup AddVerticalSection(RectTransform section, float spacing)
    {
        var vlg = section.gameObject.AddComponent<VerticalLayoutGroup>();
        vlg.padding = new RectOffset(6, 6, 6, 6);
        vlg.spacing = spacing;
        vlg.childAlignment = TextAnchor.UpperLeft;
        vlg.childControlWidth = true;
        vlg.childControlHeight = true;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;
        return vlg;
    }

    private static HorizontalLayoutGroup AddHorizontalSectionRow(RectTransform section, float spacing)
    {
        var hlg = section.gameObject.AddComponent<HorizontalLayoutGroup>();
        hlg.padding = new RectOffset(0, 0, 0, 0);
        hlg.spacing = spacing;
        hlg.childAlignment = TextAnchor.UpperLeft;
        hlg.childControlWidth = true;
        hlg.childControlHeight = true;
        hlg.childForceExpandWidth = true;
        hlg.childForceExpandHeight = false;
        return hlg;
    }

    private static void ApplyFixedPanelSize(RectTransform rt)
    {
        if (rt == null)
            return;

        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = new Vector2(PanelWidth, PanelHeight);

        LayoutElement le = rt.gameObject.GetComponent<LayoutElement>();
        if (le == null)
            le = rt.gameObject.AddComponent<LayoutElement>();
        le.preferredWidth = PanelWidth;
        le.preferredHeight = PanelHeight;
        le.minWidth = PanelWidth;
        le.minHeight = PanelHeight;
        le.flexibleWidth = 0f;
        le.flexibleHeight = 0f;
    }

    private static void AddDivider(RectTransform parent)
    {
        var divider = CreateChild(parent, "Divider");
        SkillNodeDetailsPanelUI.PrepareVerticalLayoutDivider(divider);
        var img = divider.gameObject.AddComponent<Image>();
        img.color = DividerColor;
        img.raycastTarget = false;
    }

    private static Image CreateIcon(RectTransform parent, string name, float size)
    {
        var rt = CreateChild(parent, name);
        AddLayoutElement(rt, preferredWidth: size, preferredHeight: size);
        var frame = rt.gameObject.AddComponent<Image>();
        frame.color = IconFrame;
        var iconChild = CreateChild(rt, "Icon");
        Stretch(iconChild);
        iconChild.offsetMin = new Vector2(2, 2);
        iconChild.offsetMax = new Vector2(-2, -2);
        var icon = iconChild.gameObject.AddComponent<Image>();
        icon.color = Color.white;
        icon.preserveAspect = true;
        return icon;
    }

    private static Button CreateButton(RectTransform parent, string name, string label, float width, float height)
    {
        var rt = CreateChild(parent, name);
        if (width > 0)
            AddLayoutElement(rt, preferredWidth: width, preferredHeight: height);
        else
            AddLayoutElement(rt, preferredHeight: height);

        var img = rt.gameObject.AddComponent<Image>();
        img.color = new Color(0.2f, 0.17f, 0.14f, 1f);
        var btn = rt.gameObject.AddComponent<Button>();
        btn.targetGraphic = img;

        var textRt = CreateChild(rt, "Text");
        Stretch(textRt);
        var tmp = textRt.gameObject.AddComponent<TextMeshProUGUI>();
        tmp.text = label;
        tmp.fontSize = SkillNodeDetailsPanelUI.FontEnhancementButton;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = GoldTitle;
        return btn;
    }

    private static TMP_Text CreateBodyText(
        RectTransform parent,
        string name,
        string text,
        float size,
        FontStyles style,
        TextAlignmentOptions align,
        Color color)
    {
        var rt = CreateChild(parent, name);
        var tmp = rt.gameObject.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = size;
        tmp.fontStyle = style;
        tmp.alignment = align;
        tmp.color = color;
        tmp.richText = true;
        tmp.textWrappingMode = TextWrappingModes.Normal;
        return tmp;
    }

    private static void AddLayoutElement(RectTransform rt, float preferredWidth = -1, float preferredHeight = -1,
        float flexibleWidth = -1, float flexibleHeight = -1, float minWidth = -1, float minHeight = -1)
    {
        var le = rt.gameObject.GetComponent<LayoutElement>();
        if (le == null)
            le = rt.gameObject.AddComponent<LayoutElement>();
        if (preferredWidth >= 0) le.preferredWidth = preferredWidth;
        if (preferredHeight >= 0) le.preferredHeight = preferredHeight;
        if (flexibleWidth >= 0) le.flexibleWidth = flexibleWidth;
        if (flexibleHeight >= 0) le.flexibleHeight = flexibleHeight;
        if (minWidth >= 0) le.minWidth = minWidth;
        if (minHeight >= 0) le.minHeight = minHeight;
    }

    private const string DefaultEmptyMessage = "Click a node to view details";

    private static void WireHorizontalScaffold(SkillNodeDetailsPanelUI panel)
    {
        HorizontalSkillTreeScaffoldUI scaffold =
            Object.FindFirstObjectByType<HorizontalSkillTreeScaffoldUI>(FindObjectsInactive.Include);
        if (scaffold == null || panel == null)
            return;

        SerializedObject so = new SerializedObject(scaffold);
        so.FindProperty("detailsPanel").objectReferenceValue = panel;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static RectTransform CreateChild(RectTransform parent, string name)
    {
        var child = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        child.SetParent(parent, false);
        return child;
    }

    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        rt.localScale = Vector3.one;
    }
}
#endif
