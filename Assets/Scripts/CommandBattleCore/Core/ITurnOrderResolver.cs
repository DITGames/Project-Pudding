/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file ITurnOrderResolver.cs
 * @author hqrse
 * @date 2026/06/13
 * @brief 行動順制御インターフェース
 * =====================================*/
using System.Collections.Generic;

namespace CommandBattleCore
{
    /// <summary>
    /// ターン内の行動順を決めるリゾルバ。<see cref="BattleManager.TurnOrderResolver"/> に差し込む。
    /// 素早さ順・陣営交互・固定順といった方式の違いを実装として切り替えられるようにしてある。
    /// </summary>
    public interface ITurnOrderResolver
    {
        /// <summary>
        /// 現在の行動順を解決する。
        /// </summary>
        /// <param name="aContext">バトルコンテキスト。</param>
        /// <returns>先に行動する順に並べたユニットのリスト。</returns>
        List<BattleUnit> ResolveOrder(BattleContext aContext);
    }
}
