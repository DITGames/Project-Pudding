/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file PPResourceSimulation.cs
 * @author hqrse
 * @date 2026/08/03
 * @brief ゲージ供給シミュレーション
 * =====================================*/

using System;
using CommandBattleCore;
using UnityEngine;
using AttributeUtility;

namespace PPCore
{
    // プッシャーを持たない陣営へ、ティックごとにゲージを供給するための設定
    // 実機のコインプッシャーが無い環境や敵側の収入を、コインブリッジと同じ
    // 「生存ユニットへ均等分配」の形で代替する
    [Serializable]
    public class PPResourceSimulation
    {
        // 1 ティックあたりに供給するコインゲージ量の範囲（最小, 最大）
        [Label("コインゲージ供給量")]
        [SerializeField] private Vector2Int mCoinGaugeAmount = new Vector2Int(5, 10);
        // 1 ティックあたりに供給するスキルゲージ量の範囲（最小, 最大）
        [Label("スキルゲージ供給量")]
        [SerializeField] private Vector2Int mSkillGaugeAmount = new Vector2Int(0, 0);

        // 設定量を抽選し、対象パーティの生存ユニットへ均等分配する
        // 乱数はシード管理・再現性のため aContext.Rules.RandomProvider を経由する
        // aParty : 供給先のパーティ。null なら何もしない
        // aContext : 乱数供給元を含むバトルコンテキスト
        public void Supply(PPBattleParty aParty, BattleContext aContext)
        {
            if (aParty == null || aContext == null) return;

            var random = aContext.Rules.RandomProvider;
            PPGaugeUtility.DistributeToAliveUnits(aParty.ActiveMembers, PPGaugeKind.Coin,
                random.NextInt(mCoinGaugeAmount.x, mCoinGaugeAmount.y));
            PPGaugeUtility.DistributeToAliveUnits(aParty.ActiveMembers, PPGaugeKind.Skill,
                random.NextInt(mSkillGaugeAmount.x, mSkillGaugeAmount.y));
        }
    }
}
