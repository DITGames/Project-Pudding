/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file PPActionCandidate.cs
 * @author hqrse
 * @date 2026/07/16
 * @brief 戦略層が評価する行動候補
 * =====================================*/

using System;
using CommandBattleCore;

namespace PPCore
{
    // 行動候補が採用されなかった理由。デバッグ表示で「なぜ撃たなかったか」を追うのに使う
    public enum PPActionRejectReason
    {
        // 却下されていない（採用済み、または未判定）
        None,
        // 効用が λ × コストに届かなかった。今は撃つ価値が無いと判断された
        BelowLambda,
        // 使ってよい額が足りなかった
        NotEnoughBudget,
        // 同時行動数の上限に達した
        ActionLimit,
        // 同じユニットが既に別の行動を採用済み
        UnitAlreadyActed,
        // 対象の状態から見て効果が無くなった（満タンの味方への回復、倒れる予定の敵への追撃など）
        NoEffect,
    }

    // AI が評価する行動候補 1 件分
    // スコアリングに必要な情報（ロール・コスト・対象）と、
    // 採用が決まった時点で実際のコマンドを作るためのファクトリを 1 つにまとめたもの
    // コマンドを先に作らずデリゲートで遅延させることで、
    // 採用されなかった候補の分だけ無駄にインスタンスを作らずに済む
    public sealed class PPActionCandidate
    {
        // この行動を取るユニット
        public PPBattleUnit Unit;
        // 行動のロール。単一フラグを想定する（複数ロールを持つスキルはロールごとに候補を分ける）
        // シチュエーション係数・ロール別重みの解決と実行順序の決定に使う
        public PPBattleSkillRole Role;
        // この行動に必要なリソースコスト
        public PPResourceCost Cost;
        // 使用するスキル。通常攻撃の場合は null
        public PPBattleSkill Skill;
        // 対象ユニット。範囲行動や自己完結する行動では null
        public PPBattleUnit Target;

        // 採用時にコマンドを生成するファクトリ。対象は生成時点で焼き込まれている
        public Func<BattleContext, BattleCommandBase> BuildCommand;

        // ロール別の基礎AIスコア。スキルなら PPSkillDefinition.RoleScores、
        // 通常攻撃なら PPBattleRules.NormalAttackAIScore から生成時に設定される
        public float AIScore;

        // 評価済みのスコア。PPPartyAIStrategistBase.Evaluate が設定する
        public float Score;

        // 対象への効果量の見積もり。新 AI が候補生成時に設定する
        public PPEffectEstimate Estimate;

        // 戦況を反映した最終的な効用。PPActionUtilityEvaluator が設定する
        public float Utility;

        // 却下された理由。採用された場合は None のまま
        public PPActionRejectReason RejectReason;

        // 同じユニットの上位候補が却下された後で採用されたか
        // 「本命が買えなかったので次善で動いた」ことをデバッグ表示から読み取れるようにする
        public bool IsFallback;

        // 表示用のスキル名。通常攻撃は専用の表記になる
        public string DisplayName => Skill != null ? Skill.DisplayName : "通常攻撃";
    }
}
