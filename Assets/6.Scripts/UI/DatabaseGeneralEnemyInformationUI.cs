using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Builds static reference content under <c>GeneralEnemyInformationContent</c>.</summary>
[DisallowMultipleComponent]
public sealed class DatabaseGeneralEnemyInformationUI : MonoBehaviour
{
    private const string CombatProfileContentName = "CombatProfileContent";
    private const string CombatProfileRowObjectName = "EnemyCombatProfileRow";

    [SerializeField] private RectTransform combatProfileListRoot;
    [Tooltip("Optional. Defaults to Assets/2.Prefabs/UI/EnemyCombatProfileRow.prefab when unset.")]
    [SerializeField] private GameObject combatProfileRowPrefab;

    private GameObject _combatProfileRowTemplate;

    private void OnEnable()
    {
        RebuildCombatProfileRows();
    }

    public void RebuildCombatProfileRows()
    {
        ResolveReferences();
        EnsureCombatProfileListLayout();

        if (!combatProfileListRoot)
            return;

        ClearSpawnedCombatProfileRows();
        CacheCombatProfileRowTemplate();
        if (!_combatProfileRowTemplate)
            return;

        var entries = DatabaseCombatProfileCatalog.GetAllEntries();
        for (int i = 0; i < entries.Count; i++)
        {
            (string label, string description) = entries[i];
            GameObject rowGo = Instantiate(_combatProfileRowTemplate, combatProfileListRoot);
            rowGo.SetActive(true);
            rowGo.name = $"{CombatProfileRowObjectName}_{label.Replace(' ', '_')}";

            if (!rowGo.TryGetComponent(out DatabaseCombatProfileRowUI rowUi))
                rowUi = rowGo.AddComponent<DatabaseCombatProfileRowUI>();
            rowUi.Bind(label, description);
        }

        if (_combatProfileRowTemplate != null && _combatProfileRowTemplate.transform.parent == combatProfileListRoot)
            _combatProfileRowTemplate.SetActive(false);

        LayoutRebuilder.ForceRebuildLayoutImmediate(combatProfileListRoot);
    }

    private void ResolveReferences()
    {
        if (!combatProfileListRoot)
        {
            Transform found = FindDeepChildByTrimmedName(transform, CombatProfileContentName);
            combatProfileListRoot = found as RectTransform;
        }
    }

    private void EnsureCombatProfileListLayout()
    {
        if (!combatProfileListRoot)
            return;

        if (combatProfileListRoot.TryGetComponent(out TMP_Text placeholderText))
        {
            placeholderText.text = string.Empty;
            placeholderText.enabled = false;
        }

        VerticalLayoutGroup layout = combatProfileListRoot.GetComponent<VerticalLayoutGroup>();
        if (!layout)
            layout = combatProfileListRoot.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.enabled = true;
        layout.childAlignment = TextAnchor.UpperLeft;
        layout.spacing = 4f;
        layout.padding = new RectOffset(0, 0, 0, 0);
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        layout.childControlWidth = true;
        layout.childControlHeight = true;

        ContentSizeFitter fitter = combatProfileListRoot.GetComponent<ContentSizeFitter>();
        if (!fitter)
            fitter = combatProfileListRoot.gameObject.AddComponent<ContentSizeFitter>();
        fitter.enabled = true;
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
    }

    private void CacheCombatProfileRowTemplate()
    {
        if (_combatProfileRowTemplate)
            return;

        if (combatProfileListRoot)
        {
            Transform existing = combatProfileListRoot.Find(CombatProfileRowObjectName);
            if (!existing)
            {
                for (int i = 0; i < combatProfileListRoot.childCount; i++)
                {
                    Transform child = combatProfileListRoot.GetChild(i);
                    if (child != null && child.name.StartsWith(CombatProfileRowObjectName, System.StringComparison.Ordinal))
                    {
                        existing = child;
                        break;
                    }
                }
            }

            if (existing)
            {
                _combatProfileRowTemplate = existing.gameObject;
                return;
            }
        }

        if (!combatProfileRowPrefab)
            combatProfileRowPrefab = DatabasePageUI.ResolveCombatProfileRowPrefab();

        if (combatProfileRowPrefab)
            _combatProfileRowTemplate = combatProfileRowPrefab;
    }

    private void ClearSpawnedCombatProfileRows()
    {
        if (!combatProfileListRoot)
            return;

        for (int i = combatProfileListRoot.childCount - 1; i >= 0; i--)
        {
            Transform child = combatProfileListRoot.GetChild(i);
            if (child == null)
                continue;

            if (string.Equals(child.name, CombatProfileRowObjectName, System.StringComparison.Ordinal))
            {
                if (_combatProfileRowTemplate == null)
                    _combatProfileRowTemplate = child.gameObject;
                continue;
            }

            if (child.name.StartsWith(CombatProfileRowObjectName + "_", System.StringComparison.Ordinal))
                Destroy(child.gameObject);
        }
    }

    private static Transform FindDeepChildByTrimmedName(Transform root, string wantedName)
    {
        if (!root || string.IsNullOrWhiteSpace(wantedName))
            return null;

        for (int i = 0; i < root.childCount; i++)
        {
            Transform child = root.GetChild(i);
            if (child == null)
                continue;

            if (string.Equals(child.name.Trim(), wantedName, System.StringComparison.OrdinalIgnoreCase))
                return child;

            Transform nested = FindDeepChildByTrimmedName(child, wantedName);
            if (nested)
                return nested;
        }

        return null;
    }
}
