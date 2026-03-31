using UnityEngine;

public class PersistAcrossScenes : MonoBehaviour
{
    private static PersistAcrossScenes _instance;

    private void Awake()
    {
        if (_instance != null)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;
        DontDestroyOnLoad(gameObject);
    }
}