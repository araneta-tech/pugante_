using UnityEngine;
using Unity.Netcode;

public class CollectibleItem : NetworkBehaviour
{
    [HideInInspector] public bool isPlayerNearby = false;
}