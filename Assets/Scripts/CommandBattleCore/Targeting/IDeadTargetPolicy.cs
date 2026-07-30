/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file IDeadTargetPolicy.cs
 * @author hqrse
 * @date 2026/06/13
 * @brief 単体対象がいないときの代替選択方式
 * =====================================*/

using System.Collections.Generic;

namespace CommandBattleCore
{
    /// <summary>
    /// 単体対象を指定していたが、行動時点でその対象が戦闘不能だった場合の代替決定ポリシー。
    /// <para>
    /// コマンド入力から実行までにラグがある（キューに積まれる・演出を挟む）以上、
    /// 選んだ相手が先に倒れているケースは必ず起きる。そのときに
    /// 「別の相手に流す」のか「不発にする」のかはゲーム性の問題なので、差し替え可能にしてある。
    /// <see cref="BattleRules.DeadTargetPolicy"/> に設定する。
    /// </para>
    /// </summary>
    public interface IDeadTargetPolicy
    {
        /// <summary>
        /// 代替対象を決定する。
        /// </summary>
        /// <param name="aSource">行動主体のユニット。</param>
        /// <param name="aNoneTarget">本来狙っていた、既に対象にできないユニット。</param>
        /// <param name="aAliveCandidates">代替候補となる生存ユニット。</param>
        /// <param name="aContext">バトルコンテキスト。</param>
        /// <returns>代替対象のリスト。不発にする場合は空リスト。</returns>
        List<BattleUnit> Fallback(BattleUnit aSource, BattleUnit aNoneTarget,
            List<BattleUnit> aAliveCandidates, BattleContext aContext);
    }

    /// <summary>
    /// 生存している先頭のユニットへ対象を置き換えるポリシー。既定の挙動。
    /// 行動が無駄になりにくい代わりに、狙いが必ずずれる。
    /// </summary>
    public class FirstAliveFallback : IDeadTargetPolicy
    {
        /// <summary>生存候補の先頭を返す。候補が無ければ空リスト。</summary>
        public List<BattleUnit> Fallback(BattleUnit aSource, BattleUnit aNoneTarget,
            List<BattleUnit> aAliveCandidates, BattleContext aContext)
                => aAliveCandidates.Count > 0 ? new List<BattleUnit> {aAliveCandidates[0]} : new List<BattleUnit>();
    }

    /// <summary>
    /// 代替を探さず不発にするポリシー。狙った相手が倒れていたら行動が空振りする。
    /// </summary>
    public class NoFallback : IDeadTargetPolicy
    {
        /// <summary>常に空リストを返す。</summary>
        public List<BattleUnit> Fallback(BattleUnit aSource, BattleUnit aNoneTarget,
            List<BattleUnit> aAliveCandidates, BattleContext aContext)
            => new List<BattleUnit>();
    }

    /// <summary>
    /// 生存ユニットからランダムに代替を選ぶポリシー。
    /// </summary>
    public class RandomFallback : IDeadTargetPolicy
    {
        /// <summary>生存候補からランダムに 1 体返す。候補が無ければ空リスト。</summary>
        public List<BattleUnit> Fallback(BattleUnit aSource, BattleUnit aNoneTarget,
            List<BattleUnit> aAliveCandidates, BattleContext aContext)
                => aAliveCandidates.Count > 0
                    ? new List<BattleUnit> {aAliveCandidates[aContext.Rules.RandomProvider.NextInt(aAliveCandidates.Count)]}
                    : new List<BattleUnit>();
    }
}
