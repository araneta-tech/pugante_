using UnityEngine;
using UnityEngine.SceneManagement;

public class RestartToFirstSceneOnEscape : MonoBehaviour
{
    [Header("Allowed Scene")]
    [SerializeField] private string targetSceneName; 

    private float timer = 0f;
    private const float autoLoadTime = 40f; 

    private void Update()
    {
        if (!IsCorrectScene())
            return;

        HandleEscapeInput();
        HandleAutoSceneLoad();
    }

    private void HandleEscapeInput()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            RestartGame();
        }
    }

    private void HandleAutoSceneLoad()
    {
        timer += Time.deltaTime;

        if (timer >= autoLoadTime)
        {
            RestartGame();
        }
    }

    private bool IsCorrectScene()
    {
        return SceneManager.GetActiveScene().name == targetSceneName;
    }

    private void RestartGame()
    {
        if (SceneManager.sceneCountInBuildSettings > 0)
        {
            SceneManager.LoadScene(0);
        }
        else
        {
            Debug.Log("No scenes found in Build Settings.");
        }
    }
}