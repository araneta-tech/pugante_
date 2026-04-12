using UnityEngine;
using Unity.Netcode;
using System.Collections;

public class QTESmash : NetworkBehaviour
{
    [Header("QTE Settings")]
    public float riseHeight = 2f;
    public float riseDuration = 1f;
    public KeyCode qteKey = KeyCode.Space;  
    public float maxPressTime = 2f; 

    [Header("Floating UI")]
    public GameObject floatingUIText; 

    [HideInInspector]
    public bool isPlayerNearby = false;

    private float pressTime = 0f; 

    void Start()
    {
        if (floatingUIText != null)
            floatingUIText.SetActive(false);
    }

    void Update()
    {
        if (!IsSpawned) return;

        if (isPlayerNearby)
        {
            if (Input.GetKey(qteKey))
            {
                pressTime += Time.deltaTime; 

                if (pressTime >= maxPressTime)
                {
                    CollectKeyServerRpc();
                }
            }
            else
            {
                pressTime = 0f;  
            }
        }
    }

    public void PlayerNearby(bool nearby)
    {
        isPlayerNearby = nearby;
        if (floatingUIText != null)
            floatingUIText.SetActive(nearby);
    }

    private void OnTriggerEnter(Collider other)
    {
        PlayerMovement player = other.GetComponent<PlayerMovement>();
        if (player != null && player.IsOwner)
        {
            isPlayerNearby = true;
            if (floatingUIText != null)
                floatingUIText.SetActive(true);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        PlayerMovement player = other.GetComponent<PlayerMovement>();
        if (player != null && player.IsOwner)
        {
            isPlayerNearby = false;
            if (floatingUIText != null)
                floatingUIText.SetActive(false);
        }
    }

    [ServerRpc(RequireOwnership = false)]
    void CollectKeyServerRpc()
    {
        if (!IsSpawned) return;

        if (floatingUIText != null)
            floatingUIText.SetActive(false);  

        StartCoroutine(RiseAndDestroy());
    }

    private IEnumerator RiseAndDestroy()
    {
        Vector3 startPos = transform.position;
        Vector3 targetPos = startPos + Vector3.up * riseHeight;
        float elapsed = 0f;

        while (elapsed < riseDuration)
        {
            transform.position = Vector3.Lerp(startPos, targetPos, elapsed / riseDuration);
            elapsed += Time.deltaTime;
            yield return null;
        }

        transform.position = targetPos;  

        if (NetworkObject != null && NetworkObject.IsSpawned)
            NetworkObject.Despawn();

        Destroy(gameObject);
    }
}