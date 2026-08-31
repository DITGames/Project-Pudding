/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file PPUnitAISkillQuery.cs
 * @author hqrse
 * @date 2026/08/24
 * @brief ユニットの所持スキルを絞り込んで選ぶヘルパー
 * =====================================*/

using System;
using System.Collections.Generic;
using AttributeUtility;
using CommandBattleCore;
using UnityEngine;

namespace PPCore
{
    // 候補が複数あったときにどのスキルを採るか
    public enum PPUnitAISkillSelectRule
    {
        // AI スコアが最も高いもの。「一番強いスキル」の既定
        [InspectorName("AIスコアが高い")]
        HighestAIScore,
        // 必要スキルゲージ量が最も少ないもの
        [InspectorName("消費が少ない")]
        LowestGaugeCost,
        // 必要スキルゲージ量が最も多いもの
        [InspectorName("消費が多い")]
        HighestGaugeCost,
        // ランダム
        [InspectorName("ランダム")]
        Random,
    }

    // 候補の採り方を表示用の日本語へ変換するヘルパー
    // グラフ上の要約表示から使う。インスペクタの表記と揃えてある
    public static class PPUnitAISkillSelectRuleUtility
    {
        // aRule : 候補の採り方
        // return : 日本語の表記
        public static string ToDisplayString(PPUnitAISkillSelectRule aRule)
            => aRule switch
            {
                PPUnitAISkillSelectRule.HighestAIScore => "AIスコアが高い",
                PPUnitAISkillSelectRule.LowestGaugeCost => "消費が少ない",
                PPUnitAISkillSelectRule.HighestGaugeCost => "消費が多い",
                PPUnitAISkillSelectRule.Random => "ランダム",
                _ => "",
            };
    }

    // スキルの指定方法
    public enum PPUnitAISkillFilterMode
    {
        // 同種グループとスキルタグで絞り込む
        [InspectorName("グループ・タグで絞る")]
        GroupAndTag,
        // スキル定義を直接指定する
        [InspectorName("スキルを直接指定")]
        Direct,
    }

    // 所持スキルの絞り込み条件
    //
    // 既定は同種グループとスキルタグによる絞り込みで、どちらも未指定なら全スキルが対象になる
    // 直接指定モードでは、そのスキル定義 1 つだけが候補になる
    // 同じグループ・同じタグのスキルを複数持つユニットで「必ずこのスキル」を確定させたい場合に使う
    // （絞り込みだけでは候補が同値になり、所持順の先頭が採られてしまうため）
    [Serializable]
    public sealed class PPUnitAISkillFilter
    {
        [Label("スキルの指定方法")]
        [SerializeField] private PPUnitAISkillFilterMode mMode = PPUnitAISkillFilterMode.GroupAndTag;
        [Label("スキル定義")]
        [EditCondition(nameof(IsDirectMode), true, false)]
        [SerializeField] private PPSkillDefinition mSkillDefinition;
        [Label("同種グループで絞る")]
        [EditCondition(nameof(IsFilterMode), true, false)]
        [SerializeField] private bool mIsFilterByGroup = true;
        [Label("同種グループ")]
        [EditCondition(nameof(IsGroupFilterEnabled), true, false)]
        [SerializeField] private PPSkillGroup mGroup = PPSkillGroup.Attack;
        [Label("スキルタグ", true)]
        [EditCondition(nameof(IsFilterMode), true, false)]
        [SerializeField] private List<PPSkillTagDefinition> mTags = new();

        public PPUnitAISkillFilterMode Mode => mMode;
        public PPSkillDefinition SkillDefinition => mSkillDefinition;
        public bool IsFilterByGroup => mIsFilterByGroup;
        public PPSkillGroup Group => mGroup;
        public IReadOnlyList<PPSkillTagDefinition> Tags => mTags;

