using UnityEngine;

public class UIWindow : MonoBehaviour
{
    private void OnEnable()
    {
        UIWindowManager.Instance?.Register(gameObject);
    }

    private void OnDisable()
    {
        UIWindowManager.Instance?.Unregister(gameObject);
    }
}