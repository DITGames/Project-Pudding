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
    /// <summary>
    /// スキル発動時にエフェクトを誰に付与するか。
    /// </summary>
    public enum PPEffectApplyTarget
    {
        /// <summary>スキルの対象に付与する（デバフ・状態異常など）。</summary>
        [InspectorName("対象")]
        Target,
        /// <summary>発動者自身に付与する（自己バフなど）。</summary>
        [InspectorName("発動者")]
        Self,
    }

    /// <summary>
    /// 付与するエフェクトと、その適用対象の組。インスペクタで配列として設定する。
    /// </summary>
    public struct PPSkillEffectEntry
    {
        /// <summary>付与するエフェクトの定義アセット。</summary>
        [Label("エフェクト")]
        public PPEffectDefinition Effect;
        /// <summary>このエフェクトを誰に付与するか。</summary>
        [Label("対象")]
        public PPEffectApplyTarget ApplyTarget;
    }

    /// <summary>
    /// Project-Pudding 固有のスキル定義（ScriptableObject）の抽象基底。
    /// <para>
    /// 汎用の <see cref="SkillDefinition"/> に対して、属性・種別・AI 用のスキルロール・
    /// 消費リソース・付与エフェクトを追加する。
    /// 効果本体の組み立て（<c>BuildEffect()</c>）は
    /// <see cref="PPAttackSkillDefinition"/> / <see cref="PPHealSkillDefinition"/> /
    /// <see cref="PPEffectCureSkillDefinition"/> といった派生側が実装する。
    /// </para>
    /// </summary>
    public abstract class PPSkillDefinition : SkillDefinition
    {
        /// <summary>AI が行動候補を分類するためのスキルロール（攻撃・回復・補助など）。</summary>
        [Header("拡張")]
        [Label("スキルタイプ")]
        [SerializeField]protected PPBattleSkillRole mBattleSkillRole;
        /// <summary>物理か魔法かの種別。ダメージ計算に使う参照パラメータが変わる。</summary>
        [Label("種別")]
        [SerializeField]protected PPSkillCategory mCategory = PPSkillCategory.Physical;
        /// <summary>スキルの属性。相手の属性との相性でダメージ倍率が変わる。</summary>
        [Label("属性")]
        [SerializeField]protected PPTypeAttribute mAttribute = PPTypeAttribute.Normal;
        /// <summary>属性ごとの消費リソース量。インスペクタ設定用の生データ。</summary>
        [Label("消費リソース")]
        [SerializeField] protected PPResourceAmount[] mCost;
        /// <summary><see cref="mCost"/> から一度だけ構築するコストのキャッシュ。</summary>
        private PPResourceCost mCachedCost;

        /// <summary>このスキルが付与するエフェクトと適用対象の一覧。</summary>
        [Header("エフェクト")]
        [Label("付与するエフェクト")]
        [SerializeField]protected PPSkillEffectEntry[] mEffectEntries;

        /// <summary>スキルの威力。ダメージ・回復量の基礎値。</summary>
        public float Power => mPower;
        /// <summary>AI 用のスキルロール。</summary>
        public PPBattleSkillRole BattleSkillRole => mBattleSkillRole;
        /// <summary>物理／魔法の種別。</summary>
        public PPSkillCategory Category => mCategory;
        /// <summary>スキルの属性。</summary>
        public PPTypeAttribute Attribute => mAttribute;
        /// <summary>消費リソース。初回アクセス時に構築してキャッシュする。</summary>
        public PPResourceCost Cost => mCachedCost ??= PPResourceCost.From(mCost);

        /// <summary>
        /// この定義からスキルのランタイムインスタンスを生成する。
        /// 生成物には自身への参照（SourceDefinition）を必ず設定する。
        /// AI やコマンドが <c>SourceDefinition is PPSkillDefinition</c> で
        /// コストや属性を引くため、この設定を省略するとリソース消費が働かなくなる。
        /// </summary>
        /// <returns>クールダウン・使用回数を初期化済みのランタイムスキル。</returns>
        public override BattleSkill CreateRuntimeSkill()
        {
            var skill = new PPBattleSkill(mSkillId, mDisplayName, mTargetScope.CreateResolver(), BuildEffect());
            skill.SourceDefinition = this;
            skill.MaxCooldown = mMaxCooldown;
            skill.MaxUsesPerBattle = mMaxUsesPerBattle;
            skill.ResetForBattle();
            return skill;
        }

        /// <summary>
        /// 派生クラスの効果本体に、<see cref="mEffectEntries"/> のエフェクト付与を後段として繋げた
        /// デリゲートを組み立てる。
        /// </summary>
        /// <remarks>
        /// 既知の未整理箇所: 現状 <see cref="CreateRuntimeSkill"/> は <c>BuildEffect()</c> を直接使っており
        /// このメソッドを呼んでいないため、インスペクタで設定したエフェクト付与は実際には走らない。
        /// </remarks>
        /// <returns>効果本体とエフェクト付与を続けて実行するデリゲート。</returns>
        private Action<BattleUnit, List<BattleUnit>, BattleContext> BuildEffectWithEntries()
        {
            var mainEffect = BuildEffect();
            return (src, targets, ctx) =>
            {
                mainEffect?.Invoke(src, targets, ctx);
                ApplyEffectEntries(src, targets, ctx);
            };
        }

        /// <summary>
        /// <see cref="mEffectEntries"/> の各エントリを、適用対象に応じて発動者または全対象へ付与する。
        /// エフェクトのランタイム実体はエントリごと・対象ごとに個別生成する。
        /// </summary>
        /// <param name="aSource">スキルの発動者。</param>
        /// <param name="aTargets">解決済みの対象リスト。</param>
        /// <param name="aContext">バトルコンテキスト。</param>
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
                    aSource.AddStatusEffect(entry.Effect.CreateRuntimeStatusEffect(aSource, aSource, aContext));
                    continue;
                }

                foreach (var tgt in aTargets)
                {
                    tgt.AddStatusEffect(entry.Effect.CreateRuntimeStatusEffect(aSource, tgt, aContext));
                }
            }
        }
    }
}
