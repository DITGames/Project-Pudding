/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file StatusEffect.cs
 * @author hqrse
 * @date 2026/06/13
 * @brief 状態異常定義
 * 防御状態や毒などステータスの異変はすべて定義をまとめる
 * =====================================*/

using System;
using System.Collections.Generic;

namespace CommandBattleCore
{
    /// <summary>
    /// ユニットに掛かる状態変化のランタイムインスタンス。
    /// <para>
    /// 毒などの状態異常も、バフ・デバフも、防御状態も、すべてこの 1 つの型で表現する。
    /// 表現手段は次の 3 つで、これらを組み合わせて個々のエフェクトを作る。
    /// 1. パラメータ修飾子（<see cref="AddModifier"/>）… 攻撃力低下などの数値変化
    /// 2. 行動制限（<see cref="Restriction"/>）… 麻痺・沈黙など
    /// 3. コールバック（<see cref="OnTick"/> / <see cref="ModifyIncomingDamage"/> など）… 毒ダメージ・ダメージ軽減など
    /// </para>
    /// <para>
    /// 「いつ切れるか」は <see cref="DurationCondition"/> へ、
    /// 「重ね掛けされたらどうなるか」は <see cref="StackPolicy"/> へ分離してある。
    /// </para>
    /// </summary>
    public class StatusEffect
    {
        /// <summary>エフェクト ID。同一 ID が重ね掛け判定の単位になる。</summary>
        public string EffectId { get; }
        /// <summary>UI 表示名。</summary>
        public string DisplayName { get; }
        /// <summary>このエフェクトが撒くパラメータ修飾子。<see cref="mModifierTargets"/> と同じ順で対応する。</summary>
        public List<ParameterModifier> Modifiers { get; } = new();
        /// <summary>同一 ID が重ねて付与されたときの挙動。</summary>
        public StatusEffectStackPolicy StackPolicy { get; set; } = StatusEffectStackPolicy.Stack;
        /// <summary>スタック数の上限。</summary>
        public int MaxStacks { get; set; } = 1;
        /// <summary>現在のスタック数。</summary>
        public int CurrentStacks { get; protected internal set; } = 1;
        /// <summary>効果が切れる条件。未指定なら永続。</summary>
        public IDurationCondition DurationCondition { get; set; }
        /// <summary>このエフェクトが課す行動制限。</summary>
        public ActionRestriction Restriction { get; set; } = ActionRestriction.None;
        /// <summary>
        /// 行動が失敗する確率（0～1）。
        /// null なら <see cref="ActionRestriction.CannotAct"/> 時に無条件で失敗する（睡眠など）。
        /// 値を持つ場合は確率判定になる（麻痺など）。
        /// </summary>
        public float? ActionFailChange {get; set; }

        /// <summary>被ダメージ前介入(対象ユニット, ダメージ情報)。ダメージ情報を書き換えて軽減・無効化する。</summary>
        public Action<BattleUnit, DamageInfo> ModifyIncomingDamage { get; set; }
        /// <summary>ステータスエフェクト更新(対象ユニット, コンテキスト)。毒の継続ダメージなどをここで行う。</summary>
        public Action<BattleUnit, BattleContext> OnTick { get; set; }
        /// <summary>ステータスエフェクトのスタック更新(対象ユニット, エフェクト)</summary>
        public Action<BattleUnit, StatusEffect> OnStackChanged { get; set; }
        /// <summary>ステータスエフェクト適用(対象ユニット)</summary>
        public Action<BattleUnit> OnApply { get; set; }
        /// <summary>ステータスエフェクト除去(対象ユニット)</summary>
        public Action<BattleUnit> OnRemove { get; set; }

        /// <param name="aEffectId">エフェクト ID。</param>
        /// <param name="aDisplayName">UI 表示名。</param>
        /// <param name="aDurationCondition">持続条件。null なら永続として扱う。</param>
        public StatusEffect(string aEffectId, string aDisplayName, IDurationCondition aDurationCondition = null)
        {
            EffectId = aEffectId;
            DisplayName = aDisplayName;
            DurationCondition = aDurationCondition ?? new PermanentDurationCondition();
        }

        /// <summary>
        /// このエフェクトが掛けるパラメータ修飾子を登録する。
        /// 修飾子の付与元には自身を設定するため、除去時に <see cref="RemoveFrom"/> でまとめて剥がせる。
        /// 呼び出しを繋げられるよう自身を返す。
        /// </summary>
        /// <param name="aParamId">修飾対象のパラメータ ID。</param>
        /// <param name="aModifierType">適用方式（加算・乗算・上書き）。</param>
        /// <param name="aValue">修飾値。</param>
        /// <param name="aPriority">Override 競合時の優先度。</param>
        /// <returns>メソッドチェーン用に自身を返す。</returns>
        public StatusEffect AddModifier(string aParamId, ParameterModifierType aModifierType, float aValue,
            int aPriority = 0)
        {
            Modifiers.Add(new ParameterModifier(aModifierType, this, aValue, aPriority));
            mModifierTargets.Add(aParamId);
            return this;
        }

        /// <summary>
        /// 各修飾子の適用先パラメータ ID。<see cref="Modifiers"/> と添字で対応するため、
        /// 追加は必ず <see cref="AddModifier"/> 経由で行い、両リストの並びを崩さないこと。
        /// </summary>
        protected readonly List<string> mModifierTargets = new();

        /// <summary>
        /// ユニットへ効果を適用する。登録済みの修飾子を対応するパラメータへ配り、適用コールバックを呼ぶ。
        /// 対象 ID のパラメータが存在しない場合、その修飾子は黙って読み飛ばされる。
        /// </summary>
        /// <param name="aUnit">適用先のユニット。</param>
        protected internal virtual void ApplyTo(BattleUnit aUnit)
        {
            for (int i = 0; i < Modifiers.Count; i++)
            {
                aUnit.Parameters.Get(mModifierTargets[i])?.AddModifier(Modifiers[i]);
            }
            OnApply?.Invoke(aUnit);
        }

        /// <summary>
        /// ユニットから効果を取り除く。自身を付与元とする修飾子を全パラメータから一括で剥がす。
        /// </summary>
        /// <param name="aUnit">除去先のユニット。</param>
        protected internal virtual void RemoveFrom(BattleUnit aUnit)
        {
            aUnit.Parameters.RemoveModifiersFromSource(this);
            OnRemove?.Invoke(aUnit);
        }
    }
}
