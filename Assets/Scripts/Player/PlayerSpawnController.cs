using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
public class PlayerSpawnController : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private Rigidbody2D rb;
    [SerializeField] private Collider2D col;
    [SerializeField] private PlayerController player;

    [Header("Spawn Point")]
    [Tooltip("Name of the spawn point GameObject in each scene.")]
    [SerializeField] private string spawnPointName = "SpawnPoint_Player";

    [Header("Optional Walk-In (New Game)")]
    [Tooltip("If true, and both points exist, spawns at Entry then auto-walks to Arrival (every scene load).")]
    [SerializeField] private bool walkInOnSceneLoad = true;

    [Tooltip("Spawn point used for walk-in entry (off-screen).")]
    [SerializeField] private string walkInEntryPointName = "SpawnPoint_Player_Entry";

    [Tooltip("Arrival point the player walks to (on-screen).")]
    [SerializeField] private string walkInArrivalPointName = "SpawnPoint_Player";

    [Tooltip("Delay after fade-in before issuing walk command.")]
    [SerializeField] private float walkInCommandDelaySeconds = 0.05f;

    [Tooltip("Extra frames to wait after scene load before moving player (helps setup order).")]
    [SerializeField] private int waitFramesAfterLoad = 1;

    [Header("Optional Ground Snap")]
    [SerializeField] private bool snapToGround = false;
    [SerializeField] private LayerMask groundMask;
    [SerializeField] private float castDistance = 5f;

    [Tooltip("Tiny separation so we don't end up penetrating the ground collider.")]
    [SerializeField] private float groundSkin = 0.001f;

    [Header("Fade")]
    [SerializeField] private float fadeDuration = 0.2f;

    [Header("Debug")]
    [SerializeField] private bool debugSnap = false;

    private SpriteRenderer[] renderers;
    private Coroutine _running;

    private readonly RaycastHit2D[] _castHits = new RaycastHit2D[16];

    private void Awake()
    {
        if (!rb) rb = GetComponent<Rigidbody2D>();
        if (!col) col = GetComponent<Collider2D>();
        if (!player) player = GetComponent<PlayerController>();
        renderers = GetComponentsInChildren<SpriteRenderer>(true);
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
        if (_running != null) StopCoroutine(_running);
        _running = StartCoroutine(SpawnAfterLoad(scene));
    }

    private static bool IsBootstrapScene(Scene scene) =>
        scene.IsValid() && scene.name.Equals("Bootstrap", StringComparison.OrdinalIgnoreCase);

    private IEnumerator SpawnAfterLoad(Scene loadedScene)
    {
        bool doFade = fadeDuration > 0.001f;

        // Hide instantly BEFORE waiting (only if we're actually going to fade)
        if (doFade)
            SetAlpha(0f);
        else
            SetAlpha(1f);

        // Freeze physics so teleport + snap is clean
        if (rb) rb.simulated = false;

        // IMPORTANT: keep collider enabled so Cast snapping works
        if (col) col.enabled = true;

        // Wait for scene objects (spawn point/colliders) to exist
        for (int i = 0; i < waitFramesAfterLoad; i++)
            yield return null;

        // Choose spawn location (walk-in entry if configured + present)
        bool shouldWalkIn = walkInOnSceneLoad;

        GameObject spawn = null;
        if (shouldWalkIn)
            spawn = GameObject.Find(walkInEntryPointName);

        // Fallback to the normal spawn point if no entry point exists
        if (spawn == null)
            spawn = GameObject.Find(spawnPointName);

        if (spawn != null)
        {
            transform.position = spawn.transform.position;
        }
        else if (!IsBootstrapScene(loadedScene))
        {
            Debug.LogWarning($"[PlayerSpawnController] Missing spawn point in scene. Tried '{(shouldWalkIn ? walkInEntryPointName : spawnPointName)}' then '{spawnPointName}'. Player stays where it is.");
        }

        // Let transforms + physics settle
        yield return null;
        yield return new WaitForEndOfFrame();
        yield return new WaitForFixedUpdate();

        if (snapToGround)
            SnapToGround_ColliderCast(!IsBootstrapScene(loadedScene));

        // Clear motion + re-enable physics
        if (rb)
        {
            rb.linearVelocity = Vector2.zero;
            rb.simulated = true;
        }

        // Fade in (optional)
        if (doFade)
            yield return FadeIn();
        else
            SetAlpha(1f);

        // After fade-in, issue walk-to command for the walk-in arrival point
        if (shouldWalkIn && player != null)
        {
            var arrival = GameObject.Find(walkInArrivalPointName);
            if (arrival != null)
            {
                if (walkInCommandDelaySeconds > 0f)
                    yield return new WaitForSeconds(walkInCommandDelaySeconds);

                player.MoveToPointX(arrival.transform.position.x);
            }
        }

        _running = null;
    }

    /// <summary>
    /// Reliable snap: casts the player's collider downward against groundMask and moves
    /// the player so the collider bottom rests on the ground.
    /// Works with pivot offsets and thin/edge colliders better than a single Raycast.
    /// </summary>
    private void SnapToGround_ColliderCast(bool logFailureWarning)
    {
        if (!col)
        {
            if (debugSnap) Debug.LogWarning("[Snap] No collider assigned.");
            return;
        }

        // Ensure collider is enabled for Cast
        bool wasEnabled = col.enabled;
        if (!wasEnabled) col.enabled = true;

        // Try a few times in case composite/tile colliders build a frame late
        for (int attempt = 0; attempt < 3; attempt++)
        {
            var filter = new ContactFilter2D
            {
                useLayerMask = true,
                layerMask = groundMask,
                useTriggers = false
            };

            int hitCount = col.Cast(Vector2.down, filter, _castHits, Mathf.Max(0.25f, castDistance));

            if (hitCount > 0)
            {
                // pick closest hit
                RaycastHit2D best = _castHits[0];
                for (int i = 1; i < hitCount; i++)
                    if (_castHits[i].distance < best.distance)
                        best = _castHits[i];

                float moveDown = Mathf.Max(0f, best.distance - groundSkin);

                if (debugSnap)
                {
                    Debug.Log($"[Snap] Hit '{best.collider.name}' layer={LayerMask.LayerToName(best.collider.gameObject.layer)} " +
                              $"dist={best.distance:F4} moveDown={moveDown:F4} pointY={best.point.y:F3}");
                }

                // Move down by the cast distance so collider rests on ground
                transform.position += new Vector3(0f, -moveDown, 0f);

                // restore enabled state if needed
                if (!wasEnabled) col.enabled = false;
                return;
            }

            if (debugSnap)
            {
                Debug.Log($"[Snap] No hit (attempt {attempt + 1}). groundMask={groundMask.value} " +
                          $"colBottomY={col.bounds.min.y:F3} posY={transform.position.y:F3}");
            }
        }

        if (!wasEnabled) col.enabled = false;

        if (logFailureWarning)
            Debug.LogWarning("[PlayerSpawnController] SnapToGround failed (no ground hit). Check groundMask + colliders.");
    }

    private IEnumerator FadeIn()
    {
        float t = 0f;
        while (t < fadeDuration)
        {
            t += Time.deltaTime;
            float a = Mathf.Clamp01(t / fadeDuration);
            SetAlpha(a);
            yield return null;
        }
        SetAlpha(1f);
    }

    private void SetAlpha(float alpha)
    {
        if (renderers == null) return;

        foreach (var r in renderers)
        {
            if (!r) continue;
            Color c = r.color;
            c.a = alpha;
            r.color = c;
        }
    }
}