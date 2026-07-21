using CustomConsole;
using UnityEngine;

public class NewMonoBehaviourScript : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        CustomConsoleLog.Log("AI", "Start AI", this);
    }

    // Update is called once per frame
    void Update()
    {
        CustomConsoleLog.Log("AIUpdate", "Update", this);
    }
}
