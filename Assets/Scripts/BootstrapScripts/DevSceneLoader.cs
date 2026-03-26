using UnityEngine;
using UnityEngine.SceneManagement;

public class DevSceneLoader : MonoBehaviour
{
    [SerializeField] private string sceneOne = "Resource_Map_01";
    [SerializeField] private string sceneTwo = "Combat_Map_01";
    [SerializeField] private string sceneThree = "Resource_Map_02";

    private void Update()
    {
        // Press F1 to load the test scene during Play Mode
        if (Input.GetKeyDown(KeyCode.F1))
        {
            SceneManager.LoadScene(sceneOne);
        }
        // Press F2 to load the test scene during Play Mode
        if (Input.GetKeyDown(KeyCode.F2))
        {

            SceneManager.LoadScene(sceneTwo);
        }
        // Press F3 to load the test scene during Play Mode
        if (Input.GetKeyDown(KeyCode.F3))
        {

            SceneManager.LoadScene(sceneThree);
        }
    }
}