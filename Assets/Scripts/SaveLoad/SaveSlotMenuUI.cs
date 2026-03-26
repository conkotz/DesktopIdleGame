using UnityEngine;
using UnityEngine.SceneManagement;

public class SaveSlotMenuUI : MonoBehaviour
{
    [SerializeField] private string gameplaySceneName = "GameScene";

    public void OnClickLoadSlot(int slotIndex)
    {
        Debug.Log($"Load slot {slotIndex}");

        SaveSlotManager.SetActiveSlot(slotIndex);
        SceneManager.LoadScene(gameplaySceneName);
    }

    public void OnClickNewGame(int slotIndex)
    {
        Debug.Log($"New game slot {slotIndex}");

        SaveSlotManager.DeleteSlot(slotIndex);
        SaveSlotManager.SetActiveSlot(slotIndex);

        SceneManager.LoadScene(gameplaySceneName);
    }

    public void OnClickDeleteSlot(int slotIndex)
    {
        Debug.Log($"Delete slot {slotIndex}");

        SaveSlotManager.DeleteSlot(slotIndex);

        // later: refresh UI
    }
}