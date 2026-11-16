/* =====================================
 * Copyright WabisabiAndons. All rights reserved.
 * @file PPSkillTagDefinition.cs
 * @author hqrse
 * @date 2026/08/11
 * @brief スキルタグの定義とユーティリティ
 * =====================================*/

using System.Collections.Generic;
using UnityEngine;
using AttributeUtility;

namespace PPCore
{
    // スキルを分類するタグ（ScriptableObject）。AI 条件がスキルを指すための目印になる
    // ピッカーのツリー構造は、アセットの置き場ではなくカテゴリパスから組み立てる
    [CreateAssetMenu(fileName = "PPSkillTagDefinition", menuName = "Project-Pudding/AI/PPSkillTagDefinition")]
    public class PPSkillTagDefinition : ScriptableObject
    {
        [Label("タグ名")]
        [SerializeField] private string mTagName = "";
        // "攻撃/範囲" のようなスラッシュ区切りのパス。ピッカーのツリー構造はこの値から組む
        [Label("カテゴリパス")]
        [SerializeField] private string mCategoryPath = "";
        [Label("説明")]
        [SerializeField][Multiline] private string mDescription = "";

        // 表示に使うタグ名。未入力ならアセット名で代用する
        public string TagName => string.IsNullOrEmpty(mTagName) ? name : mTagName;
        public string CategoryPath => mCategoryPath;
        public string Description => mDescription;

        // ツリー上でのフルパス。カテゴリパス未設定のタグは「未分類」へ集める
        public string MenuPath
            => string.IsNullOrEmpty(mCategoryPath) ? $"未分類/{TagName}" : $"{mCategoryPath}/{TagName}";
    }

    // スキルタグによる絞り込みをまとめたヘルパー。複数のユニット条件から使う
    public static class PPSkillTagUtility
    {
        // ユニットが持つスキルのうち、指定タグのいずれかに合致するものを列挙する
        // PP 側の定義を持たないスキルはタグを判定できないため対象外になる
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
        public static bool HasTaggedSkill(PPBattleUnit aUnit, IReadOnlyList<PPSkillTagDefinition> aTags)
        {
            foreach (var _ in EnumerateTaggedSkills(aUnit, aTags))
            {
                return true;
            }
            return false;
        }

        // ユニットが指定タグのスキルを今すぐ発動できるかを判定する
        // クールダウン・使用回数・スキルゲージ残量まで含めた判定になる
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
