/* =====================================
 * Copyright WabisabiAndons. All rights reserved.
 * @file PPUnitStateConditions.cs
 * @author hqrse
 * @date 2026/08/11
 * @brief ユニット条件（ユニット状態）
 * =====================================*/

using System;
using UnityEngine;
using AttributeUtility;

namespace PPCore
{
    // ユニット条件: そのユニットの HP 割合を判定する
    // 「瀕死の味方だけを実行者にする」「まだ余裕のある攻撃役に大技を撃たせる」といった絞り込みに使う
    [Serializable]
    [PPTypeMenuName("ユニット状態/HP割合")]
    public sealed class PPUnitHpRatioCondition : PPUnitConditionValidator
    {
        [Label("比較")]
        [SerializeField] private PPCompareOp mOp = PPCompareOp.LessOrEqual;
        [PercentLabel("HP割合")]
        [SerializeField] private float mThreshold = 0.5f;

        // HP 割合を閾値と比較する
        public override bool Evaluate(PPBattleUnit aUnit, PPPartyAIContext aSnapShot)
            => aUnit != null && PPConditionMath.Compare(PPPartyAIContext.HpRatio(aUnit), mOp, mThreshold);

        // 設定内容から説明文を組み立てる
        [ContextMenu("説明文を生成")]
        protected override void BuildDescription()
            => mDescription = $"HP割合が{mThreshold * 100f:F0}%{GetOpString(mOp)}";
    }

    // ユニット条件: 生存していて行動制限も掛かっていないか
    // 主な用途は「達成済み判定条件」で対象の生存を確かめるケース
    [Serializable]
    [PPTypeMenuName("ユニット状態/行動可能")]
    public sealed class PPUnitAliveCondition : PPUnitConditionValidator
    {
        // 生存だけでなく麻痺などの行動不可も見るか
        [Label("行動制限も見る")]
        [SerializeField] private bool mIsCheckRestriction = true;
        // 反転すると「行動できない」の判定になる
        [Label("条件を反転する")]
        [SerializeField] private bool mIsInvert = false;

        // ユニットが行動可能かを判定する
        public override bool Evaluate(PPBattleUnit aUnit, PPPartyAIContext aSnapShot)
        {
            bool canAct = aUnit != null && aUnit.IsAlive;
            if (canAct && mIsCheckRestriction)
            {
                // 確定で行動できない（睡眠など）場合だけを「行動できない」とする
                // 麻痺のような確率付きの行動不能は、行動すれば成功する可能性があるため行動を割り当てる
                canAct = !aUnit.IsActionCertainlyBlocked;
            }
            return canAct != mIsInvert;
        }

        // 設定内容から説明文を組み立てる
        [ContextMenu("説明文を生成")]
        protected override void BuildDescription()
        {
            string target = mIsCheckRestriction ? "行動できる" : "生存している";
            mDescription = mIsInvert ? target.Replace("できる", "できない").Replace("している", "していない") : target;
        }
    }

    // 判定対象にできるパラメータの種別
    // パラメータ ID を直接文字列で入力させるとタイポで常に不成立になるため、選択式にしている
    public enum PPUnitParameterKind
    {
        [InspectorName("現在HP")]
        CurrentHp,
        [InspectorName("最大HP")]
        MaxHp,
        [InspectorName("攻撃力")]
        Attack,
        [InspectorName("防御力")]
        Defense,
        [InspectorName("素早さ")]
        Speed,
        [InspectorName("通常攻撃コスト")]
        AttackCost,
        [InspectorName("行動回数上限")]
        ActionCount,
    }

    // ユニット条件: パラメータの現在値を判定する
    // 「攻撃力が一定以上のユニットに大技を撃たせる」のように、実行者を能力で絞るのに使う
    [Serializable]
    [PPTypeMenuName("ユニット状態/パラメータ比較")]
    public sealed class PPUnitParameterCondition : PPUnitConditionValidator
    {
        [Label("対象パラメータ")]
        [SerializeField] private PPUnitParameterKind mKind = PPUnitParameterKind.Attack;
        [Label("比較")]
        [SerializeField] private PPCompareOp mOp = PPCompareOp.GreaterOrEqual;
        [Label("閾値")]
        [SerializeField] private float mThreshold = 0f;

        // パラメータの現在値を閾値と比較する
        public override bool Evaluate(PPBattleUnit aUnit, PPPartyAIContext aSnapShot)
        {
            if (aUnit == null) return false;

            float? value = ResolveValue(aUnit);
            return value.HasValue && PPConditionMath.Compare(value.Value, mOp, mThreshold);
        }

        // 種別に対応する現在値を引く
        // 現在 HP だけは修飾子の対象ではなく残量そのものを見るため、ID 経由ではなく直接参照する
        private float? ResolveValue(PPBattleUnit aUnit)
        {
            if (mKind == PPUnitParameterKind.CurrentHp)
                return aUnit.Parameters.Hp.CurrentValue;

            var parameter = aUnit.ResolveParameter(ResolveParameterId(mKind));
            return parameter?.CurrentValue;
        }

