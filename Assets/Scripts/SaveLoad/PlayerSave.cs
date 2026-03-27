using UnityEngine;

public class PlayerSave : MonoBehaviour, ISaveable
{
    [SerializeField] private int level = 1;
    [SerializeField] private int xp = 0;
    [SerializeField] private CharacterStats stats;
    [SerializeField] private PlayerController player;

    private bool _hasPendingVitals;
    private float _pendingHp = -1f;
    private float _pendingEnergy = -1f;
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

        stats.ApplyLoadedVitals(_pendingHp, _pendingEnergy);
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
            data.playerCurrentEnergy = stats.Energy;
        }
    }

    public void LoadFrom(SaveData data)
    {
        if (data == null) return;
        level = data.playerLevel;
        xp = data.xp;
        _pendingName = data.playerName;

        if (data.playerCurrentHP >= 0f || data.playerCurrentEnergy >= 0f)
        {
            if (!stats) stats = GetComponent<CharacterStats>();

            float hp = data.playerCurrentHP >= 0f ? data.playerCurrentHP : (stats ? stats.HP : 0f);
            float energy = data.playerCurrentEnergy >= 0f ? data.playerCurrentEnergy : (stats ? stats.Energy : 0f);

            _pendingHp = hp;
            _pendingEnergy = energy;
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