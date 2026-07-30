/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file ITargetFilter.cs
 * @author hqrse
 * @date 2026/06/13
 * @brief TargetResolverが解決した対象を加工するフィルタ
 * =====================================*/

using System.Collections.Generic;

namespace CommandBattleCore
{
    // ITargetResolver が出した対象候補を後段で絞り込む・並べ替えるフィルタ
    // BattleRules.TargetFilters に登録された順に適用され、
    // 前段の出力が次段の入力になる
    // 「かばう」「挑発でヘイト先を強制する」といった、
    // 対象の取り方そのものではなく後から差し替わる仕様をここで表現する
    public interface ITargetFilter
    {
        // 対象候補を加工する
        // aSource : 行動主体のユニット
        // aCandidates : 前段までの対象候補
        // aContext : バトルコンテキスト
        // return : 加工後の対象リスト
        List<BattleUnit> Filter(BattleUnit aSource, List<BattleUnit> aCandidates, BattleContext aContext);
    }
}
