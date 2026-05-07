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
    private bool _hasPendingWorldPosition;
    private Vector3 _pendingWorldPosition;

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
            ApplyPendingWorldPosition();
            return;
        }

        if (!stats) stats = GetComponent<CharacterStats>();
        if (!stats) return;

        // Energy/mana always start full after load (saved values were only used for HP).
        stats.ApplyLoadedVitals(_pendingHp, stats.MaxEnergy, stats.MaxMana);
        stats.SnapGuardToNaturalCapOnSessionLoad();
        _hasPendingVitals = false;
        ApplyPendingName();
        ApplyPendingWorldPosition();
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
            data.playerName = string.IsNullOrWhiteSpace(player.displayName) ? "Adventurer" : player.displayName.Trim();
            float hp = stats.HP;
            if (float.IsNaN(hp) || float.IsInfinity(hp) || hp <= 0f)
            {
                Debug.LogWarning($"[PlayerSave] SaveInto: invalid HP ({hp}); clamping to a living value.");
                hp = Mathf.Max(1f, stats.MaxHP);
            }

            data.playerCurrentHP = hp;
            // Energy/mana not persisted — always full on load (see LoadFrom / ApplyLoadedVitals).
            data.playerCurrentEnergy = -1f;
            data.playerCurrentMana = -1f;

            Vector3 pos = transform.position;
            bool validPos =
                !float.IsNaN(pos.x) && !float.IsNaN(pos.y) && !float.IsNaN(pos.z) &&
                !float.IsInfinity(pos.x) && !float.IsInfinity(pos.y) && !float.IsInfinity(pos.z);
            data.hasSavedPlayerWorldPosition = validPos;
            if (validPos)
            {
                data.playerWorldPosX = pos.x;
                data.playerWorldPosY = pos.y;
                data.playerWorldPosZ = pos.z;
            }
        }
        else
        {
            Debug.LogWarning(
                "[PlayerSave] SaveInto: CharacterStats or PlayerController missing — vitals may be corrected in SaveDataIntegrity.");
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
            if (float.IsNaN(hp) || float.IsInfinity(hp))
            {
                Debug.LogWarning($"[PlayerSave] LoadFrom: invalid saved HP ({hp}); recovering from stats.");
                hp = stats ? stats.HP : 0f;
            }

            if (hp <= 0f && stats != null)
                hp = Mathf.Max(1f, stats.MaxHP);

            _pendingHp = hp;
            _hasPendingVitals = true;
        }

        _hasPendingWorldPosition = data.hasSavedPlayerWorldPosition;
        if (_hasPendingWorldPosition)
            _pendingWorldPosition = new Vector3(data.playerWorldPosX, data.playerWorldPosY, data.playerWorldPosZ);

        ApplyPendingName();
        ApplyPendingWorldPosition();
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

    private void ApplyPendingWorldPosition()
    {
        if (!_hasPendingWorldPosition)
            return;

        transform.position = _pendingWorldPosition;
        Rigidbody2D rb = GetComponent<Rigidbody2D>();
        if (rb != null)
            rb.position = _pendingWorldPosition;

        _hasPendingWorldPosition = false;
    }
}