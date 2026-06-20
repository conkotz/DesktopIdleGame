#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public static class SpriteWaterSurfaceMaterialRepair
{
    const string ShaderName = "Custom/SpriteWaterSurface";
    const string SmallPondMaterialPath = "Assets/5.Art/Materials/SpriteWaterSurface/M_SmallPond_Water.mat";

    [MenuItem("Tools/Art/Repair Pond Water Materials")]
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

        if (!RepairMaterial(SmallPondMaterialPath, shader, 1.05f, 0.015f, 11f, 0.14f))
            return;

        AssetDatabase.SaveAssets();

        if (logResults)
            Debug.Log("Repaired pond water materials.");
    }

    static bool RepairMaterial(
        string path,
        Shader shader,
        float rippleSpeed,
        float rippleStrength,
        float rippleScale,
        float shimmerStrength)
    {
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            material = AssetDatabase.LoadAssetAtPath<Material>(path);
        }

        if (material == null)
            return false;

        if (material.shader == shader)
            return false;

        material.shader = shader;
        material.SetFloat("_RippleSpeed", rippleSpeed);
        material.SetFloat("_RippleStrength", rippleStrength);
        material.SetFloat("_RippleScale", rippleScale);
        material.SetFloat("_ShimmerStrength", shimmerStrength);
        EditorUtility.SetDirty(material);
        return true;
    }
}
#endif