        // 種別に対応するパラメータ ID を返す
        private static string ResolveParameterId(PPUnitParameterKind aKind)
            => aKind switch
            {
                PPUnitParameterKind.MaxHp => ParameterSet.ParamIdMaxHp,
                PPUnitParameterKind.Attack => ParameterSet.ParamIdAttack,
                PPUnitParameterKind.Defense => ParameterSet.ParamIdDefense,
                PPUnitParameterKind.Speed => ParameterSet.ParamIdSpeed,
                PPUnitParameterKind.AttackCost => PPParameterSet.ParameterIdAttackCost,
                PPUnitParameterKind.ActionCount => PPParameterSet.ParameterIdActionCount,
                _ => "",
            };

        // 設定内容から説明文を組み立てる
        [ContextMenu("説明文を生成")]
        protected override void BuildDescription()
            => mDescription = $"{GetKindString(mKind)}が{mThreshold}{GetOpString(mOp)}";

        // パラメータ種別を説明文用の日本語へ変換する
        private static string GetKindString(PPUnitParameterKind aKind)
            => aKind switch
            {
                PPUnitParameterKind.CurrentHp => "現在HP",
                PPUnitParameterKind.MaxHp => "最大HP",
                PPUnitParameterKind.Attack => "攻撃力",
                PPUnitParameterKind.Defense => "防御力",
                PPUnitParameterKind.Speed => "素早さ",
                PPUnitParameterKind.AttackCost => "通常攻撃コスト",
                PPUnitParameterKind.ActionCount => "行動回数上限",
                _ => "",
            };
    }

    // ユニット条件: ユニットの属性が指定と一致するか
    // 弱点を突ける属性のユニットにだけ行動させる、といった絞り込みに使う
    [Serializable]
    [PPTypeMenuName("ユニット状態/属性")]
    public sealed class PPUnitTypeAttributeCondition : PPUnitConditionValidator
    {
        [Label("対象属性")]
        [SerializeField] private PPTypeAttribute mTypeAttribute = PPTypeAttribute.Normal;
        // 反転すると「その属性ではない」の判定になる
        [Label("条件を反転する")]
        [SerializeField] private bool mIsInvert = false;

        // ユニットの属性が指定と一致するかを判定する
        public override bool Evaluate(PPBattleUnit aUnit, PPPartyAIContext aSnapShot)
            => aUnit != null && (aUnit.TypeAttribute == mTypeAttribute) != mIsInvert;

        // 設定内容から説明文を組み立てる
        [ContextMenu("説明文を生成")]
        protected override void BuildDescription()
        {
            string type = GetResourceTypeString(mTypeAttribute);
            mDescription = mIsInvert ? $"属性が{type}ではない" : $"属性が{type}";
        }

        // 属性を説明文用の日本語へ変換する。表示名は定数から引くためハードコードしない
        private static string GetResourceTypeString(PPTypeAttribute aTypeAttribute)
            => aTypeAttribute switch
            {
                PPTypeAttribute.Normal => PPTypeAttributeDefinition.TypeNormal,
                PPTypeAttribute.Fire => PPTypeAttributeDefinition.TypeFire,
                PPTypeAttribute.Water => PPTypeAttributeDefinition.TypeWater,
                PPTypeAttribute.Earth => PPTypeAttributeDefinition.TypeEarth,
                PPTypeAttribute.Shine => PPTypeAttributeDefinition.TypeShine,
                PPTypeAttribute.Dark => PPTypeAttributeDefinition.TypeDark,
                _ => ""
            };
    }

    // ユニット条件: 指定の状態異常・バフが付与されているか
    // エフェクト ID とタグで絞り込み、どちらも未指定なら何か 1 つでも付与されていれば成立する
    [Serializable]
    [PPTypeMenuName("ユニット状態/状態異常の付与")]
    public sealed class PPUnitStatusEffectCondition : PPUnitConditionValidator
    {
        // 判定するエフェクト。未設定なら特定のエフェクトでは絞り込まない
        [Label("エフェクト")]
        [SerializeField] private PPEffectDefinition mEffect;
        // 判定するタグ。None なら タグでは絞り込まない
        [Label("エフェクトタグ")]
        [SerializeField] private StatusEffectTag mTags = StatusEffectTag.None;
        // 反転すると「付与されていない」の判定になる
        [Label("条件を反転する")]
        [SerializeField] private bool mIsInvert = false;

        // 指定の状態異常が付与されているかを判定する
        public override bool Evaluate(PPBattleUnit aUnit, PPPartyAIContext aSnapShot)
            => aUnit != null && HasMatchingEffect(aUnit) != mIsInvert;

        // 条件に合致する状態異常を持っているかを走査する
        // ID とタグはどちらも未指定なら素通しになるため、両方未指定だと付与の有無だけを見る
        private bool HasMatchingEffect(PPBattleUnit aUnit)
        {
            foreach (var effect in aUnit.ActiveStatusEffects)
            {
                if (effect == null) continue;
                if (mEffect != null && effect.EffectId != mEffect.EffectId) continue;
                if (mTags != StatusEffectTag.None && (effect.Tags & mTags) == 0) continue;

                return true;
            }
            return false;
        }

        // 設定内容から説明文を組み立てる
        [ContextMenu("説明文を生成")]
        protected override void BuildDescription()
        {
            string target = mEffect == null
                ? (mTags == StatusEffectTag.None ? "何らかの状態異常" : $"{mTags} の効果")
                : mEffect.DisplayName;
            mDescription = mIsInvert ? $"{target} が付与されていない" : $"{target} が付与されている";
        }
    }
}
