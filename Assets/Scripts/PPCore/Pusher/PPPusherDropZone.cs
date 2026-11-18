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
    // プッシャーから落ちたコインとチャージ用アイテムを検出する領域。トリガーの Collider を付けて使う
    // 落ちたものは通知して消す（コインは置き場へ戻す）。バトル側へはブリッジ（PPCoinResourceBridge / PPChargeItemBridge）経由で伝わる
    [RequireComponent(typeof(Collider))]
    public class PPPusherDropZone : MonoBehaviour, IPPCoinGainNotifier, IPPChargeItemNotifier
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
        // チャージ用アイテムが落ちたとき(アイテムの中身)
        public event Action<PPChargeItemTicket> OnChargeItemDropped;

        /* Unity イベント */

        // 領域へ入ったものを調べ、コインかチャージ用アイテムなら通知して消す
        private void OnTriggerEnter(Collider aOther)
        {
            // 当たり判定は子に付いていることがあるため、Rigidbody を持つ本体で判定する
            var body = aOther.attachedRigidbody;
            if (body == null) return;

            var target = body.gameObject;
            if (target.TryGetComponent<PPPusherChargeItem>(out var item))
            {
                // 同じ物理ステップで複数の Collider が入っても、通知は 1 回にする
                if (item.IsCollected) return;

                item.MarkCollected();
                Destroy(target);
                if (mIsNotify) OnChargeItemDropped?.Invoke(item.Ticket);
                return;
            }

            if (!target.CompareTag(CoinTag)) return;
            // 消える前の同じステップでもう一度入ってきた場合に、二重に数えない
            if (!target.activeSelf) return;

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
