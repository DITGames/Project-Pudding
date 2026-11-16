/* =====================================
 * Copyright WabisabiAndons. All rights reserved.
 * @file PPSkillDefinition.cs
 * @author hqrse
 * @date 2026/06/21
 * @brief スキル定義
 * =====================================*/

using System;
using System.Collections.Generic;
using UnityEngine;
using AttributeUtility;

namespace PPCore
{
    // スキル定義（ScriptableObject）。スキルタグ・同種グループ・AI スコア・必要スキルゲージ量・スキルエフェクトを持つ
    // 効果本体は mSkillEffects を順に実行することで組み立てる
    [CreateAssetMenu(fileName = "PPSkillDefinition", menuName = "Project-Pudding/Skill/PPSkillDefinition")]
    public class PPSkillDefinition : SkillDefinition
    {
        /* インスペクタ設定 */

        // AI の条件判定がこのスキルを指すための分類タグ
        [Header("拡張")]
        [Label("スキルタグ", true)]
        [SerializeField]protected List<PPSkillTagDefinition> mTags = new();

        // 同種グループ。AI は同じグループ内で「今撃つか、上位を待つか」を比較する
        [Label("同種グループ")]
        [SerializeField] protected PPSkillGroup mGroup = PPSkillGroup.Attack;
        // 1 ティック内で行動を並べるときの優先度。同じ優先度どうしは速度で順序が決まる
        [Label("行動優先度")]
        [SerializeField] protected PPSkillActionPriority mActionPriority = PPSkillActionPriority.Normal;
        // 発動に必要なスキルゲージ量。発動者自身のスキルゲージから支払う
        [Label("必要スキルゲージ量")]
        [SerializeField] protected float mSkillGaugeCost = 0f;

        // このスキルが発動時に実行するスキルエフェクトの一覧。登録順に実行される
        [Label("効果", true)]
        [SerializeReference]
        [SerializeField] protected List<PPSkillEffectDefinition> mSkillEffects = new();

        // AI がスキルを比較する際の基礎スコア
        // 同種グループ内で強弱を比べる際の基準になる
        [Label("AIスコア")]
        [SerializeField]protected float mAIScore = 1f;

        /* プロパティ */

        public IReadOnlyList<PPSkillTagDefinition> Tags => mTags;
        public PPSkillGroup Group => mGroup;
        public PPSkillActionPriority ActionPriority => mActionPriority;
        public float AIScore => mAIScore;
        // 発動に必要なスキルゲージ量。負値が設定されていても 0 として扱う
        public float SkillGaugeCost => Mathf.Max(0f, mSkillGaugeCost);
        // コストを必要としないか
        public bool IsFreeCost => SkillGaugeCost <= 0f;

        /* 生成 */

        // この定義からスキルのランタイムインスタンスを生成する
        // 生成物には自身への参照（SourceDefinition）を必ず設定する（ゲージ消費がこれを参照する）
        public override BattleSkill CreateRuntimeSkill()
        {
            var skill = new PPBattleSkill(mSkillId, mDisplayName, mTargetScope.CreateResolver(), BuildEffect());
            skill.SourceDefinition = this;
            skill.MaxCooldown = mMaxCooldown;
            skill.MaxUsesPerBattle = mMaxUsesPerBattle;
            skill.ResetForBattle();
            return skill;
        }

        // mSkillEffects を登録順に実行するデリゲートを組み立てる
        // 各エフェクトは自身の ApplyTarget に従い、発動者自身または対象リスト全員に適用される
        private Action<BattleUnit, List<BattleUnit>, BattleContext> BuildEffect()
        {
            return (src, targets, ctx) =>
            {
                if (mSkillEffects == null) return;

                foreach (var effect in mSkillEffects)
                {
                    if (effect == null) continue;

                    // 発動者向けは対象リストを走査せず 1 回だけ適用する
                    if (effect.ApplyTarget == PPEffectApplyTarget.Self)
                    {
                        effect.Apply(src, src, this, ctx);
                        continue;
                    }

                    foreach (var tgt in targets)
                    {
                        effect.Apply(src, tgt, this, ctx);
                    }
                }
            };
        }

        /* 問い合わせ */

        // 指定タグのいずれかを持つかを判定する
        public bool HasAnyTag(IReadOnlyList<PPSkillTagDefinition> aTags)
        {
            if (aTags == null || aTags.Count == 0) return true;

            foreach (var tag in aTags)
            {
                if (tag != null && mTags.Contains(tag)) return true;
            }
            return false;
        }

        // このスキルを aTarget へ撃った場合の効果量を、実行せずに見積もる
        // 発動者自身に掛かる効果（ApplyTarget = Self）は合算しない
        public PPEffectEstimate EstimateFor(BattleUnit aSource, BattleUnit aTarget, BattleContext aContext)
        {
            var result = PPEffectEstimate.None;
            if (aTarget == null || mSkillEffects == null)
                return result;

            foreach (var effect in mSkillEffects)
            {
                if (effect == null || effect.ApplyTarget != PPEffectApplyTarget.Target)
                    continue;

                result = result.Merge(effect.Estimate(aSource, aTarget, aContext));
            }
            return result;
        }
    }
}
