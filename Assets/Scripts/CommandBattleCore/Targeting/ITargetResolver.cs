/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file ITargetResolver.cs
 * @author hqrse
 * @date 2026/06/13
 * @brief ターゲット解決の差し替え
 * =====================================*/
using System.Collections.Generic;

namespace CommandBattleCore
{
    /// <summary>
    /// 行動の対象を決めるリゾルバ。
    /// <para>
    /// 「敵単体」「味方全体」といった対象の取り方をこのインターフェースの実装として表現し、
    /// コマンドやスキルはリゾルバを差し替えるだけで対象範囲を変えられる。
    /// 実装は <see cref="StandardResolver"/> 系を参照。
    /// </para>
    /// <para>
    /// 呼び出し側は直接ではなく <see cref="BattleContext.ResolveTargets"/> 経由で使うこと。
    /// そちらを通すと <see cref="ITargetFilter"/> による絞り込みも掛かる。
    /// </para>
    /// </summary>
    public interface ITargetResolver
    {
        /// <summary>
        /// 対象を解決する。
        /// </summary>
        /// <param name="aSource">行動主体のユニット。陣営の判定に使う。</param>
        /// <param name="aContext">バトルコンテキスト。</param>
        /// <returns>対象ユニットのリスト。該当なしの場合は空リスト。</returns>
        List<BattleUnit> Resolve(BattleUnit aSource, BattleContext aContext);
    }
}
