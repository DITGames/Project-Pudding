/* =====================================
 * Copyright WabisabiAndons. All rights reserved.
 * @file BattleRules.cs
 * @author hqrse
 * @date 2026/06/13
 * @brief バトルルール
 * =====================================*/

using System.Collections.Generic;

namespace CommandBattleCore
{
    // バトル中の判定ロジックと調整値を差し替え可能な形でまとめた設定オブジェクト
    // 命中・クリティカル・乱数・詠唱可否・ターゲット絞り込み・死亡対象の扱い・行動順・勝敗判定といった
    // 「どう判定するか」をすべてインターフェースで保持し、既定実装をあらかじめ入れてある
    // 進行そのもの（キュー・ステート・演出やログの出力先）は BattleManager 側の持ち物で、ここには入れない
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
        // ターンごとの行動順並び替えクラス。既定は素早さ順
        public ITurnOrderResolver TurnOrderResolver { get; set; } = new SpeedTurnOrderResolver();
        // 勝敗判定クラス。差し替えることで引き分け条件などを追加できる
        public IBattleResultChecker ResultChecker { get; set; } = new DefaultBattleResultChecker();
        // 1 イベント当たりのリアクション上限
        // 想定外の連鎖に対する安全網であり、意図した連鎖の制御はリアクション側の条件判定で行う
        public int MaxReactionPerEvent { get; set; } = 1;
    }
}
