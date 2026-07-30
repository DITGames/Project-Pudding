/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file BattleRules.cs
 * @author hqrse
 * @date 2026/06/13
 * @brief バトルルール
 * =====================================*/

using System.Collections.Generic;

namespace CommandBattleCore
{
    /// <summary>
    /// バトル中の判定ロジックを差し替え可能な形でまとめた設定オブジェクト。
    /// <para>
    /// 命中・クリティカル・乱数・詠唱可否・ターゲット絞り込み・死亡対象の扱いといった
    /// 「どう判定するか」をすべてインターフェースで保持し、既定実装をあらかじめ入れてある。
    /// </para>
    /// </summary>
    public class BattleRules
    {
        /// <summary>命中判定を行うリゾルバ。</summary>
        public IHitResolver HitResolver { get; set; } = new StandardHitResolver();
        /// <summary>クリティカル判定を行うリゾルバ。</summary>
        public ICriticalResolver CriticalResolver { get; set; } = new StandardCriticalResolver();
        /// <summary>バトル中の乱数供給元。AI を含め乱数は必ずここを経由させる。</summary>
        public IRandomProvider RandomProvider { get; set; } = new DefaultRandomProvider();
        /// <summary>スキル発動可否（コスト・クールダウン等）を検証するバリデータ。</summary>
        public ICastValidator CastValidator { get; set; } = new DefaultCastValidator();
        /// <summary>ターゲット候補に対して順に適用される絞り込みフィルタ群。</summary>
        public List<ITargetFilter> TargetFilters { get; } = new();
        /// <summary>対象が死亡していた場合の代替ターゲット決定ポリシー。</summary>
        public IDeadTargetPolicy DeadTargetPolicy { get; set; } = new FirstAliveFallback();
    }
}
