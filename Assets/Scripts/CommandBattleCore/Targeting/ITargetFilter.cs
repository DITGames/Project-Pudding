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
    /// <summary>
    /// <see cref="ITargetResolver"/> が出した対象候補を後段で絞り込む・並べ替えるフィルタ。
    /// <para>
    /// <see cref="BattleRules.TargetFilters"/> に登録された順に適用され、
    /// 前段の出力が次段の入力になる。
    /// 「かばう」「挑発でヘイト先を強制する」といった、
    /// 対象の取り方そのものではなく後から差し替わる仕様をここで表現する。
    /// </para>
    /// </summary>
    public interface ITargetFilter
    {
        /// <summary>
        /// 対象候補を加工する。
        /// </summary>
        /// <param name="aSource">行動主体のユニット。</param>
        /// <param name="aCandidates">前段までの対象候補。</param>
        /// <param name="aContext">バトルコンテキスト。</param>
        /// <returns>加工後の対象リスト。</returns>
        List<BattleUnit> Filter(BattleUnit aSource, List<BattleUnit> aCandidates, BattleContext aContext);
    }
}
