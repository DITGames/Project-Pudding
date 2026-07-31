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
    // スキル発動時にエフェクトを誰に付与するか
    public enum PPEffectApplyTarget
    {
        // スキルの対象に付与する（デバフ・状態異常など）
        [InspectorName("対象")]
        Target,
        // 発動者自身に付与する（自己バフなど）
        [InspectorName("発動者")]
        Self,
    }

    // 付与するエフェクトと、その適用対象の組。インスペクタで配列として設定する
    public struct PPSkillEffectEntry
    {
        [Label("エフェクト")]
        public PPEffectDefinition Effect;
        [Label("対象")]
        public PPEffectApplyTarget ApplyTarget;
    }

    // Project-Pudding 固有のスキル定義（ScriptableObject）の抽象基底
    // 汎用の SkillDefinition に対して、属性・種別・AI 用のスキルロール・
    // 消費リソース・付与エフェクトを追加する
    // 効果本体の組み立て（BuildEffect()）は
    // PPAttackSkillDefinition / PPHealSkillDefinition / PPEffectCureSkillDefinition
    // といった派生側が実装する
    public abstract class PPSkillDefinition : SkillDefinition
    {
        // AI が行動候補を分類するためのスキルロール（攻撃・回復・補助など）
        [Header("拡張")]
        [Label("スキルタイプ")]
        [SerializeField]protected PPBattleSkillRole mBattleSkillRole;
        // 物理か魔法かの種別。ダメージ計算に使う参照パラメータが変わる
        [Label("種別")]
        [SerializeField]protected PPSkillCategory mCategory = PPSkillCategory.Physical;
        // スキルの属性。相手の属性との相性でダメージ倍率が変わる
        [Label("属性")]
        [SerializeField]protected PPTypeAttribute mAttribute = PPTypeAttribute.Normal;
        [Label("消費リソース")]
        [SerializeField] protected PPResourceAmount[] mCost;
        // mCost から一度だけ構築するコストのキャッシュ
        private PPResourceCost mCachedCost;

        // このスキルが付与するエフェクトと適用対象の一覧
        [Header("エフェクト")]
        [Label("付与するエフェクト")]
        [SerializeField]protected PPSkillEffectEntry[] mEffectEntries;

        // スキルの威力。ダメージ・回復量の基礎値
        public float Power => mPower;
        public PPBattleSkillRole BattleSkillRole => mBattleSkillRole;
        public PPSkillCategory Category => mCategory;
        public PPTypeAttribute Attribute => mAttribute;
        // 消費リソース。初回アクセス時に構築してキャッシュする
        public PPResourceCost Cost => mCachedCost ??= PPResourceCost.From(mCost);

        // この定義からスキルのランタイムインスタンスを生成する
        // 生成物には自身への参照（SourceDefinition）を必ず設定する
        // AI やコマンドが SourceDefinition is PPSkillDefinition で
        // コストや属性を引くため、この設定を省略するとリソース消費が働かなくなる
        // return : クールダウン・使用回数を初期化済みのランタイムスキル
        public override BattleSkill CreateRuntimeSkill()
        {
            var skill = new PPBattleSkill(mSkillId, mDisplayName, mTargetScope.CreateResolver(), BuildEffectWithEntries());
            skill.SourceDefinition = this;
            skill.MaxCooldown = mMaxCooldown;
            skill.MaxUsesPerBattle = mMaxUsesPerBattle;
            skill.ResetForBattle();
            return skill;
        }

        // 派生クラスの効果本体に、mEffectEntries のエフェクト付与を後段として繋げたデリゲートを組み立てる
        // return : 効果本体とエフェクト付与を続けて実行するデリゲート
        private Action<BattleUnit, List<BattleUnit>, BattleContext> BuildEffectWithEntries()
        {
            var mainEffect = BuildEffect();
            return (src, targets, ctx) =>
            {
                mainEffect?.Invoke(src, targets, ctx);
                ApplyEffectEntries(src, targets, ctx);
            };
        }

        // mEffectEntries の各エントリを、適用対象に応じて発動者または全対象へ付与する
        // エフェクトのランタイム実体はエントリごと・対象ごとに個別生成する
        // aSource : スキルの発動者
        // aTargets : 解決済みの対象リスト
        // aContext : バトルコンテキスト
        private void ApplyEffectEntries(BattleUnit aSource, List<BattleUnit> aTargets, BattleContext aContext)
        {
            if(mEffectEntries == null)
                return;

            foreach (var entry in mEffectEntries)
            {
                if(entry.Effect == null)
                    continue;

                // 発動者向けは対象リストを走査せず 1 回だけ付与する
                if (entry.ApplyTarget == PPEffectApplyTarget.Self)
                {
                    aSource.AddStatusEffect(entry.Effect.CreateRuntimeStatusEffect(aSource, aSource, aContext), aContext);
                    continue;
                }

                foreach (var tgt in aTargets)
                {
                    tgt.AddStatusEffect(entry.Effect.CreateRuntimeStatusEffect(aSource, tgt, aContext), aContext);
                }
            }
        }
    }
}
