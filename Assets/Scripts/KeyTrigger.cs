using UnityEngine;
using Unity.Netcode;

public class KeyTrigger : MonoBehaviour
{
    public Key keyParent; 

    private void OnTriggerEnter(Collider other)
    {
        PlayerMovement player = other.GetComponent<PlayerMovement>();
        if (player != null && player.IsOwner)
        {
            keyParent.PlayerNearby(true);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        PlayerMovement player = other.GetComponent<PlayerMovement>();
        if (player != null && player.IsOwner)
        {
            keyParent.PlayerNearby(false);
        }
    }
}