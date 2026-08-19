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
    // 汎用の SkillDefinition に対して、AI 用のスキルタグ・AI スコア・
    // 消費リソース・スキルエフェクトの組み合わせを追加する
    // 効果本体は mSkillEffects（PPSkillEffectDefinition のインスタンス）を順に実行することで組み立てる
    [CreateAssetMenu(fileName = "PPSkillDefinition", menuName = "Project-Pudding/Skill/PPSkillDefinition")]
    public class PPSkillDefinition : SkillDefinition
    {
        // 戦術ステップがこのスキルを指すための分類タグ
        [Header("拡張")]
        [Label("スキルタグ", true)]
        [SerializeField]protected List<PPSkillTagDefinition> mTags = new();

        [Label("コスト", true)]
        [SerializeField] protected PPResourceAmount[] mCost;
        // mCost から一度だけ構築するコストのキャッシュ
        private PPResourceCost mCachedCost;

        // このスキルが発動時に実行するスキルエフェクトの一覧。登録順に実行される
        [Label("効果", true)]
        [SerializeReference]
        [SerializeField] protected List<PPSkillEffectDefinition> mSkillEffects = new();

        // AI がスキルを比較する際の基礎スコア
        // 戦術のステップで同じタグのスキルが複数マッチしたときの優劣に使う
        [Label("AIスコア")]
        [SerializeField]protected float mAIScore = 1f;

        public IReadOnlyList<PPSkillTagDefinition> Tags => mTags;
        public float AIScore => mAIScore;
        // 消費リソース。初回アクセス時に構築してキャッシュする
        public PPResourceCost Cost => mCachedCost ??= PPResourceCost.From(mCost);

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
        // 効果量から行動を比較したい場合に使う。現状の戦術 AI からは呼ばれていない
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
        // コストを引くため、この設定を省略するとリソース消費が働かなくなる
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
