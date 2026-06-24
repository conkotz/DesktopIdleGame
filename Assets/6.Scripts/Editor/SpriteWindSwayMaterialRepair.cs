#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public static class SpriteWindSwayMaterialRepair
{
    const string ShaderName = "Custom/SpriteWindSway";
    const string Folder = "Assets/5.Art/Materials/SpriteWindSway";

    struct TreeMaterialSettings
    {
        public string FileName;
        public float WindStrength;
        public float WindSpeed;
        public float BaseAnchorHeight;
        public float PhaseOffset;
    }

    static readonly TreeMaterialSettings[] TreeMaterials =
    {
        new TreeMaterialSettings
        {
            FileName = "M_SplitwoodTree_WindSway.mat",
            WindStrength = 0.15f,
            WindSpeed = 0.32f,
            BaseAnchorHeight = 0.15f,
            PhaseOffset = 0f
        },
        new TreeMaterialSettings
        {
            FileName = "M_WildwoodTree_WindSway.mat",
            WindStrength = 0.13f,
            WindSpeed = 0.3f,
            BaseAnchorHeight = 0.15f,
            PhaseOffset = 1.2f
        },
        new TreeMaterialSettings
        {
            FileName = "M_HardwoodTree_WindSway.mat",
            WindStrength = 0.14f,
            WindSpeed = 0.24f,
            BaseAnchorHeight = 0.22f,
            PhaseOffset = 2.4f
        }
    };

    [MenuItem("Tools/Art/Repair Tree Wind Sway Materials")]
    public static void RepairFromMenu()
    {
        RepairAll(logResults: true);
    }

    [InitializeOnLoadMethod]
    static void ScheduleRepair()
    {
        EditorApplication.delayCall += () => RepairAll(logResults: false);
    }

    static void RepairAll(bool logResults)
    {
        var shader = Shader.Find(ShaderName);
        if (shader == null)
            return;

        var repairedAny = false;

        foreach (var settings in TreeMaterials)
        {
            if (!RepairMaterial($"{Folder}/{settings.FileName}", shader, settings))
                continue;

            repairedAny = true;
        }

        if (!repairedAny)
            return;

        AssetDatabase.SaveAssets();

        if (logResults)
            Debug.Log("Repaired tree wind sway materials.");
    }

    static bool RepairMaterial(string path, Shader shader, TreeMaterialSettings settings)
    {
        var material = LoadOrRecreateMaterial(path, shader, settings.FileName);
        if (material == null)
            return false;

        var needsRepair =
            material.shader != shader ||
            !Mathf.Approximately(material.GetFloat("_WindStrength"), settings.WindStrength) ||
            !Mathf.Approximately(material.GetFloat("_WindSpeed"), settings.WindSpeed) ||
            !Mathf.Approximately(material.GetFloat("_BaseAnchorHeight"), settings.BaseAnchorHeight) ||
            !Mathf.Approximately(material.GetFloat("_PhaseOffset"), settings.PhaseOffset);

        if (!needsRepair)
            return false;

        material.shader = shader;
        material.SetFloat("_WindStrength", settings.WindStrength);
        material.SetFloat("_WindSpeed", settings.WindSpeed);
        material.SetFloat("_BaseAnchorHeight", settings.BaseAnchorHeight);
        material.SetFloat("_PhaseOffset", settings.PhaseOffset);
        EditorUtility.SetDirty(material);
        return true;
    }

    static Material LoadOrRecreateMaterial(string path, Shader shader, string fileName)
    {
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material != null)
            return material;

        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material != null)
            return material;

        AssetDatabase.DeleteAsset(path);
        material = new Material(shader)
        {
            name = fileName.Replace(".mat", string.Empty)
        };
        AssetDatabase.CreateAsset(material, path);
        return material;
    }
}
#endif
