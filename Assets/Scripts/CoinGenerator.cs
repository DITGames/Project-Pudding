using System.Collections;
using UnityEngine;

public class CoinGenerator : MonoBehaviour
{

    [SerializeField]
    GameObject coin;

    [SerializeField, Header("初期生成数")]
    int genarateNum = 100;

    [SerializeField, Header("開始から生成までのオフセット時間")]
    float genarateOffsetTime = 1.0f;
    
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        //オブジェクトのレンダーを無効
        this.GetComponent<MeshRenderer>().enabled = false;

        //初期コイン生成
        StartCoroutine(generateCoin());
    }

    IEnumerator generateCoin()
    {
        yield return new WaitForSeconds(genarateOffsetTime);

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
