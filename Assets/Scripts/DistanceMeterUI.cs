using UnityEngine;
using UnityEngine.UI;
using System.Linq; 

public class DistanceMeterUI : MonoBehaviour
{
    public PlayerMovement player;
    public Image fillImage;
    public GameObject distanceBarUI; 

    void Start()
    {
        player = FindObjectsOfType<PlayerMovement>()
            .FirstOrDefault(p => p.IsOwner);

        if (distanceBarUI != null)
            distanceBarUI.SetActive(false);
    }

    void Update()
    {
        if (player == null) return;

        if (player.IsSpawned && distanceBarUI != null && !distanceBarUI.activeSelf)
        {
            distanceBarUI.SetActive(true);
        }
        else if (!player.IsSpawned && distanceBarUI != null && distanceBarUI.activeSelf)
        {
            distanceBarUI.SetActive(false);
        }

        float t = player.DistanceNormalized;

        fillImage.fillAmount = t;

        if (t < 0.5f)
        {
            fillImage.color = Color.Lerp(Color.yellow, new Color(1f, 0.5f, 0f), t * 2f);
        }
        else
        {
            fillImage.color = Color.Lerp(new Color(1f, 0.5f, 0f), Color.red, (t - 0.5f) * 2f);
        }
    }
}