        // スキル定義を直接指定するモードか。入力欄の出し分けに使う
        private bool IsDirectMode => mMode == PPUnitAISkillFilterMode.Direct;
        // 絞り込みモードか。入力欄の出し分けに使う
        private bool IsFilterMode => mMode == PPUnitAISkillFilterMode.GroupAndTag;
        // 同種グループの選択欄を出すか。絞り込みモードで、かつグループ絞りが有効なときだけ意味を持つ
        private bool IsGroupFilterEnabled => IsFilterMode && mIsFilterByGroup;

        // 指定スキルがこの絞り込みに合致するかを判定する
        // ここを通る経路が 1 本なので、直接指定は行動側と条件側の双方へ同時に効く
        // aDefinition : 判定するスキル定義
        // return : 合致する場合 true
        public bool IsMatch(PPSkillDefinition aDefinition)
        {
            if (aDefinition == null) return false;
            // 未設定のまま直接指定モードにしていると、どのスキルにも合致しない（枝が不成立になる）
            if (mMode == PPUnitAISkillFilterMode.Direct) return ReferenceEquals(aDefinition, mSkillDefinition);

            if (mIsFilterByGroup && aDefinition.Group != mGroup) return false;
            return aDefinition.HasAnyTag(mTags);
        }

        // 絞り込み内容を説明文用の文字列へ整形する
        // return : 日本語の表記
        public string ToDisplayString()
        {
            if (mMode == PPUnitAISkillFilterMode.Direct)
            {
                return mSkillDefinition != null ? mSkillDefinition.DisplayName : "スキル未設定";
            }

            string group = mIsFilterByGroup ? PPSkillGroupDefinition.ToDisplayString(mGroup) : "全グループ";
            return mTags == null || mTags.Count == 0
                ? group
                : $"{group}／{PPSkillTagUtility.ToDisplayString(mTags)}";
        }
    }

    // ユニットの所持スキルを絞り込んで 1 つ選ぶヘルパー
    // 行動（PPUnitAISkillAction）と条件（所持判定・発動可否判定）の双方から同じ絞り込みを使うため、
    // 片方に実装を寄せずここへ集約している
    // 乱数は行動するユニット自身の供給元を経由すること（UnityEngine.Random は使わない）
    public static class PPUnitAISkillQuery
    {
        // 絞り込みに合致する所持スキルを列挙する
        // PP 側の定義を持たないスキルは判定できないため対象外になる
        // aUnit : 対象ユニット
        // aFilter : 絞り込み条件。null なら全スキルが対象
        // return : 合致したスキルと、その定義の組
        public static IEnumerable<(PPBattleSkill Skill, PPSkillDefinition Definition)> Enumerate(
            PPBattleUnit aUnit, PPUnitAISkillFilter aFilter)
        {
            if (aUnit == null) yield break;

            foreach (var skill in aUnit.Skills)
            {
                if (skill is not PPBattleSkill ppSkill) continue;
                if (ppSkill.SourceDefinition is not PPSkillDefinition definition) continue;
                if (aFilter != null && !aFilter.IsMatch(definition)) continue;

                yield return (ppSkill, definition);
            }
        }

        // 絞り込みに合致するスキルを 1 つでも持っているかを判定する
        // 発動可否は見ないため、単に所持しているかどうかの判定になる
        // aUnit : 対象ユニット
        // aFilter : 絞り込み条件
        // return : 該当するスキルがあれば true
        public static bool HasAny(PPBattleUnit aUnit, PPUnitAISkillFilter aFilter)
        {
            foreach (var _ in Enumerate(aUnit, aFilter))
            {
                return true;
            }
            return false;
        }

