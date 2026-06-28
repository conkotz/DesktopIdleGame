using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// Visual horizontal range ruler centered on the player. Session-only; toggled from the character page.
/// </summary>
[DisallowMultipleComponent]
public sealed class PlayerWeaponRangeIndicator : MonoBehaviour
{
    private const float PreviewHalfSpan = 30f;
    private const int LabelInterval = 5;
    private const float SpineHalfThickness = 0.028f;
    private const float UnitTickHalfHeight = 0.14f;
    private const float UnitTickHalfThickness = 0.018f;
    private const float WeaponTickHalfHeight = 0.38f;
    private const float WeaponTickHalfThickness = 0.055f;
    private const float LabelLocalY = -0.34f;
    private const float LabelFontSize = 2.1f;

    private static Material s_lineMaterial;

    private CharacterStats _stats;
    private Transform _visualRoot;
    private Transform _labelsRoot;
    private MeshFilter _spineMeshFilter;
    private MeshRenderer _spineMeshRenderer;
    private MeshFilter _weaponMeshFilter;
    private MeshRenderer _weaponMeshRenderer;
    private Mesh _spineMesh;
    private Mesh _weaponMesh;
    private float _lastWeaponRange = float.NaN;
    private bool _visible;

    private static readonly Color SpineColor = new Color(0.72f, 0.98f, 1f, 0.96f);
    private static readonly Color WeaponTickColor = new Color(1f, 0.94f, 0.18f, 1f);
    private static readonly Color LabelTextColor = new Color(0.98f, 0.99f, 1f, 0.98f);

    public bool IsVisible => _visible;

    private void Awake()
    {
        _stats = GetComponent<CharacterStats>();
        EnsureVisualBuilt();
        SetVisible(false);
    }

    private void OnDestroy()
    {
        if (_spineMesh != null)
            Destroy(_spineMesh);
        if (_weaponMesh != null)
            Destroy(_weaponMesh);
    }

    public void SetVisible(bool visible)
    {
        _visible = visible;
        if (_visualRoot != null)
            _visualRoot.gameObject.SetActive(visible);

        if (!visible)
        {
            _lastWeaponRange = float.NaN;
            return;
        }

        RefreshVerticalOffset();
        RebuildIfNeeded(force: true);
    }

    private void LateUpdate()
    {
        if (!_visible)
            return;

        RefreshVerticalOffset();
        RebuildIfNeeded(force: false);
    }

    private void RefreshVerticalOffset()
    {
        if (_visualRoot == null)
            return;

        float localY = 0f;
        SpriteRenderer sprite = GetComponentInChildren<SpriteRenderer>();
        if (sprite != null)
            localY = sprite.bounds.center.y - transform.position.y;

        _visualRoot.localPosition = new Vector3(0f, localY, 0f);
    }

    private void RebuildIfNeeded(bool force)
    {
        float weaponRange = _stats != null ? Mathf.Max(0f, _stats.Range) : 0f;
        if (!force && Mathf.Approximately(weaponRange, _lastWeaponRange))
            return;

        _lastWeaponRange = weaponRange;
        RebuildSpineMesh(weaponRange);
        RebuildWeaponRangeMesh(weaponRange);
    }

    private void EnsureVisualBuilt()
    {
        if (_visualRoot != null)
            return;

        var rootGo = new GameObject("WeaponRangeIndicator");
        _visualRoot = rootGo.transform;
        _visualRoot.SetParent(transform, false);

        var spineGo = new GameObject("SpineAndUnitTicks");
        spineGo.transform.SetParent(_visualRoot, false);
        _spineMeshFilter = spineGo.AddComponent<MeshFilter>();
        _spineMeshRenderer = spineGo.AddComponent<MeshRenderer>();
        _spineMesh = new Mesh { name = "WeaponRangeIndicatorSpine" };
        _spineMeshFilter.sharedMesh = _spineMesh;

        var weaponGo = new GameObject("WeaponRangeTicks");
        weaponGo.transform.SetParent(_visualRoot, false);
        _weaponMeshFilter = weaponGo.AddComponent<MeshFilter>();
        _weaponMeshRenderer = weaponGo.AddComponent<MeshRenderer>();
        _weaponMesh = new Mesh { name = "WeaponRangeIndicatorWeaponTicks" };
        _weaponMeshFilter.sharedMesh = _weaponMesh;

        Material baseMat = GetLineMaterial();
        if (baseMat != null)
        {
            _spineMeshRenderer.material = new Material(baseMat) { color = SpineColor };
            _weaponMeshRenderer.material = new Material(baseMat) { color = WeaponTickColor };
        }

        ApplyPlayerSorting(_spineMeshRenderer, 0);
        ApplyPlayerSorting(_weaponMeshRenderer, 1);

        EnsureRangeLabelsBuilt();

        _visualRoot.gameObject.SetActive(false);
    }

