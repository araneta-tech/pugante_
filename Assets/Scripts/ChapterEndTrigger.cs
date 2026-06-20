using UnityEngine;
using Unity.Netcode;
using UnityEngine.SceneManagement;

public class ChapterEndTrigger : NetworkBehaviour
{
    [Header("Next Scene")]
    public string nextSceneName;

    [Header("Require All Players?")]
    public bool requireAllPlayers = true;

    private int playersInTrigger = 0;

    private void OnTriggerEnter(Collider other)
    {
        if (!IsServer) return;

        PlayerMovement player = other.GetComponent<PlayerMovement>();
        if (player == null) return;

        playersInTrigger++;

        if (!requireAllPlayers || playersInTrigger >= GameManager.Instance.PlayersCount)
        {
            GameManager.Instance.EndChapter();

            if (!string.IsNullOrEmpty(nextSceneName))
                NetworkManager.Singleton.SceneManager.LoadScene(nextSceneName, LoadSceneMode.Single);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (!IsServer) return;

        PlayerMovement player = other.GetComponent<PlayerMovement>();
        if (player == null) return;

        playersInTrigger--;
    }
}