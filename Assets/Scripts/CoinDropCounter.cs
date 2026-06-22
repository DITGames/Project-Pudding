using System;
using UnityEngine;

public class CoinDropCounter : MonoBehaviour
{
    [SerializeField, Header("CoinSpawner")]
    CoinSpawner coinSpawner;

    [SerializeField, Header("�����R�C��")]
    int dropCoinCounter = 0;

    public event Action<int> OnCoinDropped;

    private void OnCollisionEnter(Collision collision)
    {
        if(collision.gameObject.tag == "Coin")
        {
            dropCoinCounter++;
            coinSpawner.AddCoin(1);
            Destroy(collision.gameObject);
            OnCoinDropped?.Invoke(1);
        }
    }
}
