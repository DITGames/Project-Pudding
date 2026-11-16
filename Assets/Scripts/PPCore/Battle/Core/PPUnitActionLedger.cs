/* =====================================
 * Copyright WabisabiAndons. All rights reserved.
 * @file PPUnitActionLedger.cs
 * @author hqrse
 * @date 2026/08/26
 * @brief 行動の仮押さえ台帳と、実行待ち行動の供給元
 * =====================================*/

using System.Collections.Generic;
using UnityEngine;

namespace PPCore
{
    // 行動を積むあいだ、ユニットごとのゲージと積んだ行動の数を仮押さえする台帳
    // 実行前に複数の行動を積んでも、同じゲージを二重に当てにしないようにする
    public sealed class PPUnitActionLedger
    {
        /* 内部型 */

        // ユニット 1 体分の仮押さえ内容
        private sealed class PPLedgerEntry
        {
            // 仮押さえ済みのスキルゲージ量
            public float SkillGauge;
            // 仮押さえ済みのコインゲージ量
            public float CoinGauge;
            // 積んだ行動の数
            public int ActionCount;
        }

        /* 状態 */

        // 積んだ行動の総数
        public int TotalActionCount { get; private set; }
        // ユニットごとの仮押さえ内容
        private readonly Dictionary<PPBattleUnit, PPLedgerEntry> mEntries = new();

        /* 操作 */

        // 台帳を空にする。ティックが切り替わったタイミングで呼ぶ
        public void Clear()
        {
            mEntries.Clear();
            TotalActionCount = 0;
        }

        // そのユニットが積んだ行動の数を返す
        public int ReservedCount(PPBattleUnit aUnit)
            => mEntries.TryGetValue(aUnit, out var entry) ? entry.ActionCount : 0;

        // 仮押さえ分を差し引いたスキルゲージの残量
        public float RemainingSkillGauge(PPBattleUnit aUnit)
            => aUnit.ExtraParameters.SkillGauge.Current - ResolveEntry(aUnit, false).SkillGauge;

        // 仮押さえ分を差し引いたコインゲージの残量
        public float RemainingCoinGauge(PPBattleUnit aUnit)
            => aUnit.ExtraParameters.CoinGauge.Current - ResolveEntry(aUnit, false).CoinGauge;

        // スキル 1 回分を仮押さえする
        public void ReserveSkill(PPBattleUnit aUnit, float aCost)
        {
            var entry = ResolveEntry(aUnit, true);
            entry.SkillGauge += Mathf.Max(0f, aCost);
            entry.ActionCount++;
            TotalActionCount++;
        }

        // 通常攻撃 1 回分を仮押さえする
        public void ReserveCoin(PPBattleUnit aUnit, float aCost)
        {
            var entry = ResolveEntry(aUnit, true);
            entry.CoinGauge += Mathf.Max(0f, aCost);
            entry.ActionCount++;
            TotalActionCount++;
        }

        /* 問い合わせ */

        // そのユニットがまだ行動を積めるかを返す
        // 積める行動は 1 ユニットにつき 1 つまで
        public bool HasActionLeft(PPBattleUnit aUnit)
            => aUnit != null && ReservedCount(aUnit) < 1;

        // スキルゲージを仮押さえできるかを判定する
        // 行動回数の空きも併せて見るため、これ 1 つで「あと 1 手積めるか」が判る
        public bool CanReserveSkill(PPBattleUnit aUnit, float aCost)
            => HasActionLeft(aUnit) && RemainingSkillGauge(aUnit) + PPGaugeUtility.CompareEpsilon >= aCost;

        // コインゲージを仮押さえできるかを判定する
        public bool CanReserveCoin(PPBattleUnit aUnit, float aCost)
            => HasActionLeft(aUnit) && RemainingCoinGauge(aUnit) + PPGaugeUtility.CompareEpsilon >= aCost;

        // ユニットの仮押さえ内容を引く
        private PPLedgerEntry ResolveEntry(PPBattleUnit aUnit, bool aIsCreate)
        {
            if (mEntries.TryGetValue(aUnit, out var entry)) return entry;
            if (!aIsCreate) return new PPLedgerEntry();

            entry = new PPLedgerEntry();
            mEntries[aUnit] = entry;
            return entry;
        }
    }

    // まだ実行されていない行動（プレイヤーの予約と相手 AI の計画）を AI へ見せるための口
    // 実装はバトルの進行役（PPBattleRunner など）が担う
    public interface IPPPendingActionSource
    {
        // 指定した陣営の、まだ実行されていない行動を列挙する
        IEnumerable<PPPendingAction> EnumeratePending(BattleSide aSide);
    }
}
