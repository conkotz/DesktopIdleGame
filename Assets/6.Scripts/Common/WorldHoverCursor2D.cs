using System;
using UnityEngine;
using UnityEngine.EventSystems;

[DisallowMultipleComponent]
public class WorldHoverCursor2D : MonoBehaviour
{
    private enum HoverCursorType
    {
        None,
        Npc,
        Enter,
        Mining,
        Woodcutting,
        Fishing,
        Enemy
    }

    [Header("Picking")]
    [Tooltip("Leave empty to use Camera.main. Should match the camera used by WorldInputRouter2D.")]
    [SerializeField] private Camera cam;

    [Tooltip("Layers that contain NPC, resource, and enemy hover colliders.")]
    [SerializeField] private LayerMask hoverMask = ~0;

    [Tooltip("Do not change the cursor while the mouse is over UI.")]
    [SerializeField] private bool blockWhenPointerOverUI = true;

    [Header("Tags")]
    [SerializeField] private string npcTag = "NPC";
    [SerializeField] private string resourceTag = "Resource";
    [SerializeField] private string enemyTag = "Enemy";
    [SerializeField] private string enterTag = "Cave";
    [Tooltip("Tag for sign posts / readables. Shares the Enter cursor with Cave.")]
    [SerializeField] private string signPostTag = "SignPost";

    [Header("Cursor Sprites")]
    [SerializeField] private Sprite npcCursor;
    [Tooltip("Generic enter/interact cursor (used for Cave and SignPost tags).")]
    [SerializeField] private Sprite enterCursor;
    [SerializeField] private Sprite miningCursor;
    [SerializeField] private Sprite woodcuttingCursor;
    [SerializeField] private Sprite fishingCursor;
    [SerializeField] private Sprite enemyCursor;

    [Header("Cursor Hotspot")]
    [Tooltip("Normalized hotspot in the cursor sprite. (0,0) is top-left, (0.5,0.5) is center.")]
    [SerializeField] private Vector2 normalizedHotspot = Vector2.zero;

    private Texture2D _npcTexture;
    private Texture2D _enterTexture;
    private Texture2D _miningTexture;
    private Texture2D _woodcuttingTexture;
    private Texture2D _fishingTexture;
    private Texture2D _enemyTexture;
    private HoverCursorType _currentType = HoverCursorType.None;

    [SerializeField] private Camera stripCamera;

    private void Awake()
    {
        if (!cam)
            cam = Camera.main;
        if (!stripCamera)
        {
            var go = GameObject.Find("StripCamera");
            if (go)
                stripCamera = go.GetComponent<Camera>();
        }

        RebuildCursorTextures();
    }

    private bool IsExpandBackgroundOutsideStripHover()
    {
        if (!ToggleSettingsStore.Get(ToggleSettingId.ExpandStripBackground))
            return false;

        if (!stripCamera)
        {
            var go = GameObject.Find("StripCamera");
            if (go)
                stripCamera = go.GetComponent<Camera>();
        }

        return stripCamera && !stripCamera.pixelRect.Contains(Input.mousePosition);
    }

    private void OnValidate()
    {
        normalizedHotspot.x = Mathf.Clamp01(normalizedHotspot.x);
        normalizedHotspot.y = Mathf.Clamp01(normalizedHotspot.y);
    }

    private void OnDisable()
    {
        ClearCursor();
    }

    private void OnDestroy()
    {
        ClearGeneratedTexture(ref _npcTexture);
        ClearGeneratedTexture(ref _enterTexture);
        ClearGeneratedTexture(ref _miningTexture);
        ClearGeneratedTexture(ref _woodcuttingTexture);
        ClearGeneratedTexture(ref _fishingTexture);
        ClearGeneratedTexture(ref _enemyTexture);
    }

    private void Update()
    {
        if (!cam)
            cam = Camera.main;

        HoverCursorType nextType = PickHoverTypeUnderMouse();
        if (nextType == _currentType)
            return;

        ApplyCursor(nextType);
    }

    private HoverCursorType PickHoverTypeUnderMouse()
    {
        if (!cam)
            return HoverCursorType.None;

        if (blockWhenPointerOverUI &&
            EventSystem.current != null &&
            EventSystem.current.IsPointerOverGameObject())
        {
            return HoverCursorType.None;
        }

        if (IsExpandBackgroundOutsideStripHover())
            return HoverCursorType.None;

        Vector3 world = cam.ScreenToWorldPoint(Input.mousePosition);
        Collider2D hit = WorldClickPicker2D.PickTopmostAtPoint(new Vector2(world.x, world.y), hoverMask);
        if (!hit)
            return HoverCursorType.None;

        if (HasTagInParents(hit.transform, enemyTag))
            return HoverCursorType.Enemy;

        ResourceNode resourceNode = hit.GetComponentInParent<ResourceNode>();
        if (resourceNode)
            return GetResourceCursorType(resourceNode.ActionType);

        if (HasTagInParents(hit.transform, resourceTag))
            return HoverCursorType.None;

        if (HasTagInParents(hit.transform, npcTag))
            return HoverCursorType.Npc;

        if (HasTagInParents(hit.transform, enterTag))
            return HoverCursorType.Enter;

        if (HasTagInParents(hit.transform, signPostTag))
            return HoverCursorType.Enter;

        return HoverCursorType.None;
    }

