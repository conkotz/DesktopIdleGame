using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Build-time bundle of enemy portrait sprites so database avatars resolve in player builds.
/// Populated via <c>Tools/Database/Rebuild Enemy Avatar Catalog</c> or automatically before builds.
/// </summary>
[CreateAssetMenu(menuName = "Desktop Idle Game/Database/Enemy Avatar Catalog", fileName = "DatabaseEnemyAvatarCatalog")]
public sealed class DatabaseEnemyAvatarCatalog : ScriptableObject
{
    [SerializeField] private List<Sprite> avatars = new();

    public IReadOnlyList<Sprite> Avatars => avatars;

#if UNITY_EDITOR
    public void SetAvatarsForEditor(List<Sprite> sprites)
    {
        avatars = sprites ?? new List<Sprite>();
        UnityEditor.EditorUtility.SetDirty(this);
    }
#endif
}
