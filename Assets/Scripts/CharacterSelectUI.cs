using UnityEngine;
using UnityEngine.UI;

public class CharacterSelectUI : MonoBehaviour
{
    [SerializeField] private NetworkUI networkUI;

    // Call this from each button, passing the character index
    public void OnSelectCharacter(int index)
    {
        networkUI.OnCharacterSelected(index);
    }
}
