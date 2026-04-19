using UnityEngine;
using UnityEngine.UI;

public class HoldFToOpen : MonoBehaviour
{
    [Header("Player Detection")]
    public string playerTag = "Player";

    [Header("Animator")]
    public Animator targetAnimator;

    [Header("UI Prompt")]
    public GameObject interactUI; // Assign your TextMeshPro UI object here

    [Header("Settings")]
    public KeyCode holdKey = KeyCode.F;

    private bool playerInside = false;

    void Start()
    {
        if (interactUI != null)
            interactUI.SetActive(false);
    }

    void Update()
    {
        if (playerInside)
        {
            // Show prompt
            if (interactUI != null)
                interactUI.SetActive(true);

            // While holding F, keep isClosed false
            if (Input.GetKey(holdKey))
            {
                targetAnimator.SetBool("isClosed", false);
            }
            else
            {
                targetAnimator.SetBool("isClosed", true);
            }
        }
        else
        {
            // Hide prompt when outside
            if (interactUI != null)
                interactUI.SetActive(false);

            targetAnimator.SetBool("isClosed", true);
        }
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag(playerTag))
        {
            playerInside = true;
        }
    }

    void OnTriggerExit(Collider other)
    {
        if (other.CompareTag(playerTag))
        {
            playerInside = false;
        }
    }
}