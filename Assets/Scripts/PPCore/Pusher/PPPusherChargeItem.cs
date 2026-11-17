/* =====================================
 * Copyright WabisabiAndons. All rights reserved.
 * @file PPPusherChargeItem.cs
 * @author hqrse
 * @date 2026/11/17
 * @brief プッシャーに出現するチャージ用アイテム
 * =====================================*/

using UnityEngine;
using AttributeUtility;

namespace PPCore
{
    // チャージ用アイテム 1 個分の中身。誰のスキルゲージへ入るかは、出現させる時点で決めておく
    // プッシャー側は中身を解釈せず、落ちたときにそのままブリッジへ返す
    public sealed class PPChargeItemTicket
    {
        // スキルゲージが入る先のユニット。パーティ全体用のアイテムなら null
        public PPBattleUnit Target { get; }
        // アイテムの上に出すアイコン。ユニットごとのアイテムではそのユニットのアイコン。無ければ null
        public Sprite Icon { get; }

        // パーティ全体へ入るアイテムか
        public bool IsParty => Target == null;

        public PPChargeItemTicket(PPBattleUnit aTarget, Sprite aIcon)
        {
            Target = aTarget;
            Icon = aIcon;
        }
    }

    // 出現させるチャージ用アイテムの中身を決める口。バトル側（PPChargeItemBridge）が実装する
    public interface IPPChargeItemTicketSource
    {
        // 次に出現させるアイテムの中身を決めて返す。出現させられないときは null を返す
        PPChargeItemTicket IssueTicket();
    }

    // チャージ用アイテムが落ちたことを知らせるためのインターフェース。実装は PPPusherDropZone
    public interface IPPChargeItemNotifier
    {
        // チャージ用アイテムが落ちたとき(アイテムの中身)
        event System.Action<PPChargeItemTicket> OnChargeItemDropped;
    }

    // プッシャーに出現するチャージ用アイテム。落とすとスキルゲージが溜まる
    // コインと同じく物理で動き、落下検出（PPPusherDropZone）に入ると中身が通知される
    public class PPPusherChargeItem : MonoBehaviour
    {
        /* インスペクタ設定 */

        // 中身を表すアイコンの表示先。アイコンが無いアイテムでは隠す
        [Label("アイコンの表示先")]
        [SerializeField] private SpriteRenderer mIconRenderer;
        // アイコンを収める大きさ。スプライトの大きさに関わらず、長い辺がこの長さになるよう拡縮する
        [Label("アイコンの大きさ")]
        [Min(0.1f)]
        [SerializeField] private float mIconSize = 1.8f;
        // 本体の見た目。パーティ全体用のアイテムを色で見分けるために使う
        [Label("本体の表示")]
        [SerializeField] private Renderer mBodyRenderer;
        [Label("ユニットごとの色")]
        [SerializeField] private Color mUnitColor = new Color(0.3f, 0.7f, 1f);
        [Label("パーティ全体用の色")]
        [SerializeField] private Color mPartyColor = new Color(1f, 0.8f, 0.2f);

        /* 状態 */

        // このアイテムの中身
        public PPChargeItemTicket Ticket { get; private set; }
        // 落下検出に拾われたか。二重に通知しないために持つ
        public bool IsCollected { get; private set; }

        /* 初期化・接続 */

        // 中身を設定し、見た目へ反映する。出現させた直後に呼ぶ
        public void Setup(PPChargeItemTicket aTicket)
        {
            Ticket = aTicket;

            if (mIconRenderer != null)
            {
                mIconRenderer.sprite = aTicket?.Icon;
                mIconRenderer.enabled = aTicket?.Icon != null;
                if (aTicket?.Icon != null)
                {
                    var size = aTicket.Icon.bounds.size;
                    float longSide = Mathf.Max(size.x, size.y);
                    if (longSide > 0f) mIconRenderer.transform.localScale = Vector3.one * (mIconSize / longSide);
                }
            }

            if (mBodyRenderer != null)
            {
                // 共有マテリアルを書き換えないよう、色はこのアイテムだけに掛ける
                var block = new MaterialPropertyBlock();
                mBodyRenderer.GetPropertyBlock(block);
                block.SetColor("_BaseColor", aTicket != null && aTicket.IsParty ? mPartyColor : mUnitColor);
                mBodyRenderer.SetPropertyBlock(block);
            }
        }

        /* 操作 */

        // 落下検出に拾われたことを記録する
        public void MarkCollected() => IsCollected = true;
    }
}
