using UnityEngine;

public class CoinDropCounter : MonoBehaviour
{
    [SerializeField, Header("CoinSpawner")]
    CoinSpawner coinSpawner;

    [SerializeField, Header("—Ž‰ºƒRƒCƒ“")]
    int dropCoinCounter = 0;

    private void OnCollisionEnter(Collision collision)
    {
        if(collision.gameObject.tag == "Coin")
        {
            dropCoinCounter++;
            coinSpawner.AddCoin(1);
            Destroy(collision.gameObject);
        }
    }
}
