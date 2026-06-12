using UnityEngine;

/// <summary>Runtime-loadable sprite references for UI that cannot use editor-only AssetDatabase paths.</summary>
[CreateAssetMenu(fileName = "UILoadingSpriteRefs", menuName = "UI/Loading Sprite Refs")]
public sealed class UILoadingSpriteRefs : ScriptableObject
{
    [SerializeField] private Sprite loadingCogSprite;
    [SerializeField] private Sprite loadingSpinnerSprite;

    private static UILoadingSpriteRefs s_instance;

    public static Sprite LoadingCogSprite
    {
        get
        {
            EnsureLoaded();
            return s_instance != null ? s_instance.loadingCogSprite : null;
        }
    }

    public static Sprite LoadingSpinnerSprite
    {
        get
        {
            EnsureLoaded();
            return s_instance != null ? s_instance.loadingSpinnerSprite : null;
        }
    }

    private static void EnsureLoaded()
    {
        if (s_instance)
            return;

        s_instance = Resources.Load<UILoadingSpriteRefs>("UILoadingSpriteRefs");
    }
}