    private static HoverCursorType GetResourceCursorType(NodeAction action)
    {
        switch (action)
        {
            case NodeAction.Mining:
                return HoverCursorType.Mining;
            case NodeAction.Woodcutting:
                return HoverCursorType.Woodcutting;
            case NodeAction.Fishing:
                return HoverCursorType.Fishing;
            default:
                return HoverCursorType.None;
        }
    }

    private bool HasTagInParents(Transform start, string tagName)
    {
        if (string.IsNullOrWhiteSpace(tagName))
            return false;

        for (Transform t = start; t != null; t = t.parent)
        {
            if (string.Equals(t.gameObject.tag, tagName, StringComparison.Ordinal))
                return true;
        }

        return false;
    }

    private void ApplyCursor(HoverCursorType type)
    {
        Texture2D texture = GetTexture(type);
        if (!texture)
        {
            ClearCursor();
            return;
        }

        _currentType = type;
        Cursor.SetCursor(texture, GetHotspot(texture), CursorMode.Auto);
    }

    private void ClearCursor()
    {
        if (_currentType == HoverCursorType.None)
            return;

        _currentType = HoverCursorType.None;
        Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
    }

    private Texture2D GetTexture(HoverCursorType type)
    {
        switch (type)
        {
            case HoverCursorType.Npc:
                return _npcTexture;
            case HoverCursorType.Enter:
                return _enterTexture;
            case HoverCursorType.Mining:
                return _miningTexture;
            case HoverCursorType.Woodcutting:
                return _woodcuttingTexture;
            case HoverCursorType.Fishing:
                return _fishingTexture;
            case HoverCursorType.Enemy:
                return _enemyTexture;
            default:
                return null;
        }
    }

    private Vector2 GetHotspot(Texture2D texture)
    {
        return new Vector2(
            Mathf.Clamp01(normalizedHotspot.x) * texture.width,
            Mathf.Clamp01(normalizedHotspot.y) * texture.height);
    }

    private void RebuildCursorTextures()
    {
        ClearGeneratedTexture(ref _npcTexture);
        ClearGeneratedTexture(ref _enterTexture);
        ClearGeneratedTexture(ref _miningTexture);
        ClearGeneratedTexture(ref _woodcuttingTexture);
        ClearGeneratedTexture(ref _fishingTexture);
        ClearGeneratedTexture(ref _enemyTexture);

        _npcTexture = BuildCursorTexture(npcCursor);
        _enterTexture = BuildCursorTexture(enterCursor);
        _miningTexture = BuildCursorTexture(miningCursor);
        _woodcuttingTexture = BuildCursorTexture(woodcuttingCursor);
        _fishingTexture = BuildCursorTexture(fishingCursor);
        _enemyTexture = BuildCursorTexture(enemyCursor);
    }

    private static Texture2D BuildCursorTexture(Sprite sprite)
    {
        if (!sprite || !sprite.texture)
            return null;

        Rect rect = sprite.textureRect;
        int x = Mathf.RoundToInt(rect.x);
        int y = Mathf.RoundToInt(rect.y);
        int width = Mathf.RoundToInt(rect.width);
        int height = Mathf.RoundToInt(rect.height);

        Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
        {
            name = $"{sprite.name}_CursorTexture",
            filterMode = sprite.texture.filterMode,
            wrapMode = TextureWrapMode.Clamp
        };

        try
        {
            Color[] pixels = sprite.texture.GetPixels(x, y, width, height);
            texture.SetPixels(pixels);
            texture.Apply(false, false);
            return texture;
        }
        catch (Exception)
        {
            // Cursor sprites are often imported as non-readable. Copy through the GPU so the
            // source import settings do not need Read/Write enabled.
            CopySpriteRectWithRenderTexture(sprite.texture, texture, rect);
            return texture;
        }
    }

    private static void CopySpriteRectWithRenderTexture(Texture2D source, Texture2D destination, Rect sourceRect)
    {
        RenderTexture renderTexture = RenderTexture.GetTemporary(destination.width, destination.height, 0, RenderTextureFormat.ARGB32);
        RenderTexture previous = RenderTexture.active;

        Vector2 scale = new Vector2(sourceRect.width / source.width, sourceRect.height / source.height);
        Vector2 offset = new Vector2(sourceRect.x / source.width, sourceRect.y / source.height);

        try
        {
            Graphics.Blit(source, renderTexture, scale, offset);
            RenderTexture.active = renderTexture;
            destination.ReadPixels(new Rect(0f, 0f, destination.width, destination.height), 0, 0);
            destination.Apply(false, false);
        }
        finally
        {
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(renderTexture);
        }
    }

    private static void ClearGeneratedTexture(ref Texture2D texture)
    {
        if (!texture)
            return;

        if (Application.isPlaying)
            Destroy(texture);
        else
            DestroyImmediate(texture);

        texture = null;
    }
}
