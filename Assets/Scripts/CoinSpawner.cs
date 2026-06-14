using System.Linq.Expressions;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

public class CoinSpawner : MonoBehaviour
{
    [SerializeField, Header("所持コイン")]
    int currentCoinCount = 0;

    [SerializeField, Header("コインPrefab")]
    GameObject coin;

    [SerializeField, Header("所持コインカウンターテキスト（UI）")]
    TextMeshProUGUI currentCoinCounterText;

    [SerializeField, Header("投入の最大速度")]
    float throwMaxSpeed = 10.0f;

    [SerializeField, Header("投入位置（右）")]
    Transform spawnPositionRight;

    [SerializeField, Header("投入位置（左）")]
    Transform spawnPositionLeft;

    private void Update()
    {
        if (currentCoinCount > 0)
        {
            //右挿入
            if (Keyboard.current.rightArrowKey.wasPressedThisFrame)
            {
                spawnCoin(
                    spawnPositionRight.position,
                    new Vector3(-Random.Range(1.0f, throwMaxSpeed), 0.0f, 0.0f)
                    );
            }

            //左挿入
            if (Keyboard.current.leftArrowKey.wasPressedThisFrame)
            {
                spawnCoin(
                    spawnPositionLeft.position,
                    new Vector3(Random.Range(1.0f, throwMaxSpeed), 0.0f, 0.0f)
                    );
            }
        }
    }

    void spawnCoin(Vector3 pos, Vector3 power)
    {
        Quaternion rot = Quaternion.Euler(
                Random.Range(0f, 360f),
                Random.Range(0f, 360f),
                Random.Range(0f, 360f)
                );

        GameObject spawnCoin = Instantiate(coin, pos, rot);

        Rigidbody spawnCoinRB = spawnCoin.GetComponent<Rigidbody>();
        spawnCoinRB.linearVelocity = power;

        AddCoin(-1);
    }

    public void AddCoin(int num)
    {
        currentCoinCount += num;
        currentCoinCounterText.text = currentCoinCount.ToString();
    }
}
