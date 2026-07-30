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
    // パラメータ変動エフェクトの種別
    // 対象パラメータとバフ／デバフの組み合わせごとにビットを割り当ててあり、
    // 「攻撃デバフだけ解除」のような選択的な解除ができる
    [Flags]
    public enum PPParameterEffectCategory : long
    {
        // 種別なし
        [InspectorName("なし")]
        None = 0,
        // 攻撃力上昇
        [InspectorName(PPParameterEffectCategoryDefinition.NameAttack + "バフ")]
        AttackBuff = 1L << 0,
        // 攻撃力低下
        [InspectorName(PPParameterEffectCategoryDefinition.NameAttack + "デバフ")]
        AttackDebuff =  1L << 1,
        // 防御力上昇
        [InspectorName(PPParameterEffectCategoryDefinition.NameDefense + "バフ")]
        DefenseBuff = 1L << 2,
        // 防御力低下
        [InspectorName(PPParameterEffectCategoryDefinition.NameDefense + "デバフ")]
        DefenseDebuff =  1L << 3,
        // 素早さ上昇
        [InspectorName(PPParameterEffectCategoryDefinition.NameSpeed + "バフ")]
        SpeedBuff =  1L << 4,
        // 素早さ低下
        [InspectorName(PPParameterEffectCategoryDefinition.NameSpeed + "デバフ")]
        SpeedDebuff =  1L << 5,
        // 最大HP上昇
        [InspectorName(PPParameterEffectCategoryDefinition.NameHp + "バフ")]
        HpBuff = 1L << 6,
        // 最大HP低下
        [InspectorName(PPParameterEffectCategoryDefinition.NameHp + "デバフ")]
        HpDebuff =  1L << 7,
        // 攻撃コスト軽減
        [InspectorName(PPParameterEffectCategoryDefinition.NameCost + "バフ")]
        CostBuff =  1L << 8,
        // 攻撃コスト増加
        [InspectorName(PPParameterEffectCategoryDefinition.NameCost + "デバフ")]
        CostDebuff =  1L << 9,
    }

    // パラメータ種別の日本語表示名を集約した定数群
    // 種別 enum とパラメータ指定 enum の双方から参照され、表記を揃えている
    public static class PPParameterEffectCategoryDefinition
    {
        public const string NameAttack = "攻撃";
        public const string NameDefense = "防御";
        public const string NameSpeed = "素早さ";
        public const string NameHp = "HP";
        public const string NameCost = "コスト";
    }

    // パラメータを増減させるバフ・デバフのランタイム実体
    // 基底の StatusEffect は基本パラメータしか見ないが、
    // 本作は攻撃コストなどを PPBattleUnit.ExtraParameters に持つ
    // そのため適用・除去を上書きし、基本パラメータと追加パラメータの両方を対象にできるようにしている
    public class PPParameterEffect : StatusEffect
    {
        // このエフェクトの種別。解除スキルの対象判定に使う
        public PPParameterEffectCategory Category { get; set; } = PPParameterEffectCategory.None;

        // aEffectId : エフェクトID
        // aDisplayName : UI表示名
        // aDurationCondition : 持続条件。null なら永続
        public PPParameterEffect(string aEffectId, string aDisplayName, IDurationCondition aDurationCondition = null)
            : base(aEffectId, aDisplayName, aDurationCondition)
        {

        }

        // 修飾子を適用する
        // 基底と違い PPBattleUnit.ResolveParameter を通すため、
        // 基本パラメータと追加パラメータのどちらも修飾対象にできる
        // 対象が PPBattleUnit でない場合は修飾子を適用せず通知だけ行う
        // aUnit : 適用先のユニット
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

        // 修飾子を除去する。適用先が 2 系統ありうるため、
        // 基本パラメータと追加パラメータの両方から自身を付与元とする修飾子を剥がす
        // aUnit : 除去先のユニット
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
