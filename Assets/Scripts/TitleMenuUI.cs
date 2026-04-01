using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.Netcode;

public class ButtonActions : MonoBehaviour
{
    public void ExitGame()
    {
        Debug.Log("Game is exiting...");
        Application.Quit();
    }

    public void NextScene()
    {
        LoadScene(SceneManager.GetActiveScene().buildIndex + 1);
    }

    public void PreviousScene()
    {
        LoadScene(SceneManager.GetActiveScene().buildIndex - 1);
    }

    void LoadScene(int sceneIndex)
    {
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
        {
            NetworkManager.Singleton.Shutdown();
        }
        StopAllCoroutines();

        SceneManager.LoadScene(sceneIndex, LoadSceneMode.Single);
    }
}