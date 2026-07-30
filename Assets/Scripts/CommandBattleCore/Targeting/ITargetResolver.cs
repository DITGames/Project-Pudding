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
    // 行動の対象を決めるリゾルバ
    // 「敵単体」「味方全体」といった対象の取り方をこのインターフェースの実装として表現し、
    // コマンドやスキルはリゾルバを差し替えるだけで対象範囲を変えられる
    // 実装は StandardResolver 系を参照
    // 呼び出し側は直接ではなく BattleContext.ResolveTargets 経由で使うこと
    // そちらを通すと ITargetFilter による絞り込みも掛かる
    public interface ITargetResolver
    {
        // 対象を解決する
        // aSource : 行動主体のユニット。陣営の判定に使う
        // aContext : バトルコンテキスト
        // return : 対象ユニットのリスト。該当なしの場合は空リスト
        List<BattleUnit> Resolve(BattleUnit aSource, BattleContext aContext);
    }
}
