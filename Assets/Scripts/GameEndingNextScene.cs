using UnityEngine;
using Unity.Netcode;
using UnityEngine.SceneManagement;
using System.Collections;

public class NextSceneOnPlayerEnter : NetworkBehaviour
{
    [Header("Player Detection")]
    [SerializeField] private string playerTag = "Player";

    [Header("Settings")]
    [SerializeField] private float delayBeforeLoad = 0f;

    private bool sceneLoadTriggered = false;

    private void OnTriggerEnter(Collider other)
    {
        if (!IsPlayerCollider(other))
            return;

        RequestNextSceneServerRpc();
    }

    private bool IsPlayerCollider(Collider other)
    {
        if (other == null)
            return false;

        NetworkObject netObj = other.GetComponentInParent<NetworkObject>();

        if (netObj != null && netObj.CompareTag(playerTag))
            return true;

        return other.CompareTag(playerTag);
    }

    [ServerRpc(RequireOwnership = false)]
    private void RequestNextSceneServerRpc(ServerRpcParams rpcParams = default)
    {
        if (sceneLoadTriggered)
            return;

        sceneLoadTriggered = true;
        StartCoroutine(LoadNextSceneForEveryone());
    }

    private IEnumerator LoadNextSceneForEveryone()
    {
        if (delayBeforeLoad > 0f)
            yield return new WaitForSeconds(delayBeforeLoad);

        if (NetworkManager.Singleton == null)
            yield break;

        int currentSceneIndex = SceneManager.GetActiveScene().buildIndex;
        int nextSceneIndex = currentSceneIndex + 1;

        if (nextSceneIndex >= SceneManager.sceneCountInBuildSettings)
        {
            Debug.Log("No more scenes in Build Settings.");
            sceneLoadTriggered = false;
            yield break;
        }

        string nextScenePath = SceneUtility.GetScenePathByBuildIndex(nextSceneIndex);
        string nextSceneName = System.IO.Path.GetFileNameWithoutExtension(nextScenePath);

        if (NetworkManager.Singleton.IsServer)
        {
            NetworkManager.Singleton.SceneManager.LoadScene(nextSceneName, LoadSceneMode.Single);
        }
        else
        {
            sceneLoadTriggered = false;
        }
    }
}