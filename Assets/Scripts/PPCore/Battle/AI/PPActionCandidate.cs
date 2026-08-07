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
    }
}
