using System.Collections;
using TMPro;
using UnityEngine;

public class InventoryFullPopup : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private Inventory inventory;
    [SerializeField] private TMP_Text text;

    [Header("Behavior")]
    [SerializeField] private string message = "Inventory Full";
    [SerializeField] private float showSeconds = 1.25f;
    [SerializeField] private float yOffset = 1.2f;

    private Coroutine _routine;

    private Transform _playerRoot;

    private void Awake()
    {
        if (!inventory) inventory = GetComponentInParent<Inventory>();
        if (!text) text = GetComponentInChildren<TMP_Text>(true);

        if (inventory != null)
            _playerRoot = inventory.transform; // Inventory lives on Player root

        if (text)
        {
            text.text = message;
            text.gameObject.SetActive(false);
        }
    }

    private void LateUpdate()
    {
        if (_playerRoot == null) return;

        Vector3 p = _playerRoot.position;
        transform.position = new Vector3(p.x, p.y + yOffset, p.z);
    }

    private void OnEnable()
    {
        if (inventory != null)
            inventory.OnInventoryFull += Show;
    }

    private void OnDisable()
    {
        if (inventory != null)
            inventory.OnInventoryFull -= Show;
    }


    private void Show()
    {
        if (!text) return;

        if (_routine != null) StopCoroutine(_routine);
        _routine = StartCoroutine(ShowRoutine());
    }

    private IEnumerator ShowRoutine()
    {
        text.text = message;
        text.gameObject.SetActive(true);

        yield return new WaitForSeconds(showSeconds);

        text.gameObject.SetActive(false);
        _routine = null;
    }
}