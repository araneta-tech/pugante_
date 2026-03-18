using UnityEngine;
using Unity.Netcode;
using UnityEngine.SceneManagement;
using System.Collections;

public class GameManager : NetworkBehaviour
{
    public static GameManager Instance;

    [Header("UI")]
    public GameObject endScreen;

    private void Awake()
    {
        Instance = this;
    }

    [ServerRpc(RequireOwnership = false)]
    public void ShowEndScreenServerRpc()
    {
        ShowEndScreenClientRpc();
        StartCoroutine(RestartRoutine());
    }

    [ClientRpc]
    void ShowEndScreenClientRpc()
    {
        if (endScreen != null)
            endScreen.SetActive(true);
    }

    private IEnumerator RestartRoutine()
    {
        yield return new WaitForSeconds(2f);
        NetworkManager.Singleton.SceneManager.LoadScene("YourSceneName", LoadSceneMode.Single);
    }
}