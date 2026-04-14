using UnityEngine;
using Unity.Netcode;

public class FuseboxTrigger : MonoBehaviour
{
    public Fusebox doorParent;

    private void OnTriggerEnter(Collider other)
    {
        PlayerMovement player = other.GetComponent<PlayerMovement>();
        if (player != null && player.IsOwner)
        {
            doorParent.PlayerNearby(true);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        PlayerMovement player = other.GetComponent<PlayerMovement>();
        if (player != null && player.IsOwner)
        {
            doorParent.PlayerNearby(false);
        }
    }
}