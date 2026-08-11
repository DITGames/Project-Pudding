/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file PPTacticActionLedger.cs
 * @author hqrse
 * @date 2026/08/11
 * @brief 思考中のみ使うユニット別の行動回数の仮押さえ帳
 * =====================================*/

using System.Collections.Generic;
using CommandBattleCore;

namespace PPCore
{
    // AI の思考中だけ使う、ユニット別の行動回数の仮想残量
    // 実際の消費は BattleManager がコマンド実行時に ActionBudget へ行うため、
    // 計画段階では「このティックで既に何回積んだか」をここで数えて上限を超えないようにする
    // 実際の ActionBudget には一切手を触れない
    public sealed class PPTacticActionLedger
    {
        // ユニットごとに仮押さえした行動回数
        private readonly Dictionary<PPBattleUnit, int> mReserved = new();

        // 指定ユニットが今から aCount 回分の行動を積めるかを判定する
        // 生存と行動制限もここでまとめて見る
        // aUnit : 判定対象のユニット
        // aCount : 積みたい行動回数
        // return : 積める場合 true
        public bool CanAct(PPBattleUnit aUnit, int aCount = 1)
        {
            if (aUnit == null || !aUnit.IsAlive) return false;
            if ((aUnit.CurrentRestrictions & ActionRestriction.CannotAct) != 0) return false;

            return Remaining(aUnit) >= aCount;
        }

        // 指定ユニットの残り行動回数を返す
        // aUnit : 対象ユニット
        // return : 仮押さえ分を差し引いた残り回数
        public int Remaining(PPBattleUnit aUnit)
        {
            if (aUnit == null) return 0;

            int reserved = mReserved.TryGetValue(aUnit, out var value) ? value : 0;
            int budget = aUnit.Actions.Remaining + aUnit.Actions.ExtraActions;
            return budget - reserved;
        }

        // 行動回数を仮押さえする
        // aUnit : 対象ユニット
        // aCount : 仮押さえする回数
        public void Reserve(PPBattleUnit aUnit, int aCount = 1)
        {
            if (aUnit == null) return;

            mReserved[aUnit] = (mReserved.TryGetValue(aUnit, out var value) ? value : 0) + aCount;
        }
    }
}
