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

namespace PPCore
{
    // Project-Pudding 固有のスキル定義（ScriptableObject）
    // 汎用の SkillDefinition に対して、AI 用のスキルロール・主分類・AI スコア・
    // 消費リソース・スキルエフェクトの組み合わせを追加する
    // 効果本体は mSkillEffects（PPSkillEffectDefinition のインスタンス）を順に実行することで組み立てる
    [CreateAssetMenu(fileName = "PPSkillDefinition", menuName = "Project-Pudding/Skill/PPSkillDefinition")]
    public class PPSkillDefinition : SkillDefinition
    {
        // AI が行動候補を分類するためのスキルロール（攻撃・回復・補助など）
        [Header("拡張")]
        [Label("スキルタイプ")]
        [SerializeField]protected PPBattleSkillRole mBattleSkillRole;
        
        [Label("コスト", true)]
        [SerializeField] protected PPResourceAmount[] mCost;
        // mCost から一度だけ構築するコストのキャッシュ
        private PPResourceCost mCachedCost;

        // このスキルが発動時に実行するスキルエフェクトの一覧。登録順に実行される
        [Label("効果", true)]
        [SerializeReference]
        [SerializeField] protected List<PPSkillEffectDefinition> mSkillEffects = new();

        // AI がスキルのスコアリングに使う値。ロールごとに個別のスコアを持つ
        // チェックされているロール（mBattleSkillRole）の数だけ入力欄が現れる
        [Label("ロール別AIスコア")]
        [SerializeField]protected PPSkillRoleScoreList mRoleScores = new();

        public PPBattleSkillRole BattleSkillRole => mBattleSkillRole;
        public PPSkillRoleScoreList RoleScores => mRoleScores;
        // 消費リソース。初回アクセス時に構築してキャッシュする
        public PPResourceCost Cost => mCachedCost ??= PPResourceCost.From(mCost);

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
