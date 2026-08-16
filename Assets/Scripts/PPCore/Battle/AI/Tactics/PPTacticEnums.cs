/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file PPTacticEnums.cs
 * @author hqrse
 * @date 2026/08/11
 * @brief 戦術ステップが使う列挙型一式
 * =====================================*/

using UnityEngine;

namespace PPCore
{
    // 戦術ステップの対象をどう選ぶか
    // 単体対象のスキルはここで選ばれたユニットを焼き込んだリゾルバで実行される
    public enum PPTacticTargetPolicy
    {
        // スキルの TargetScope 既定のリゾルバをそのまま使う。全体攻撃など対象指定が要らない場合
        [InspectorName("スコープ既定")]
        ScopeDefault,
        // HP 割合が最も低い味方。回復の基本
        [InspectorName("HP割合が最低の味方")]
        LowestHpRatioAlly,
        // HP 割合が最も低い敵。とどめを狙う
        [InspectorName("HP割合が最低の敵")]
        LowestHpRatioEnemy,
        // 対象条件リストに合致する味方。「大技を持つ味方にバフを掛ける」のような指定に使う
        [InspectorName("条件に合う味方")]
        ConditionAlly,
        // 対象条件リストに合致する敵
        [InspectorName("条件に合う敵")]
        ConditionEnemy,
        // 直前のステップが解決した対象と同じ相手。集中攻撃やバフ対象への追撃を成立させる
        [InspectorName("直前ステップと同じ対象")]
        PreviousStepTarget,
        // 実行者自身。自己強化に使う
        [InspectorName("自分自身")]
        Self,
        // 脅威度が最も高い敵。攻撃力を基準に判定する
        [InspectorName("最も脅威の高い敵")]
        HighestThreatEnemy,
        // 生存する敵からランダム
        [InspectorName("ランダムな敵")]
        RandomEnemy,
        // 生存する味方からランダム
        [InspectorName("ランダムな味方")]
        RandomAlly,
    }

    // 候補が複数あったときにどれを採るか
    // 実行者・対象・使用スキルのいずれの絞り込みにも同じ規則を使う
    // ユニットに対して適用する場合は、そのユニットが持つスキルの値で比較する
    public enum PPTacticSelectRule
    {
        // AI スコアが最も高いもの。ユニットに対しては保持スキルの AI スコア最大値で比較する
        [InspectorName("AIスコアが高い")]
        HighestAIScore,
        // コストが最も低いもの。ユニットに対しては保持スキルの最小コストで比較する
        [InspectorName("コストが低い")]
        LowestCost,
        // コストが最も高いもの。ユニットに対しては保持スキルの最大コストで比較する
        [InspectorName("コストが高い")]
        HighestCost,
        // ランダム
        [InspectorName("ランダム")]
        Random,
    }

    // 戦術がこの思考で候補外になった理由
    // デバッグ表示で「なぜその戦術が動かなかったか」を追うために使う
    public enum PPTacticRejectReason
    {
        // 候補外ではない（実行可能）
        None,
        // 1 バトル 1 回の戦術で、既に消化済み
        DoneOnce,
        // クールタイム中
        Cooldown,
        // 成立条件を満たさなかった
        ConditionFailed,
        // 戦術に有効なステップが 1 つも無い（設定ミス）
        NoSteps,
        // ステップの実行者条件に合うユニットが居ない
        NoActor,
        // ステップのタグに合致する、発動できるスキルが無い
        NoSkill,
        // ステップの対象を解決できなかった
        NoTarget,
        // リソースが足りず、増加も見込めないため待っても撃てない
        NoIncome,
        // 撃てるまでに掛かるティック数が許容待機ティック数を超えた
        TooFarToWait,
        // 残りリソースで払えなかった（並行アクションの打ち切り理由）
        NotEnoughResource,
        // 最大実行回数まで回りきった（並行アクションの打ち切り理由。失敗ではない）
        ExecutionLimit,
    }
}
