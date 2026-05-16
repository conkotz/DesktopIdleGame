using TMPro;
using UnityEngine;

/// <summary>
/// On the reusable <c>NameLabel</c> prefab: pulls Character Name + Role from a parent
/// <see cref="NpcIdentity"/> (or merchant name from <see cref="Merchant"/>).
/// </summary>
[ExecuteAlways]
[DisallowMultipleComponent]
[RequireComponent(typeof(TMP_Text))]
[AddComponentMenu("Desktop Idle Game/NPC/NPC Name Label Binder")]
public class NpcNameLabelBinder : MonoBehaviour
{
    private TMP_Text _label;

    private void Awake() => Refresh();

    private void OnEnable() => Refresh();

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (!Application.isPlaying)
            Refresh();
    }
#endif

    public void Refresh()
    {
        _label ??= GetComponent<TMP_Text>();
        if (!_label)
            return;

        NpcIdentity identity = GetComponentInParent<NpcIdentity>();
        if (identity)
        {
            identity.RegisterNameLabel(_label);
            identity.RefreshNameLabel();
            return;
        }

        Merchant merchant = GetComponentInParent<Merchant>();
        if (merchant)
        {
            merchant.RegisterNameLabel(_label);
            merchant.RefreshNameLabel();
        }
    }
}
