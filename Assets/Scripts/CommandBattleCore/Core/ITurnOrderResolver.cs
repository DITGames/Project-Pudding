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
    // ターン内の行動順を決めるリゾルバ。BattleManager.TurnOrderResolver に差し込む
    // 素早さ順・陣営交互・固定順といった方式の違いを実装として切り替えられるようにしてある
    public interface ITurnOrderResolver
    {
        // 現在の行動順を解決する
        // aContext : バトルコンテキスト
        // return : 先に行動する順に並べたユニットのリスト
        List<BattleUnit> ResolveOrder(BattleContext aContext);
    }
}
