/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file PPTacticUnitSelector.cs
 * @author hqrse
 * @date 2026/08/11
 * @brief 選択ルールに従って候補から 1 つを選ぶヘルパー
 * =====================================*/

using System.Collections.Generic;
using CommandBattleCore;
using UnityEngine;

namespace PPCore
{
    // 候補が複数あったときに PPTacticSelectRule で 1 つへ絞り込むヘルパー
    // 実行者・対象・使用スキルのどれも同じ規則で絞れるようにしてある
    // ユニットに対する比較は、そのユニットが持つスキルの値を代表値として使う
    // 乱数は必ず aContext.Rules.RandomProvider を経由すること（UnityEngine.Random は使わない）
    public static class PPTacticUnitSelector
    {
        // 候補ユニットから 1 体を選ぶ
        // 同値の場合はリストの先頭側が残るため、候補の並び順が安定していれば結果も安定する
        // aCandidates : 選択対象の候補
        // aRule : 選択ルール
        // aContext : 乱数供給元を含むバトルコンテキスト
        // return : 選ばれたユニット。候補が空なら null
        public static PPBattleUnit SelectUnit(IReadOnlyList<PPBattleUnit> aCandidates, PPTacticSelectRule aRule,
            BattleContext aContext)
        {
            if (aCandidates == null || aCandidates.Count == 0) return null;
            if (aCandidates.Count == 1) return aCandidates[0];

            if (aRule == PPTacticSelectRule.Random)
                return aCandidates[aContext.Rules.RandomProvider.NextInt(aCandidates.Count)];

            PPBattleUnit best = null;
            float bestScore = 0f;
            foreach (var unit in aCandidates)
            {
                float score = ScoreUnit(unit, aRule);
                if (best == null || score > bestScore)
                {
                    best = unit;
                    bestScore = score;
                }
            }
            return best;
        }

        // 候補スキルから 1 つを選ぶ
        // aCandidates : 選択対象の候補（スキルと定義の組）
        // aRule : 選択ルール
        // aContext : 乱数供給元を含むバトルコンテキスト
        // return : 選ばれたスキルと定義の組。候補が空なら (null, null)
        public static (PPBattleSkill Skill, PPSkillDefinition Definition) SelectSkill(
            IReadOnlyList<(PPBattleSkill Skill, PPSkillDefinition Definition)> aCandidates,
            PPTacticSelectRule aRule, BattleContext aContext)
        {
            if (aCandidates == null || aCandidates.Count == 0) return (null, null);
            if (aCandidates.Count == 1) return aCandidates[0];

            if (aRule == PPTacticSelectRule.Random)
                return aCandidates[aContext.Rules.RandomProvider.NextInt(aCandidates.Count)];

            (PPBattleSkill Skill, PPSkillDefinition Definition) best = (null, null);
            float bestScore = 0f;
            foreach (var candidate in aCandidates)
            {
                float score = ScoreSkill(candidate.Definition, aRule);
                if (best.Definition == null || score > bestScore)
                {
                    best = candidate;
                    bestScore = score;
                }
            }
            return best;
        }

        // 実行者と使用スキルの組から 1 つ選ぶ
        // 実行者を先に決めてからスキルを探すと、スキルを持たない人が選ばれた時点で
        // 他に撃てる人が居ても打ち切られてしまうため、組にしてから選ぶ
        // 比較値はそのステップで実際に使うスキルの値になるので、
        // ユニットの全所持スキルから集計する SelectUnit とは意味が異なる
        // aCandidates : 選択対象の候補（実行者・スキル・定義の組）
        // aRule : 選択ルール
        // aContext : 乱数供給元を含むバトルコンテキスト
        // return : 選ばれた組。候補が空なら全要素 null
        public static (PPBattleUnit Actor, PPBattleSkill Skill, PPSkillDefinition Definition) SelectActorSkill(
            IReadOnlyList<(PPBattleUnit Actor, PPBattleSkill Skill, PPSkillDefinition Definition)> aCandidates,
            PPTacticSelectRule aRule, BattleContext aContext)
        {
            if (aCandidates == null || aCandidates.Count == 0) return (null, null, null);
            if (aCandidates.Count == 1) return aCandidates[0];

            if (aRule == PPTacticSelectRule.Random)
                return aCandidates[aContext.Rules.RandomProvider.NextInt(aCandidates.Count)];

            (PPBattleUnit Actor, PPBattleSkill Skill, PPSkillDefinition Definition) best = (null, null, null);
            float bestScore = 0f;
            foreach (var candidate in aCandidates)
            {
                float score = ScoreSkill(candidate.Definition, aRule);
                if (best.Actor == null || score > bestScore)
                {
                    best = candidate;
                    bestScore = score;
                }
            }
            return best;
        }

        // ユニットの比較値を求める
        // ユニット自体は AI スコアもコストも持たないため、保持スキルの値を代表値として使う
        // コストが低いほうを選びたい場合は符号を反転して「大きいほうが良い」に揃える
        // aUnit : 対象ユニット
        // aRule : 選択ルール
        // return : 大きいほど優先される比較値
        private static float ScoreUnit(PPBattleUnit aUnit, PPTacticSelectRule aRule)
            => aRule switch
            {
                PPTacticSelectRule.HighestAIScore => AggregateSkills(aUnit, PPTacticSelectRule.HighestAIScore),
                PPTacticSelectRule.HighestCost => AggregateSkills(aUnit, PPTacticSelectRule.HighestCost),
                PPTacticSelectRule.LowestCost => -AggregateSkills(aUnit, PPTacticSelectRule.LowestCost),
                _ => 0f,
            };

        // ユニットが持つスキルから代表値を集計する
        // AI スコアと最大コストは最大値を、最小コストは最小値を取る
        // aUnit : 対象ユニット
        // aRule : 集計の種類
        // return : 集計された値。対象スキルが無ければ 0
        private static float AggregateSkills(PPBattleUnit aUnit, PPTacticSelectRule aRule)
        {
            bool isFound = false;
            float result = 0f;

            foreach (var (_, definition) in PPSkillTagUtility.EnumerateTaggedSkills(aUnit, null))
            {
                float value = aRule == PPTacticSelectRule.HighestAIScore
                    ? definition.AIScore
                    : definition.Cost.Total;

                if (!isFound)
                {
                    result = value;
                    isFound = true;
                    continue;
                }

                result = aRule == PPTacticSelectRule.LowestCost
                    ? Mathf.Min(result, value)
                    : Mathf.Max(result, value);
            }
            return result;
        }

        // スキルの比較値を求める。コストが低いほうを選びたい場合は符号を反転する
        // aDefinition : 対象スキルの定義
        // aRule : 選択ルール
        // return : 大きいほど優先される比較値
        private static float ScoreSkill(PPSkillDefinition aDefinition, PPTacticSelectRule aRule)
            => aRule switch
            {
                PPTacticSelectRule.HighestAIScore => aDefinition.AIScore,
                PPTacticSelectRule.HighestCost => aDefinition.Cost.Total,
                PPTacticSelectRule.LowestCost => -aDefinition.Cost.Total,
                _ => 0f,
            };
    }
}
