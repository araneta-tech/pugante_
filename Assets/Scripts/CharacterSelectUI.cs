using UnityEngine;
using UnityEngine.UI;

public class CharacterSelectUI : MonoBehaviour
{
    [SerializeField] private NetworkUI networkUI;

    public void OnSelectCharacter(int index)
    {
        networkUI.OnCharacterSelected(index);
    }
}
