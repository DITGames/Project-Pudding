using TMPro;
using UnityEngine;

public class ConsoleCanvasManager : MonoBehaviour
{
    [SerializeField, Header("ApplicationVersion(Text)")]
    TextMeshProUGUI applicationVersionText;

    private void Start()
    {
        applicationVersionText.text = "ApplicationVersion : " + Application.version;
    }
}
