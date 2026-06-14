using UnityEngine;

public class PusherTableController : MonoBehaviour
{
    [SerializeField]
    float moveValue = 0.0f;

    [SerializeField]
    float intensity = 1.0f;

    [SerializeField]
    float speed = 1.0f;

    Rigidbody rb;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        rb = this.GetComponent<Rigidbody>();
    }

    // Update is called once per frame
    void FixedUpdate()
    {
        float moveValue = Mathf.Sin(Time.time * speed) * intensity;

        Vector3 targetPos = new Vector3(
            0.0f,
            0.0f,
            moveValue + intensity
        );

        rb.MovePosition(targetPos);
    }
}
