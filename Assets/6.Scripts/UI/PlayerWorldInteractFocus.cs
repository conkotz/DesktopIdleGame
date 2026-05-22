using UnityEngine;

/// <summary>
/// Non-combat interact target (NPC, storage, notice board, merchant walk-to).
/// Cleared when the player moves manually, like a resource node target dropping on reposition.
/// </summary>
[DisallowMultipleComponent]
public class PlayerWorldInteractFocus : MonoBehaviour
{
    private Transform _focusTransform;

    public Transform CurrentFocusTransform => _focusTransform;

    public void SetFocus(Transform focusRoot)
    {
        if (!focusRoot)
        {
            ClearFocus();
            return;
        }

        _focusTransform = focusRoot;
    }

    public void ClearFocus()
    {
        _focusTransform = null;
    }

    /// <summary>Called when the player clicks or uses the interact hotkey on a world object (not enemies).</summary>
    public static void TrySetFocusFromCollider(PlayerController player, Collider2D col)
    {
        if (!player || !col)
            return;

        if (col.GetComponentInParent<EnemyClick>())
            return;
        if (col.GetComponentInParent<ItemDrop>())
            return;
        if (col.GetComponentInParent<MapNodePortalTeleporter>())
            return;
        if (col.GetComponentInParent<ResourceNode>())
            return;

        PlayerWorldInteractFocus focus = player.GetComponent<PlayerWorldInteractFocus>();
        if (!focus)
            return;

        Transform root = WorldInteractRouter.ResolveInteractFocusRoot(col);
        if (root != null)
            focus.SetFocus(root);
    }

    public static void ClearForPlayer(PlayerController player)
    {
        if (!player)
            return;

        PlayerWorldInteractFocus focus = player.GetComponent<PlayerWorldInteractFocus>();
        focus?.ClearFocus();
    }
}
