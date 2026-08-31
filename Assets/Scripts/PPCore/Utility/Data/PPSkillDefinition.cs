/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file PPSkillDefinition.cs
 * @author hqrse
 * @date 2026/06/21
 * @brief スキル定義
 * =====================================*/

using System;
using System.Collections.Generic;
using CommandBattleCore;
using UnityEngine;
using AttributeUtility;

namespace PPCore
{
    // Project-Pudding 固有のスキル定義（ScriptableObject）
    // 汎用の SkillDefinition に対して、AI 用のスキルタグ・同種グループ・AI スコア・
    // 必要スキルゲージ量・スキルエフェクトの組み合わせを追加する
    // 効果本体は mSkillEffects（PPSkillEffectDefinition のインスタンス）を順に実行することで組み立てる
    [CreateAssetMenu(fileName = "PPSkillDefinition", menuName = "Project-Pudding/Skill/PPSkillDefinition")]
    public class PPSkillDefinition : SkillDefinition
    {
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

        public IReadOnlyList<PPSkillTagDefinition> Tags => mTags;
        public PPSkillGroup Group => mGroup;
        public PPSkillActionPriority ActionPriority => mActionPriority;
        public float AIScore => mAIScore;
        // 発動に必要なスキルゲージ量。負値が設定されていても 0 として扱う
        public float SkillGaugeCost => Mathf.Max(0f, mSkillGaugeCost);
        // コストを必要としないか
        public bool IsFreeCost => SkillGaugeCost <= 0f;

        // 指定タグのいずれかを持つかを判定する
        // aTags : 判定するタグ。null または空なら常に true（タグ指定なし＝全スキルが対象）
        // return : いずれかのタグを持つ場合 true
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
        // 効果量から行動を比較したい場合に使う
        // 発動者自身に掛かる効果（ApplyTarget = Self）は対象の状態と無関係なため合算しない
        // aSource : スキル発動者
        // aTarget : 対象。null なら効果なしを返す
        // aContext : バトルコンテキスト
        // return : 対象への効果量の見積もり
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

        // この定義からスキルのランタイムインスタンスを生成する
        // 生成物には自身への参照（SourceDefinition）を必ず設定する
        // AI やコマンドが SourceDefinition is PPSkillDefinition で
        // 必要スキルゲージ量を引くため、この設定を省略するとゲージ消費が働かなくなる
        // return : クールダウン・使用回数を初期化済みのランタイムスキル
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
        // return : 効果本体のデリゲート
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
    }
}
