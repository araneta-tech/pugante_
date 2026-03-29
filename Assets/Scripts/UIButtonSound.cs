using UnityEngine;
using UnityEngine.UI;

public class UIButtonSound : MonoBehaviour
{
    public bool isCloseButton = false;

    void Start()
    {
        Button btn = GetComponent<Button>();

        if (btn != null)
        {
            btn.onClick.AddListener(() =>
            {
                if (UISoundManager.Instance == null) return;

                if (isCloseButton)
                    UISoundManager.Instance.PlayClose();
                else
                    UISoundManager.Instance.PlayClick();
            });
        }
    }
}