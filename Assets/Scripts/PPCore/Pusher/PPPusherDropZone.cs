/* =====================================
 * Copyright WabisabiAndons. All rights reserved.
 * @file PPPusherDropZone.cs
 * @author hqrse
 * @date 2026/11/17
 * @brief プッシャーの落下検出
 * =====================================*/

using System;
using UnityEngine;
using AttributeUtility;

namespace PPCore
{
    // プッシャー（物理）側がコインの落下を知らせるためのインターフェース。実装は PPPusherDropZone と CoinDropCounter
    public interface IPPCoinGainNotifier
    {
        // コインが落ちたとき(コインの属性, 枚数)
        event Action<PPTypeAttribute, int> OnCoinGained;
    }

    // プッシャーから落ちたコインを検出する領域。トリガーの Collider を付けて使う
    // 落ちたコインは通知して置き場へ戻す
    [RequireComponent(typeof(Collider))]
    public class PPPusherDropZone : MonoBehaviour, IPPCoinGainNotifier
    {
        /* 定数 */

        // コインとして数えるオブジェクトのタグ
        private const string CoinTag = "Coin";

        /* インスペクタ設定 */

        // 落ちたコインの属性。属性つきのコインを扱うまでは、どのコインも同じ属性として通知する
        [Label("コインの属性")]
        [SerializeField] private PPTypeAttribute mCoinAttribute = PPTypeAttribute.Normal;
        // 落ちたものを通知するか。無効にすると、台の外へ飛び出したものを数えずに消すだけの領域になる
        [Label("落ちたものを通知する")]
        [SerializeField] private bool mIsNotify = true;

        /* 状態 */

        // これまでに落ちたコインの枚数
        public int DroppedCoinCount { get; private set; }

        /* イベント */

        // コインが落ちたとき(コインの属性, 枚数)
        public event Action<PPTypeAttribute, int> OnCoinGained;
        // コインが落ちたとき(落ちたコイン)。置き場へ戻す前に呼ぶので、コインの中身を読める
        public event Action<GameObject> OnCoinDropped;

        /* Unity イベント */

        // 領域へ入ったものを調べ、コインなら通知して消す
        private void OnTriggerEnter(Collider aOther)
        {
            // 当たり判定は子に付いていることがあるため、Rigidbody を持つ本体で判定する
            var body = aOther.attachedRigidbody;
            if (body == null) return;

            var target = body.gameObject;
            if (!target.CompareTag(CoinTag)) return;
            // 消える前の同じステップでもう一度入ってきた場合に、二重に数えない
            if (!target.activeSelf) return;

            if (mIsNotify) OnCoinDropped?.Invoke(target);

            // 置き場から出したコインは置き場へ戻して使い回す
            var pool = target.GetComponentInParent<PPCoinPool>();
            if (pool != null) pool.Release(target);
            else
            {
                target.SetActive(false);
                Destroy(target);
            }
            DroppedCoinCount++;
            if (!mIsNotify) return;

            OnCoinGained?.Invoke(mCoinAttribute, 1);
        }
    }
}
