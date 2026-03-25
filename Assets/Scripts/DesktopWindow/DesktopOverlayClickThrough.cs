using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using Kirurobo;

public class DesktopOverlayClickThrough : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private UniWindowController uniWin;
    [SerializeField] private Camera stripCamera;
    [SerializeField] private string stripCameraName = "StripCamera";

    // Latches interaction while dragging UI so it doesn't "drop" mid-drag.
    private bool _dragLatch;

    private void Awake()
    {
        if (!uniWin) uniWin = FindFirstObjectByType<UniWindowController>(FindObjectsInactive.Include);
        RebindStripCamera();
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        RebindStripCamera();
    }

    private void RebindStripCamera()
    {
        if (stripCamera) return;

        var go = GameObject.Find(stripCameraName);
        if (go) stripCamera = go.GetComponent<Camera>();
    }

    private void LateUpdate()
    {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        if (!uniWin) return;

        // If strip cam wasn't ready at Awake (bootstrap timing), keep trying.
        if (!stripCamera) RebindStripCamera();

        bool inStrip = stripCamera && stripCamera.pixelRect.Contains(Input.mousePosition);
        bool overUI = IsPointerOverUIRaycast();

        // Latch when click begins on UI so dragging stays interactive even if raycasts flicker.
        if (Input.GetMouseButtonDown(0))
            _dragLatch = overUI;

        if (Input.GetMouseButtonUp(0))
            _dragLatch = false;

        bool interactive = inStrip || overUI || _dragLatch;

        // interactive => Unity receives mouse (NOT click-through)
        // not interactive => Desktop receives mouse (click-through)
        uniWin.SetClickThrough(!interactive);
#endif
    }

    private static bool IsPointerOverUIRaycast()
    {
        if (EventSystem.current == null) return false;

        var pointerData = new PointerEventData(EventSystem.current)
        {
            position = Input.mousePosition
        };

        var results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(pointerData, results);
        return results.Count > 0;
    }
}