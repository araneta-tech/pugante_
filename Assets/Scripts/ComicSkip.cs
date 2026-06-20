using UnityEngine;
using UnityEngine.SceneManagement;

public class NextSceneOnEscape : MonoBehaviour
{
    [Header("Allowed Scene")]
    [SerializeField] private string targetSceneName; 

    private float timer = 0f;
    private const float autoLoadTime = 65f;

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
            LoadNextScene();
        }
    }

    private void HandleAutoSceneLoad()
    {
        timer += Time.deltaTime;

        if (timer >= autoLoadTime)
        {
            LoadNextScene();
        }
    }

    private bool IsCorrectScene()
    {
        return SceneManager.GetActiveScene().name == targetSceneName;
    }

    private void LoadNextScene()
    {
        int currentSceneIndex = SceneManager.GetActiveScene().buildIndex;
        int nextSceneIndex = currentSceneIndex + 1;

        if (nextSceneIndex < SceneManager.sceneCountInBuildSettings)
        {
            SceneManager.LoadScene(nextSceneIndex);
        }
        else
        {
            Debug.Log("No more scenes in Build Settings.");
        }
    }
}