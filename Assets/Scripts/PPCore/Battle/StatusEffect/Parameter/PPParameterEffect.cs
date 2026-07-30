/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file PPParameterEffect.cs
 * @author hqrse
 * @date 2026/07/27
 * @brief PPCore固有のパラメータエフェクト
 * =====================================*/
using System;
using CommandBattleCore;
using UnityEngine;

namespace PPCore
{
    /// <summary>
    /// パラメータ変動エフェクトの種別。
    /// 対象パラメータとバフ／デバフの組み合わせごとにビットを割り当ててあり、
    /// 「攻撃デバフだけ解除」のような選択的な解除ができる。
    /// </summary>
    [Flags]
    public enum PPParameterEffectCategory : long
    {
        /// <summary>種別なし。</summary>
        [InspectorName("なし")]
        None = 0,
        /// <summary>攻撃力上昇。</summary>
        [InspectorName(PPParameterEffectCategoryDefinition.NameAttack + "バフ")]
        AttackBuff = 1L << 0,
        /// <summary>攻撃力低下。</summary>
        [InspectorName(PPParameterEffectCategoryDefinition.NameAttack + "デバフ")]
        AttackDebuff =  1L << 1,
        /// <summary>防御力上昇。</summary>
        [InspectorName(PPParameterEffectCategoryDefinition.NameDefense + "バフ")]
        DefenseBuff = 1L << 2,
        /// <summary>防御力低下。</summary>
        [InspectorName(PPParameterEffectCategoryDefinition.NameDefense + "デバフ")]
        DefenseDebuff =  1L << 3,
        /// <summary>素早さ上昇。</summary>
        [InspectorName(PPParameterEffectCategoryDefinition.NameSpeed + "バフ")]
        SpeedBuff =  1L << 4,
        /// <summary>素早さ低下。</summary>
        [InspectorName(PPParameterEffectCategoryDefinition.NameSpeed + "デバフ")]
        SpeedDebuff =  1L << 5,
        /// <summary>最大HP上昇。</summary>
        [InspectorName(PPParameterEffectCategoryDefinition.NameHp + "バフ")]
        HpBuff = 1L << 6,
        /// <summary>最大HP低下。</summary>
        [InspectorName(PPParameterEffectCategoryDefinition.NameHp + "デバフ")]
        HpDebuff =  1L << 7,
        /// <summary>攻撃コスト軽減。</summary>
        [InspectorName(PPParameterEffectCategoryDefinition.NameCost + "バフ")]
        CostBuff =  1L << 8,
        /// <summary>攻撃コスト増加。</summary>
        [InspectorName(PPParameterEffectCategoryDefinition.NameCost + "デバフ")]
        CostDebuff =  1L << 9,
    }

    /// <summary>
    /// パラメータ種別の日本語表示名を集約した定数群。
    /// 種別 enum とパラメータ指定 enum の双方から参照され、表記を揃えている。
    /// </summary>
    public static class PPParameterEffectCategoryDefinition
    {
        /// <summary>攻撃力の表示名。</summary>
        public const string NameAttack = "攻撃";
        /// <summary>防御力の表示名。</summary>
        public const string NameDefense = "防御";
        /// <summary>素早さの表示名。</summary>
        public const string NameSpeed = "素早さ";
        /// <summary>HP の表示名。</summary>
        public const string NameHp = "HP";
        /// <summary>コストの表示名。</summary>
        public const string NameCost = "コスト";
    }

    /// <summary>
    /// パラメータを増減させるバフ・デバフのランタイム実体。
    /// <para>
    /// 基底の <see cref="StatusEffect"/> は基本パラメータしか見ないが、
    /// 本作は攻撃コストなどを <see cref="PPBattleUnit.ExtraParameters"/> に持つ。
    /// そのため適用・除去を上書きし、基本パラメータと追加パラメータの
    /// 両方を対象にできるようにしている。
    /// </para>
    /// </summary>
    public class PPParameterEffect : StatusEffect
    {
        /// <summary>このエフェクトの種別。解除スキルの対象判定に使う。</summary>
        public PPParameterEffectCategory Category { get; set; } = PPParameterEffectCategory.None;

        /// <param name="aEffectId">エフェクトID。</param>
        /// <param name="aDisplayName">UI表示名。</param>
        /// <param name="aDurationCondition">持続条件。null なら永続。</param>
        public PPParameterEffect(string aEffectId, string aDisplayName, IDurationCondition aDurationCondition = null)
            : base(aEffectId, aDisplayName, aDurationCondition)
        {

        }

        /// <summary>
        /// 修飾子を適用する。
        /// 基底と違い <see cref="PPBattleUnit.ResolveParameter"/> を通すため、
        /// 基本パラメータと追加パラメータのどちらも修飾対象にできる。
        /// 対象が <see cref="PPBattleUnit"/> でない場合は修飾子を適用せず通知だけ行う。
        /// </summary>
        /// <param name="aUnit">適用先のユニット。</param>
        protected internal override void ApplyTo(BattleUnit aUnit)
        {
            if (aUnit is PPBattleUnit ppUnit)
            {
                for (int i = 0; i < Modifiers.Count; i++)
                {
                    var param = ppUnit.ResolveParameter(mModifierTargets[i]);
                    param?.AddModifier(Modifiers[i]);
                }
            }
            OnApply?.Invoke(aUnit);
        }

        /// <summary>
        /// 修飾子を除去する。適用先が 2 系統ありうるため、
        /// 基本パラメータと追加パラメータの両方から自身を付与元とする修飾子を剥がす。
        /// </summary>
        /// <param name="aUnit">除去先のユニット。</param>
        protected internal override void RemoveFrom(BattleUnit aUnit)
        {
            aUnit.Parameters.RemoveModifiersFromSource(this);
            if (aUnit is PPBattleUnit ppUnit)
            {
                ppUnit.ExtraParameters.RemoveModifiesFromSource(this);
            }
            OnRemove?.Invoke(aUnit);
        }
    }
}
