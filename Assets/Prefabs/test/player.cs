using UnityEngine;
using Unity.Netcode;

public class SimpleNetworkObject : NetworkBehaviour
{
    [Header("Movement")]
    public float moveSpeed = 5f;

    private Rigidbody rb;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        // Only the owner can control this object
        if (!IsOwner)
        {
            if (rb != null)
                rb.isKinematic = true;
        }
        else
        {
            if (rb != null)
                rb.isKinematic = false;
        }
    }

    void Update()
    {
        // Only allow the owner to move
        if (!IsOwner) return;

        HandleMovement();
    }

    void HandleMovement()
    {
        float h = Input.GetAxis("Horizontal");
        float v = Input.GetAxis("Vertical");

        Vector3 move = new Vector3(h, 0, v);

        if (rb != null)
        {
            rb.MovePosition(transform.position + move * moveSpeed * Time.deltaTime);
        }
    }
}