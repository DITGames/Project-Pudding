/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file PPBattleSkillStatusSource.cs
 * @author hqrse
 * @date 2026/06/30
 * @brief バトルスキル情報アダプタ
 * =====================================*/
using System;
using CommandBattleCore;

namespace PPCore
{
    /// <summary>
    /// <see cref="BattleSkill"/> を UI 向けの表示情報として見せるアダプタ。
    /// <para>
    /// 発動可否は自前で判定せず <see cref="ICastValidator"/> へ委ねるため、
    /// UI の表示とコマンド実行時の判定が食い違わない。
    /// </para>
    /// <para>
    /// 購読するのはこのスキルが実際に消費する属性のリソースだけ。
    /// 無関係な属性の増減で UI が再描画されるのを避けている。
    /// </para>
    /// </summary>
    public class PPBattleSkillStatusSource : IPPSkillStatusSource
    {
        /// <summary>表示対象のスキル。</summary>
        private readonly BattleSkill mSkill;
        /// <summary>このスキルを持つユニット。発動可否の判定に使う。</summary>
        private readonly BattleUnit mOwner;
        /// <summary>発動可否の判定に使うバトルコンテキスト。</summary>
        private readonly BattleContext mContext;
        /// <summary>リソース変化を購読する対象のパーティ。取得できなければ null。</summary>
        private readonly PPBattleParty mParty;
        /// <summary>表示内容が変化したときに発火する。</summary>
        public event Action Changed;

        /// <summary>UI 表示名。</summary>
        public string DisplayName => mSkill.DisplayName;
        /// <summary>消費リソース。定義を引けない場合は無コスト扱い。</summary>
        public PPResourceCost Cost => (mSkill.SourceDefinition as PPSkillDefinition)?.Cost ?? PPResourceCost.Free;
        /// <summary>残りクールダウンターン数。</summary>
        public int CooldownRemaining => mSkill.RemainingCooldown;
        /// <summary>今このスキルを発動できるか。判定はバリデータへ委譲する。</summary>
        public bool IsCastable => mContext.Rules.CastValidator.Validate(mOwner, mSkill, mContext).CanCast;

        /// <param name="aSkill">表示対象のスキル。</param>
        /// <param name="aOwner">このスキルを持つユニット。</param>
        /// <param name="aContext">バトルコンテキスト。</param>
        public PPBattleSkillStatusSource(BattleSkill aSkill, BattleUnit aOwner, BattleContext aContext)
        {
            mSkill = aSkill;
            mOwner = aOwner;
            mContext = aContext;

            mParty = aContext.GetParty(aOwner.Side) as PPBattleParty;
            if (mParty != null)
            {
                foreach(var t in Cost.RelevantTypes())
                {
                    mParty.ResourcePool.Pool(t).OnValueChanged += HandleChanged;
                }
            }
        }

        /// <summary>リソースの変化を自身のイベントとして中継する。</summary>
        private void HandleChanged(IReadableParameter _) => Changed?.Invoke();

        /// <summary>
        /// メニュー破棄時に呼び出す(購読によるメモリリーク防止用)。
        /// 購読時と同じ属性集合を辿って解除する。
        /// </summary>
        public void Dispose()
        {
            if (mParty != null)
            {
                foreach(var t in Cost.RelevantTypes())
                {
                    mParty.ResourcePool.Pool(t).OnValueChanged -= HandleChanged;
                }
            }
        }

    }
}
