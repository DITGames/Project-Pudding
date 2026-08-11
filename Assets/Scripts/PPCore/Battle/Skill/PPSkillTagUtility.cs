/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file PPSkillTagUtility.cs
 * @author hqrse
 * @date 2026/08/11
 * @brief スキルタグでスキルを絞り込むヘルパー
 * =====================================*/

using System.Collections.Generic;
using CommandBattleCore;

namespace PPCore
{
    // スキルタグによる絞り込みをまとめたヘルパー
    // 戦術ステップ（使うスキルの決定）とユニット条件（そのスキルを持っているか）の
    // 双方から同じ判定を使うため、片方に実装を寄せずここへ集約している
    public static class PPSkillTagUtility
    {
        // ユニットが持つスキルのうち、指定タグのいずれかに合致するものを列挙する
        // PP 側の定義を持たないスキルはタグを判定できないため対象外になる
        // aUnit : 対象ユニット
        // aTags : 絞り込みに使うタグ。null または空なら全スキルが対象
        // return : 合致したスキルと、その定義の組
        public static IEnumerable<(PPBattleSkill Skill, PPSkillDefinition Definition)> EnumerateTaggedSkills(
            PPBattleUnit aUnit, IReadOnlyList<PPSkillTagDefinition> aTags)
        {
            if (aUnit == null) yield break;

            foreach (var skill in aUnit.Skills)
            {
                if (skill is not PPBattleSkill ppSkill) continue;
                if (ppSkill.SourceDefinition is not PPSkillDefinition def) continue;
                if (!def.HasAnyTag(aTags)) continue;

                yield return (ppSkill, def);
            }
        }

        // ユニットが指定タグのスキルを 1 つでも持っているかを判定する
        // 発動可否は見ないため、単に所持しているかどうかの判定になる
        // aUnit : 対象ユニット
        // aTags : 絞り込みに使うタグ。null または空なら「スキルを 1 つでも持っているか」になる
        // return : 該当するスキルがあれば true
        public static bool HasTaggedSkill(PPBattleUnit aUnit, IReadOnlyList<PPSkillTagDefinition> aTags)
        {
            foreach (var _ in EnumerateTaggedSkills(aUnit, aTags))
            {
                return true;
            }
            return false;
        }

        // ユニットが指定タグのスキルを今すぐ発動できるかを判定する
        // クールダウン・使用回数・リソース残量まで含めた判定になる
        // aUnit : 対象ユニット
        // aTags : 絞り込みに使うタグ。null または空なら全スキルが対象
        // aContext : 発動可否の検証に使うバトルコンテキスト
        // return : 発動可能なスキルがあれば true
        public static bool HasCastableTaggedSkill(PPBattleUnit aUnit, IReadOnlyList<PPSkillTagDefinition> aTags,
            BattleContext aContext)
        {
            foreach (var (skill, _) in EnumerateTaggedSkills(aUnit, aTags))
            {
                if (aContext.Rules.CastValidator.Validate(aUnit, skill, aContext).CanCast)
                    return true;
            }
            return false;
        }

        // タグリストを説明文用の文字列へ整形する
        // aTags : 整形するタグ
        // return : "／" 区切りのタグ名。空なら「指定なし」
        public static string ToDisplayString(IReadOnlyList<PPSkillTagDefinition> aTags)
        {
            if (aTags == null || aTags.Count == 0) return "指定なし";

            var names = new List<string>();
            foreach (var tag in aTags)
            {
                if (tag != null) names.Add(tag.TagName);
            }
            return names.Count == 0 ? "指定なし" : string.Join("／", names);
        }
    }
}
