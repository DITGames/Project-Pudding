/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file PPTacticStepResolution.cs
 * @author hqrse
 * @date 2026/08/11
 * @brief 戦術ステップを今の状況へ当てはめた解決結果
 * =====================================*/

using System;
using CommandBattleCore;

namespace PPCore
{
    // 戦術ステップ 1 つを今の盤面へ当てはめた結果
    // 「誰が」「何を」「誰に」と、それに掛かるコストを 1 つにまとめたもの
    // 採用が決まるまでコマンドを作らずデリゲートで遅らせるのは、
    // 待機判断で見送られた分だけ無駄なインスタンスを作らないため
    // 盤面は思考のたびに変わるので、この解決結果は 1 回の思考だけで使い捨てる
    public sealed class PPTacticStepResolution
    {
        // このステップを実行するユニット
        public PPBattleUnit Actor;
        // 対象ユニット。スコープ既定に任せる場合は null
        public PPBattleUnit Target;
        // 使用するスキル。通常攻撃の場合は null
        public PPBattleSkill Skill;
        // 実行に必要なリソースコスト
        public PPResourceCost Cost;
        // このステップの消化に必要な行動回数
        public int RequiredActionCount = 1;

        // 採用時にコマンドを生成するファクトリ。対象は生成時点で焼き込まれている
        public Func<BattleContext, BattleCommandBase> BuildCommand;

        // 表示用の行動名。通常攻撃は専用の表記になる
        public string DisplayName => Skill != null ? Skill.DisplayName : "通常攻撃";
    }
}
