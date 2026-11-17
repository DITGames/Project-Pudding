/* =====================================
 * Copyright WabisabiAndons. All rights reserved.
 * @file PPChargeItemBridge.cs
 * @author hqrse
 * @date 2026/11/17
 * @brief プッシャー → バトルのチャージ用アイテムの橋渡し
 * =====================================*/

using System.Collections.Generic;
using UnityEngine;
using AttributeUtility;
using CustomConsole;

namespace PPCore
{
    // プッシャー（物理）側のチャージ用アイテムとバトル側を繋ぐブリッジ
    // 出現させるアイテムの中身（誰のスキルゲージへ入るか）を決め、落ちたときにスキルゲージを溜める
    public class PPChargeItemBridge : MonoBehaviour, IPPChargeItemTicketSource
    {
        /* インスペクタ設定 */

        // チャージ用アイテムの落下通知元。IPPChargeItemNotifier 実装コンポーネント（PPPusherDropZone）を差す
        [Label("落下通知コンポーネント")]
        [SerializeField] private MonoBehaviour mItemNotifierSource;
        [Label("アイテムの出現役")]
        [SerializeField] private PPChargeItemSpawner mSpawner;
        // ユニットごとのアイテムへ出すアイコンを引くカタログ
        [Label("ユニットビジュアルカタログ")]
        [SerializeField] private PPUnitVisualCatalog mUnitVisualCatalog;

        // 出現するアイテムがユニットごとのものになる確率。残りはパーティ全体用になる
        [Label("ユニットごとのアイテムの割合")]
        [Range(0f, 1f)]
        [SerializeField] private float mUnitItemRate = 0.7f;
        // ユニットごとのアイテムで、そのユニットに溜まるスキルゲージ量
        [Label("ユニットごとのチャージ量")]
        [Min(0f)]
        [SerializeField] private float mUnitChargeAmount = 30f;
        // パーティ全体用のアイテムで、生存ユニット 1 体ごとに溜まるスキルゲージ量
        [Label("全体用のチャージ量(1体あたり)")]
        [Min(0f)]
        [SerializeField] private float mPartyChargeAmount = 15f;

        /* 状態 */

        // インスペクタで差された通知元をインターフェースとして解決したもの
        private IPPChargeItemNotifier mItemNotifier;
        // 加算先のパーティ。Bind されるまでは null
        private PPBattleParty mTargetParty;
        // 乱数とルールを引くためのバトルコンテキスト
        private BattleContext mContext;
        // 入る先の候補を集める作業用リスト
        private readonly List<PPBattleUnit> mCandidates = new();

        /* Unity イベント */

        // インスペクタで指定された通知元をインターフェースへ解決する
        private void Awake()
        {
            mItemNotifier = mItemNotifierSource as IPPChargeItemNotifier;
            if (mItemNotifierSource != null && mItemNotifier == null)
            {
                CustomConsoleLog.Warning("Resource",
                    $"{nameof(mItemNotifierSource)}はIPPChargeItemNotifierを実装している必要があります", this);
            }
        }

        // 破棄時にイベント購読が残らないよう解除する
        private void OnDestroy() => Unbind();

        /* 初期化・接続 */

        // 指定陣営のパーティを加算先として設定し、アイテムの出現と落下通知の購読を開始する
        public void Bind(BattleManager aBattleManager, BattleSide aTargetSide)
        {
            Unbind();

            if (aBattleManager.Context.GetParty(aTargetSide) is not PPBattleParty party)
            {
                CustomConsoleLog.Warning("Resource", "対象パーティが PPBattleParty ではありません。", this);
                return;
            }

            mTargetParty = party;
            mContext = aBattleManager.Context;
            if (mItemNotifier != null) mItemNotifier.OnChargeItemDropped += HandleItemDropped;
            if (mSpawner != null) mSpawner.TicketSource = this;
        }

        // 購読を解除し、アイテムの出現を止める。バトル終了時や破棄時に呼ぶ
        public void Unbind()
        {
            if (mItemNotifier != null) mItemNotifier.OnChargeItemDropped -= HandleItemDropped;
            if (mSpawner != null && ReferenceEquals(mSpawner.TicketSource, this)) mSpawner.TicketSource = null;
            mTargetParty = null;
            mContext = null;
        }

        /* 操作 */

        // 次に出現させるアイテムの中身を決める。ユニットごとのアイテムは、生存ユニットから 1 体を選んで入る先に決める
        // 同じユニットが複数いても、選んだ 1 体だけが入る先になる
        public PPChargeItemTicket IssueTicket()
        {
            if (mTargetParty == null || mContext == null) return null;

            mCandidates.Clear();
            foreach (var member in mTargetParty.ActiveMembers)
            {
                if (member is PPBattleUnit unit && unit.IsAlive) mCandidates.Add(unit);
            }
            if (mCandidates.Count == 0) return null;

            var random = mContext.Rules.RandomProvider;
            if (!random.NextBool(mUnitItemRate)) return new PPChargeItemTicket(null, null);

            var target = mCandidates[random.NextInt(mCandidates.Count)];
            var icon = mUnitVisualCatalog != null ? mUnitVisualCatalog.Resolve(target.UnitId)?.UnitIcon : null;
            return new PPChargeItemTicket(target, icon);
        }

        /* イベントハンドラ */

        // アイテムが落ちたとき、中身に従ってスキルゲージを溜める
        // 入る先のユニットが倒れていた場合は、誰にも入らない
        private void HandleItemDropped(PPChargeItemTicket aTicket)
        {
            if (aTicket == null || mTargetParty == null) return;

            if (aTicket.IsParty)
            {
                int count = 0;
                foreach (var member in mTargetParty.ActiveMembers)
                {
                    if (member is not PPBattleUnit unit || !unit.IsAlive) continue;

                    unit.ExtraParameters.SkillGauge.Recover(mPartyChargeAmount);
                    count++;
                }
                CustomConsoleLog.Verbose("Resource",
                    $"全体用のチャージアイテムで、生存{count}体のスキルゲージが{mPartyChargeAmount:0.##}ずつ溜まりました。", this);
                return;
            }

            var target = aTicket.Target;
            if (!target.IsAlive) return;

            target.ExtraParameters.SkillGauge.Recover(mUnitChargeAmount);
            CustomConsoleLog.Verbose("Resource",
                $"{target.DisplayName}のチャージアイテムで、スキルゲージが{mUnitChargeAmount:0.##}溜まりました（残量{target.ExtraParameters.SkillGauge.Current:0.##}）。", this);
        }
    }
}
