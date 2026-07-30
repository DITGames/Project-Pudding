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
    // ユニットに掛かる状態変化のランタイムインスタンス
    // 毒などの状態異常も、バフ・デバフも、防御状態も、すべてこの 1 つの型で表現する
    // 表現手段は次の 3 つで、これらを組み合わせて個々のエフェクトを作る
    // 1. パラメータ修飾子（AddModifier）… 攻撃力低下などの数値変化
    // 2. 行動制限（Restriction）… 麻痺・沈黙など
    // 3. コールバック（OnTick / ModifyIncomingDamage など）… 毒ダメージ・ダメージ軽減など
    // 「いつ切れるか」は DurationCondition へ、「重ね掛けされたらどうなるか」は StackPolicy へ分離してある
    public class StatusEffect
    {
        // エフェクト ID。同一 ID が重ね掛け判定の単位になる
        public string EffectId { get; }
        // UI 表示名
        public string DisplayName { get; }
        // このエフェクトが撒くパラメータ修飾子。mModifierTargets と同じ順で対応する
        public List<ParameterModifier> Modifiers { get; } = new();
        // 同一 ID が重ねて付与されたときの挙動
        public StatusEffectStackPolicy StackPolicy { get; set; } = StatusEffectStackPolicy.Stack;
        // スタック数の上限
        public int MaxStacks { get; set; } = 1;
        // 現在のスタック数
        public int CurrentStacks { get; protected internal set; } = 1;
        // 効果が切れる条件。未指定なら永続
        public IDurationCondition DurationCondition { get; set; }
        // このエフェクトが課す行動制限
        public ActionRestriction Restriction { get; set; } = ActionRestriction.None;
        // 行動が失敗する確率（0～1）
        // null なら ActionRestriction.CannotAct 時に無条件で失敗する（睡眠など）
        // 値を持つ場合は確率判定になる（麻痺など）
        public float? ActionFailChange {get; set; }

        // 被ダメージ前介入(対象ユニット, ダメージ情報)。ダメージ情報を書き換えて軽減・無効化する
        public Action<BattleUnit, DamageInfo> ModifyIncomingDamage { get; set; }
        // ステータスエフェクト更新(対象ユニット, コンテキスト)。毒の継続ダメージなどをここで行う
        public Action<BattleUnit, BattleContext> OnTick { get; set; }
        // ステータスエフェクトのスタック更新(対象ユニット, エフェクト)
        public Action<BattleUnit, StatusEffect> OnStackChanged { get; set; }
        // ステータスエフェクト適用(対象ユニット)
        public Action<BattleUnit> OnApply { get; set; }
        // ステータスエフェクト除去(対象ユニット)
        public Action<BattleUnit> OnRemove { get; set; }

        // aEffectId : エフェクト ID
        // aDisplayName : UI 表示名
        // aDurationCondition : 持続条件。null なら永続として扱う
        public StatusEffect(string aEffectId, string aDisplayName, IDurationCondition aDurationCondition = null)
        {
            EffectId = aEffectId;
            DisplayName = aDisplayName;
            DurationCondition = aDurationCondition ?? new PermanentDurationCondition();
        }

        // このエフェクトが掛けるパラメータ修飾子を登録する
        // 修飾子の付与元には自身を設定するため、除去時に RemoveFrom でまとめて剥がせる
        // 呼び出しを繋げられるよう自身を返す
        // aParamId : 修飾対象のパラメータ ID
        // aModifierType : 適用方式（加算・乗算・上書き）
        // aValue : 修飾値
        // aPriority : Override 競合時の優先度
        // return : メソッドチェーン用に自身を返す
        public StatusEffect AddModifier(string aParamId, ParameterModifierType aModifierType, float aValue,
            int aPriority = 0)
        {
            Modifiers.Add(new ParameterModifier(aModifierType, this, aValue, aPriority));
            mModifierTargets.Add(aParamId);
            return this;
        }

        // 各修飾子の適用先パラメータ ID。Modifiers と添字で対応するため、
        // 追加は必ず AddModifier 経由で行い、両リストの並びを崩さないこと
        protected readonly List<string> mModifierTargets = new();

        // ユニットへ効果を適用する。登録済みの修飾子を対応するパラメータへ配り、適用コールバックを呼ぶ
        // 対象 ID のパラメータが存在しない場合、その修飾子は黙って読み飛ばされる
        // aUnit : 適用先のユニット
        protected internal virtual void ApplyTo(BattleUnit aUnit)
        {
            for (int i = 0; i < Modifiers.Count; i++)
            {
                aUnit.Parameters.Get(mModifierTargets[i])?.AddModifier(Modifiers[i]);
            }
            OnApply?.Invoke(aUnit);
        }

        // ユニットから効果を取り除く。自身を付与元とする修飾子を全パラメータから一括で剥がす
        // aUnit : 除去先のユニット
        protected internal virtual void RemoveFrom(BattleUnit aUnit)
        {
            aUnit.Parameters.RemoveModifiersFromSource(this);
            OnRemove?.Invoke(aUnit);
        }
    }
}
