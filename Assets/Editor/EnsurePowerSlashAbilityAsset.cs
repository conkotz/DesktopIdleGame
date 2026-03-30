#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class EnsurePowerSlashAbilityAsset
{
    private const string AbilityId = "power_slash";
    private const string AssetPath = "Assets/Resources/Abilities/Ability_power_slash.asset";

    static EnsurePowerSlashAbilityAsset()
    {
        EditorApplication.delayCall += EnsureAsset;
    }

    private static void EnsureAsset()
    {
        AbilityDefinition existing = AssetDatabase.LoadAssetAtPath<AbilityDefinition>(AssetPath);
        if (existing != null)
            return;

        string dir = System.IO.Path.GetDirectoryName(AssetPath);
        if (!System.IO.Directory.Exists(dir))
            System.IO.Directory.CreateDirectory(dir);

        var def = ScriptableObject.CreateInstance<AbilityDefinition>();
        def.abilityId = AbilityId;
        def.displayName = "Power Slash";
        def.description = "A heavy strike that scales with Physical Damage and Ability Power.";
        def.sourceSkill = SkillType.Melee;
        def.unlockLevel = 5;
        def.cooldown = 5f;
        def.energyCost = 60f;
        def.physicalDamageMultiplier = 1.25f;
        def.abilityPowerMultiplier = 0.25f;

        AssetDatabase.CreateAsset(def, AssetPath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }
}
#endif

