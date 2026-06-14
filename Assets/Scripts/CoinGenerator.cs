using System.Collections;
using UnityEngine;

public class CoinGenerator : MonoBehaviour
{

    [SerializeField]
    GameObject coin;

    [SerializeField]
    int genarateNum = 100;
    
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        StartCoroutine(generateCoin());
    }

    IEnumerator generateCoin()
    {
        yield return new WaitForSeconds(3);

        for (int i = 0; i < genarateNum; i++)
        {
            Vector3 pos = new Vector3(
                Random.Range(-transform.localScale.x * 0.5f, transform.localScale.x * 0.5f),
                Random.Range(-transform.localScale.y * 0.5f, transform.localScale.y * 0.5f),
                Random.Range(-transform.localScale.z * 0.5f, transform.localScale.z * 0.5f)
                );

            pos = pos + transform.position;

            Quaternion rot = Quaternion.Euler(
                Random.Range(0f, 360f),
                Random.Range(0f, 360f),
                Random.Range(0f, 360f)
            );

            Instantiate(coin, pos, rot);
        }
    }
}
