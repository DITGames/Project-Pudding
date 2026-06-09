using UnityEngine;

public class PusherTableController : MonoBehaviour
{
    [SerializeField]
    float moveValue = 0.0f;

    [SerializeField]
    float intensity = 1.0f;

    [SerializeField]
    float speed = 1.0f;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void FixedUpdate()
    {
        moveValue = Mathf.Sin(Time.time * speed) * intensity;
        transform.position = new Vector3(0.0f,0.0f, moveValue + (intensity));
    }
}
