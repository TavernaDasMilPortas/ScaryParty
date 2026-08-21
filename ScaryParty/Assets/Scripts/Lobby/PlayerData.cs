using UnityEngine;

[CreateAssetMenu(fileName = "PlayerData", menuName = "Scary Party/Player Data")]
public class PlayerData : ScriptableObject
{
    public string PlayerName = "Player";
    public Color PlayerColor = Color.white;

    private void OnEnable()
    {
        // Carrega as preferências salvas
        PlayerName = PlayerPrefs.GetString("PlayerName", "Player" + Random.Range(1000, 9999));
        
        if (PlayerPrefs.HasKey("PlayerColorR"))
        {
            PlayerColor = new Color(
                PlayerPrefs.GetFloat("PlayerColorR"),
                PlayerPrefs.GetFloat("PlayerColorG"),
                PlayerPrefs.GetFloat("PlayerColorB"),
                1f
            );
        }
    }

    private void OnDisable()
    {
        Save();
    }

    public void Save()
    {
        PlayerPrefs.SetString("PlayerName", PlayerName);
        PlayerPrefs.SetFloat("PlayerColorR", PlayerColor.r);
        PlayerPrefs.SetFloat("PlayerColorG", PlayerColor.g);
        PlayerPrefs.SetFloat("PlayerColorB", PlayerColor.b);
        PlayerPrefs.Save();
    }
}
