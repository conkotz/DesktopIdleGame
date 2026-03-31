using UnityEngine;

public class UISectionToggleGroup : MonoBehaviour
{
    [Header("Rules")]
    [SerializeField] private bool mustKeepOneOpen = true;
    public bool MustKeepOneOpen => mustKeepOneOpen;

    [Header("Default Open")]
    [Tooltip("Which section should be open on scene load.")]
    [SerializeField] private UISectionToggle defaultSection;

    private UISectionToggle[] _sections;

    public int OpenCount
    {
        get
        {
            int c = 0;
            if (_sections == null) return 0;
            for (int i = 0; i < _sections.Length; i++)
                if (_sections[i] != null && _sections[i].IsExpanded) c++;
            return c;
        }
    }

    private void Awake()
    {
        _sections = GetComponentsInChildren<UISectionToggle>(true);
    }

    private void Start()
    {
        // Ensure exactly one is open on load (default = Offence)
        if (defaultSection != null)
        {
            OpenOnly(defaultSection);
            return;
        }

        // If no default assigned, keep the first open
        if (_sections != null && _sections.Length > 0)
            OpenOnly(_sections[0]);
    }

    public void OpenOnly(UISectionToggle target)
    {
        if (target == null) return;

        // If it's already the only open one, do nothing
        // (this also prevents closing the last open section)
        for (int i = 0; i < _sections.Length; i++)
        {
            var s = _sections[i];
            if (s == null) continue;

            bool shouldBeOpen = (s == target);
            s.SetExpandedInternal(shouldBeOpen);
        }
    }
}