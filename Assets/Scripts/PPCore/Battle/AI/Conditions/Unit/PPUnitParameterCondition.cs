/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file PPUnitParameterCondition.cs
 * @author hqrse
 * @date 2026/08/11
 * @brief ユニット条件 : パラメータ値の比較
 * =====================================*/

using System;
using CommandBattleCore;
using UnityEngine;

namespace PPCore
{
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
        // aUnit : 判定対象のユニット
        // aSnapShot : 評価に使うパーティ状況スナップショット
        // return : 条件を満たす場合 true。パラメータを解決できない場合は false
        public override bool Evaluate(PPBattleUnit aUnit, PPPartyAIContext aSnapShot)
        {
            if (aUnit == null) return false;

            float? value = ResolveValue(aUnit);
            return value.HasValue && PPConditionMath.Compare(value.Value, mOp, mThreshold);
        }

        // 種別に対応する現在値を引く
        // 現在 HP だけは修飾子の対象ではなく残量そのものを見るため、ID 経由ではなく直接参照する
        // aUnit : 判定対象のユニット
        // return : 解決された値。解決できなければ null
        private float? ResolveValue(PPBattleUnit aUnit)
        {
            if (mKind == PPUnitParameterKind.CurrentHp)
                return aUnit.Parameters.Hp.CurrentValue;

            var parameter = aUnit.ResolveParameter(ResolveParameterId(mKind));
            return parameter?.CurrentValue;
        }

        // 種別に対応するパラメータ ID を返す
        // aKind : パラメータ種別
        // return : パラメータ ID
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
        // aKind : パラメータ種別
        // return : 日本語の表記
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
}