        // 絞り込みに合致するスキルのうち、今すぐ発動できるものから 1 つ選ぶ
        // クールダウン・使用回数・スキルゲージ残量の判定はバリデータへ委ねる
        // aUnit : 対象ユニット
        // aFilter : 絞り込み条件
        // aRule : 候補が複数あったときの選び方
        // aContext : 発動可否の検証と乱数に使うバトルコンテキスト
        // aLedger : 仮押さえ台帳。渡すと「既に積んだ行動で使う予定のゲージ」を差し引いて判定する
        // return : 選ばれたスキルと定義の組。発動できるものが無ければ (null, null)
        public static (PPBattleSkill Skill, PPSkillDefinition Definition) SelectCastable(
            PPBattleUnit aUnit, PPUnitAISkillFilter aFilter, PPUnitAISkillSelectRule aRule, BattleContext aContext,
            PPUnitActionLedger aLedger = null)
        {
            var candidates = new List<(PPBattleSkill Skill, PPSkillDefinition Definition)>();
            foreach (var candidate in Enumerate(aUnit, aFilter))
            {
                if (!aContext.Rules.CastValidator.Validate(aUnit, candidate.Skill, aContext).CanCast) continue;
                // 台帳がある場合は、同じティックで既に積んだ分を差し引いても払えるものだけ残す
                if (aLedger != null && !aLedger.CanReserveSkill(aUnit, candidate.Definition.SkillGaugeCost)) continue;

                candidates.Add(candidate);
            }
            return Select(candidates, aRule, aUnit, aContext);
        }

        // 絞り込みに合致する所持スキルのうち、最も強い（AI スコアが高い）ものを返す
        // 発動可否は見ないため、「持っている中で一番強いのはどれか」の判定に使う
        // aUnit : 対象ユニット
        // aFilter : 絞り込み条件
        // return : 最も AI スコアの高いスキル定義。該当が無ければ null
        public static PPSkillDefinition SelectStrongest(PPBattleUnit aUnit, PPUnitAISkillFilter aFilter)
        {
            PPSkillDefinition best = null;
            foreach (var (_, definition) in Enumerate(aUnit, aFilter))
            {
                if (best == null || definition.AIScore > best.AIScore)
                {
                    best = definition;
                }
            }
            return best;
        }

        // 候補リストから選択規則に従って 1 つ選ぶ
        // 同値の場合はリストの先頭側が残るため、所持順が安定していれば結果も安定する
        // aCandidates : 選択対象の候補
        // aRule : 選択規則
        // aUnit : 選択するユニット。乱数の供給元になる
        // aContext : バトルコンテキスト
        // return : 選ばれた組。候補が空なら (null, null)
        private static (PPBattleSkill Skill, PPSkillDefinition Definition) Select(
            IReadOnlyList<(PPBattleSkill Skill, PPSkillDefinition Definition)> aCandidates,
            PPUnitAISkillSelectRule aRule, PPBattleUnit aUnit, BattleContext aContext)
        {
            if (aCandidates.Count == 0) return (null, null);
            if (aCandidates.Count == 1) return aCandidates[0];

            if (aRule == PPUnitAISkillSelectRule.Random)
                return aCandidates[aUnit.ResolveRandom(aContext).NextInt(aCandidates.Count)];

            (PPBattleSkill Skill, PPSkillDefinition Definition) best = (null, null);
            float bestScore = 0f;
            foreach (var candidate in aCandidates)
            {
                float score = Score(candidate.Definition, aRule);
                if (best.Definition == null || score > bestScore)
                {
                    best = candidate;
                    bestScore = score;
                }
            }
            return best;
        }

        // 選択規則に対する比較値を求める。小さいほうを選びたい場合は符号を反転して「大きいほうが良い」に揃える
        // aDefinition : 対象スキルの定義
        // aRule : 選択規則
        // return : 大きいほど優先される比較値
        private static float Score(PPSkillDefinition aDefinition, PPUnitAISkillSelectRule aRule)
            => aRule switch
            {
                PPUnitAISkillSelectRule.HighestAIScore => aDefinition.AIScore,
                PPUnitAISkillSelectRule.HighestGaugeCost => aDefinition.SkillGaugeCost,
                PPUnitAISkillSelectRule.LowestGaugeCost => -aDefinition.SkillGaugeCost,
                _ => 0f,
            };
    }
}
