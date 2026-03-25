using System.Collections.Generic;
using UnityEngine;

public class UIWindowManager : MonoBehaviour
{
    public static UIWindowManager Instance;

    private readonly List<GameObject> _openWindows = new();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            CloseAllWindows();
        }
    }

    public void Register(GameObject window)
    {
        if (!_openWindows.Contains(window))
            _openWindows.Add(window);
    }

    public void Unregister(GameObject window)
    {
        _openWindows.Remove(window);
    }

    public void CloseAllWindows()
    {
        for (int i = _openWindows.Count - 1; i >= 0; i--)
        {
            if (_openWindows[i])
                _openWindows[i].SetActive(false);
        }

        _openWindows.Clear();
    }
}