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
    // バトル中の判定ロジックを差し替え可能な形でまとめた設定オブジェクト
    // 命中・クリティカル・乱数・詠唱可否・ターゲット絞り込み・死亡対象の扱いといった
    // 「どう判定するか」をすべてインターフェースで保持し、既定実装をあらかじめ入れてある
    public class BattleRules
    {
        // 命中判定を行うリゾルバ
        public IHitResolver HitResolver { get; set; } = new StandardHitResolver();
        // クリティカル判定を行うリゾルバ
        public ICriticalResolver CriticalResolver { get; set; } = new StandardCriticalResolver();
        // バトル中の乱数供給元。AI を含め乱数は必ずここを経由させる
        public IRandomProvider RandomProvider { get; set; } = new DefaultRandomProvider();
        // スキル発動可否（コスト・クールダウン等）を検証するバリデータ
        public ICastValidator CastValidator { get; set; } = new DefaultCastValidator();
        // ターゲット候補に対して順に適用される絞り込みフィルタ群
        public List<ITargetFilter> TargetFilters { get; } = new();
        // 対象が死亡していた場合の代替ターゲット決定ポリシー
        public IDeadTargetPolicy DeadTargetPolicy { get; set; } = new FirstAliveFallback();
    }
}
