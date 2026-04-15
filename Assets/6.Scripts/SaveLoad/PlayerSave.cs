using UnityEngine;

public class PlayerSave : MonoBehaviour, ISaveable
{
    [SerializeField] private int level = 1;
    [SerializeField] private int xp = 0;
    [SerializeField] private CharacterStats stats;
    [SerializeField] private PlayerController player;

    private bool _hasPendingVitals;
    private float _pendingHp = -1f;
    private string _pendingName;

    private void Awake()
    {
        if (!stats) stats = GetComponent<CharacterStats>();
        if (!player) player = GetComponent<PlayerController>();
    }

    private void Update()
    {
        if (!_hasPendingVitals)
        {
            ApplyPendingName();
            return;
        }

        if (!stats) stats = GetComponent<CharacterStats>();
        if (!stats) return;

        // Energy/mana always start full after load (saved values were only used for HP).
        stats.ApplyLoadedVitals(_pendingHp, stats.MaxEnergy, stats.MaxMana);
        stats.SnapGuardToNaturalCapOnSessionLoad();
        _hasPendingVitals = false;
        ApplyPendingName();
    }

    public void SaveInto(SaveData data)
    {
        if (data == null) return;
        data.playerLevel = level;
        data.xp = xp;

        if (!stats) stats = GetComponent<CharacterStats>();
        if (!player) player = GetComponent<PlayerController>();

        if (stats != null && player != null)
        {
            data.playerName = player.displayName;
            data.playerCurrentHP = stats.HP;
            // Energy/mana not persisted — always full on load (see LoadFrom / ApplyLoadedVitals).
            data.playerCurrentEnergy = -1f;
            data.playerCurrentMana = -1f;
        }
    }

    public void LoadFrom(SaveData data)
    {
        if (data == null) return;
        level = data.playerLevel;
        xp = data.xp;
        _pendingName = data.playerName;

        if (data.playerCurrentHP >= 0f || data.playerCurrentEnergy >= 0f || data.playerCurrentMana >= 0f)
        {
            if (!stats) stats = GetComponent<CharacterStats>();

            // If HP was saved as 0 (e.g. edge-case/death snapshot), recover to a valid alive value on load.
            float hp = data.playerCurrentHP >= 0f ? data.playerCurrentHP : (stats ? stats.HP : 0f);
            if (hp <= 0f && stats != null)
                hp = Mathf.Max(1f, stats.MaxHP);

            _pendingHp = hp;
            _hasPendingVitals = true;
        }

        ApplyPendingName();
    }

    private void ApplyPendingName()
    {
        if (string.IsNullOrWhiteSpace(_pendingName))
            return;

        if (!player) player = GetComponent<PlayerController>();
        if (!player) return;

        player.SetDisplayName(_pendingName.Trim());
        _pendingName = null;
    }
}