    private void EnsureRangeLabelsBuilt()
    {
        if (_labelsRoot != null)
            return;

        var labelsGo = new GameObject("RangeLabels");
        _labelsRoot = labelsGo.transform;
        _labelsRoot.SetParent(_visualRoot, false);

        for (int distance = LabelInterval; distance <= Mathf.RoundToInt(PreviewHalfSpan); distance += LabelInterval)
        {
            CreateRangeLabel(distance, distance);
            CreateRangeLabel(-distance, distance);
        }
    }

    private void CreateRangeLabel(float localX, int distance)
    {
        var go = new GameObject($"RangeLabel_{distance}_{(localX < 0f ? "L" : "R")}");
        go.transform.SetParent(_labelsRoot, false);
        go.transform.localPosition = new Vector3(localX, LabelLocalY, 0f);

        TextMeshPro label = go.AddComponent<TextMeshPro>();
        label.text = distance.ToString();
        label.fontSize = LabelFontSize;
        label.alignment = TextAlignmentOptions.Center;
        label.color = LabelTextColor;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.overflowMode = TextOverflowModes.Overflow;
        label.rectTransform.sizeDelta = new Vector2(1.6f, 0.8f);

        if (TMP_Settings.defaultFontAsset != null)
            label.font = TMP_Settings.defaultFontAsset;

        try
        {
            label.outlineWidth = 0.22f;
            label.outlineColor = new Color32(0, 0, 0, 255);
            label.UpdateMeshPadding();
        }
        catch (System.Exception)
        {
            // Outline unsupported on some TMP material setups — readable fill color is enough.
        }

        ApplyPlayerSorting(label, 2);
    }

    private void ApplyPlayerSorting(Renderer renderer, int orderOffset)
    {
        if (renderer == null)
            return;

        SpriteRenderer sprite = GetComponentInChildren<SpriteRenderer>();
        if (sprite == null)
            return;

        renderer.sortingLayerID = sprite.sortingLayerID;
        renderer.sortingOrder = sprite.sortingOrder + orderOffset;
    }

    private void ApplyPlayerSorting(TextMeshPro label, int orderOffset)
    {
        if (label == null)
            return;

        Renderer textRenderer = label.renderer;
        if (textRenderer != null)
            ApplyPlayerSorting(textRenderer, orderOffset);
    }

    private void RebuildSpineMesh(float weaponRange)
    {
        var verts = new List<Vector3>(384);
        var tris = new List<int>(768);

        AddQuad(verts, tris,
            -PreviewHalfSpan, -SpineHalfThickness,
            PreviewHalfSpan, SpineHalfThickness);

        int weaponTickIndex = weaponRange > 0.01f ? Mathf.RoundToInt(weaponRange) : -1;

        for (int i = Mathf.RoundToInt(-PreviewHalfSpan); i <= Mathf.RoundToInt(PreviewHalfSpan); i++)
        {
            if (weaponTickIndex >= 0 && (i == weaponTickIndex || i == -weaponTickIndex))
                continue;

            AddQuad(verts, tris,
                i - UnitTickHalfThickness, -UnitTickHalfHeight,
                i + UnitTickHalfThickness, UnitTickHalfHeight);
        }

        ApplyMesh(_spineMesh, verts, tris);
    }

    private void RebuildWeaponRangeMesh(float weaponRange)
    {
        var verts = new List<Vector3>(16);
        var tris = new List<int>(32);

        if (weaponRange > 0.01f)
        {
            float clamped = Mathf.Clamp(weaponRange, 0f, PreviewHalfSpan);
            AddWeaponTick(verts, tris, -clamped);
            AddWeaponTick(verts, tris, clamped);
        }

        ApplyMesh(_weaponMesh, verts, tris);
        _weaponMeshRenderer.enabled = verts.Count > 0;
    }

    private static void AddWeaponTick(List<Vector3> verts, List<int> tris, float x)
    {
        AddQuad(verts, tris,
            x - WeaponTickHalfThickness, -WeaponTickHalfHeight,
            x + WeaponTickHalfThickness, WeaponTickHalfHeight);
    }

    private static void AddQuad(
        List<Vector3> verts,
        List<int> tris,
        float xMin,
        float yMin,
        float xMax,
        float yMax)
    {
        int start = verts.Count;
        verts.Add(new Vector3(xMin, yMin, 0f));
        verts.Add(new Vector3(xMin, yMax, 0f));
        verts.Add(new Vector3(xMax, yMax, 0f));
        verts.Add(new Vector3(xMax, yMin, 0f));

        tris.Add(start);
        tris.Add(start + 1);
        tris.Add(start + 2);
        tris.Add(start);
        tris.Add(start + 2);
        tris.Add(start + 3);
    }

    private void ApplyMesh(Mesh mesh, List<Vector3> verts, List<int> tris)
    {
        mesh.Clear();
        if (verts.Count == 0)
            return;

        mesh.SetVertices(verts);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateBounds();
    }

    private static Material GetLineMaterial()
    {
        if (s_lineMaterial != null)
            return s_lineMaterial;

        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null)
            return null;

        s_lineMaterial = new Material(shader) { color = Color.white };
        s_lineMaterial.mainTexture = Texture2D.whiteTexture;
        return s_lineMaterial;
    }
}
