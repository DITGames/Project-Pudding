using System;
using PPCore;
using UnityEngine;

public class CoinDropCounter : MonoBehaviour, IPPCoinGainNotifier
{
    [SerializeField, Header("CoinSpawner")]
    CoinSpawner coinSpawner;

    [SerializeField, Header("�����R�C��")]
    int dropCoinCounter = 0;

    public event Action<PPResourceType, int> OnCoinGained;

    private void OnCollisionEnter(Collision collision)
    {
        if(collision.gameObject.tag == "Coin")
        {
            dropCoinCounter++;
            coinSpawner.AddCoin(1);
            Destroy(collision.gameObject);
            OnCoinGained?.Invoke(PPResourceType.Normal, 1);
        }
    }
}
