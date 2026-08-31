/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file PPUnitActionLedger.cs
 * @author hqrse
 * @date 2026/08/26
 * @brief 1ティック分の行動をユニットごとに仮押さえする台帳
 * =====================================*/

using System.Collections.Generic;
using UnityEngine;

namespace PPCore
{
    // 1 ティック分の行動を積むあいだ、ユニットごとのゲージと行動回数を仮押さえする台帳
    //
    // ゲージが実際に減るのは行動を実行する時点だが、実行前に複数の行動を積む都合上、
    // 「まだ払っていないが払う予定の分」を差し引いて判定しないと同じゲージを二重に当てにしてしまう
    // 大技を 2 回積んだのに 1 回分しか払えず 2 回目が空振りする、という状態を防ぐためのもの
    //
    // 敵 AI は 1 回の思考のあいだ、プレイヤーは 1 ティックの予約が消化されるまで、それぞれ同じ台帳を使う
    // 味方と敵でユニットが重ならないため、台帳を分けても判定が混ざることはない
    public sealed class PPUnitActionLedger
    {
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

        // ユニットごとの仮押さえ内容
        private readonly Dictionary<PPBattleUnit, PPLedgerEntry> mEntries = new();

        // 積んだ行動の総数
        public int TotalActionCount { get; private set; }

        // 台帳を空にする。ティックが切り替わったタイミングで呼ぶ
        public void Clear()
        {
            mEntries.Clear();
            TotalActionCount = 0;
        }

        // そのユニットが積んだ行動の数を返す
        // aUnit : 対象ユニット
        // return : 積んだ行動の数
        public int ReservedCount(PPBattleUnit aUnit)
            => mEntries.TryGetValue(aUnit, out var entry) ? entry.ActionCount : 0;

        // そのユニットがまだ行動を積めるかを返す
        // 1 ティックあたりの行動回数上限（バフ込み）と、積んだ数を突き合わせる
        // aUnit : 対象ユニット
        // return : まだ積める場合 true
        public bool HasActionLeft(PPBattleUnit aUnit)
            => aUnit != null && ReservedCount(aUnit) < aUnit.ResolveActionCount();

        // 仮押さえ分を差し引いたスキルゲージの残量
        // aUnit : 対象ユニット
        // return : 残量
        public float RemainingSkillGauge(PPBattleUnit aUnit)
            => aUnit.ExtraParameters.SkillGauge.Current - ResolveEntry(aUnit, false).SkillGauge;

        // 仮押さえ分を差し引いたコインゲージの残量
        // aUnit : 対象ユニット
        // return : 残量
        public float RemainingCoinGauge(PPBattleUnit aUnit)
            => aUnit.ExtraParameters.CoinGauge.Current - ResolveEntry(aUnit, false).CoinGauge;

        // スキルゲージを仮押さえできるかを判定する
        // 行動回数の空きも併せて見るため、これ 1 つで「あと 1 手積めるか」が判る
        // aUnit : 対象ユニット
        // aCost : 必要なスキルゲージ量
        // return : 積める場合 true
        public bool CanReserveSkill(PPBattleUnit aUnit, float aCost)
            => HasActionLeft(aUnit) && RemainingSkillGauge(aUnit) + PPGaugeUtility.CompareEpsilon >= aCost;

        // コインゲージを仮押さえできるかを判定する
        // aUnit : 対象ユニット
        // aCost : 必要なコインゲージ量
        // return : 積める場合 true
        public bool CanReserveCoin(PPBattleUnit aUnit, float aCost)
            => HasActionLeft(aUnit) && RemainingCoinGauge(aUnit) + PPGaugeUtility.CompareEpsilon >= aCost;

        // スキル 1 回分を仮押さえする
        // aUnit : 対象ユニット
        // aCost : 消費予定のスキルゲージ量
        public void ReserveSkill(PPBattleUnit aUnit, float aCost)
        {
            var entry = ResolveEntry(aUnit, true);
            entry.SkillGauge += Mathf.Max(0f, aCost);
            entry.ActionCount++;
            TotalActionCount++;
        }

        // 通常攻撃 1 回分を仮押さえする
        // aUnit : 対象ユニット
        // aCost : 消費予定のコインゲージ量
        public void ReserveCoin(PPBattleUnit aUnit, float aCost)
        {
            var entry = ResolveEntry(aUnit, true);
            entry.CoinGauge += Mathf.Max(0f, aCost);
            entry.ActionCount++;
            TotalActionCount++;
        }

        // ユニットの仮押さえ内容を引く
        // aUnit : 対象ユニット
        // aIsCreate : 未登録の場合に作るか。判定のみのときは作らない
        // return : 仮押さえ内容。作らない指定で未登録なら空の内容
        private PPLedgerEntry ResolveEntry(PPBattleUnit aUnit, bool aIsCreate)
        {
            if (mEntries.TryGetValue(aUnit, out var entry)) return entry;
            if (!aIsCreate) return new PPLedgerEntry();

            entry = new PPLedgerEntry();
            mEntries[aUnit] = entry;
            return entry;
        }
    }
}
