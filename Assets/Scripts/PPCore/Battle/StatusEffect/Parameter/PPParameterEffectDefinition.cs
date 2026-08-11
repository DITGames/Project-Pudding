/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file PPParameterEffectDefinition.cs
 * @author hqrse
 * @date 2026/07/27
 * @brief パラメータ異常のデータ定義
 * =====================================*/

using System;
using CommandBattleCore;
using UnityEngine;

namespace PPCore
{
    // バフ・デバフの対象となるパラメータ
    // 実際のパラメータ ID への対応は PPParameterEffectDefinition.ParamId が行う
    public enum PPModifierTargetParam
    {
        [InspectorName(PPParameterEffectCategoryDefinition.NameAttack)]
        Attack,
        [InspectorName(PPParameterEffectCategoryDefinition.NameDefense)]
        Defense,
        [InspectorName(PPParameterEffectCategoryDefinition.NameSpeed)]
        Speed,
        [InspectorName(PPParameterEffectCategoryDefinition.NameHp)]
        Hp,
        // 通常攻撃コスト。追加パラメータ側にある
        [InspectorName(PPParameterEffectCategoryDefinition.NameCost)]
        Cost,
        // 1 ティックあたりの行動回数上限。追加パラメータ側にある
        [InspectorName(PPParameterEffectCategoryDefinition.NameActionCount)]
        ActionCount,
    }

    // 変動の向き。設定側は常に正の変動量を入れ、増減の別はこちらで指定する
    public enum PPModifierDirection
    {
        [InspectorName("バフ")]
        Increase,
        [InspectorName("デバフ")]
        Decrease,
    }

    // パラメータを増減させるバフ・デバフの定義
    // 「どのパラメータを」「上げるか下げるか」「加算か乗算か」「どれだけ」の 4 つを
    // インスペクタで組み合わせて表現する。変動量は常に正の値で入力し、
    // 符号や倍率への変換は ResolveModifier が引き受ける
    [Serializable]
    [PPTypeMenuName("StatusEffect付与/パラメータ変動")]
    public class PPParameterEffectDefinition : PPEffectDefinition
    {
        [Header("パラメータエフェクト")]
        [Label("対象パラメータ")]
        [SerializeField]protected PPModifierTargetParam mTargetParam = PPModifierTargetParam.Attack;
        [Label("種別")]
        [SerializeField]protected PPModifierDirection mDirection = PPModifierDirection.Increase;
        [Label("変動タイプ")]
        [SerializeField]protected ParameterModifierType mModifierType = ParameterModifierType.Add;
        // 変動量。常に正の値で入力する
        [Label("変動量")][Min(0)]
        [SerializeField]protected float mValue = 10f;

        public PPModifierTargetParam TargetParam => mTargetParam;
        public PPModifierDirection Direction => mDirection;
        public ParameterModifierType ModifierType => mModifierType;
        public float Value => mValue;

        // 対象パラメータと変動方向の組み合わせから、解除判定用の分類を決める
        public override PPEffectCategory Category
            => (mTargetParam, mDirection) switch
            {
                (PPModifierTargetParam.Attack, PPModifierDirection.Increase) => PPEffectCategory.AttackBuff,
                (PPModifierTargetParam.Attack, PPModifierDirection.Decrease) => PPEffectCategory.AttackDebuff,
                (PPModifierTargetParam.Defense, PPModifierDirection.Increase) => PPEffectCategory.DefenseBuff,
                (PPModifierTargetParam.Defense, PPModifierDirection.Decrease) => PPEffectCategory.DefenseDebuff,
                (PPModifierTargetParam.Speed, PPModifierDirection.Increase) => PPEffectCategory.SpeedBuff,
                (PPModifierTargetParam.Speed, PPModifierDirection.Decrease) => PPEffectCategory.SpeedDebuff,
                (PPModifierTargetParam.Hp, PPModifierDirection.Increase) => PPEffectCategory.MaxHpBuff,
                (PPModifierTargetParam.Hp, PPModifierDirection.Decrease) => PPEffectCategory.MaxHpDebuff,
                (PPModifierTargetParam.Cost, PPModifierDirection.Increase) => PPEffectCategory.CostBuff,
                (PPModifierTargetParam.Cost, PPModifierDirection.Decrease) => PPEffectCategory.CostDebuff,
                (PPModifierTargetParam.ActionCount, PPModifierDirection.Increase) => PPEffectCategory.ActionCountBuff,
                _ => PPEffectCategory.ActionCountDebuff,
            };

        public override StatusEffectTag Tags
            => StatusEffectTag.ParameterMod
             | (mDirection == PPModifierDirection.Increase ? StatusEffectTag.Buff : StatusEffectTag.Debuff);

        // 入力された変動量を、実際に修飾子へ渡す値へ変換する
        // 加算はデバフなら符号を反転させる。乗算は変動方向と矛盾する値
        // （バフなのに 1 未満、デバフなのに 1 超）が設定された場合、
        // 意図と逆の効果になるのを避けて等倍へ丸める
        // return : 修飾子に設定する値
        private float ResolveModifier()
            => (mModifierType, mDirection) switch
            {
                (ParameterModifierType.Add, PPModifierDirection.Increase) => mValue,
                (ParameterModifierType.Add, PPModifierDirection.Decrease) => -mValue,
                (ParameterModifierType.Multiply, PPModifierDirection.Increase) when mValue < 1 => 1f,
                (ParameterModifierType.Multiply, PPModifierDirection.Increase) => mValue,
                (ParameterModifierType.Multiply, PPModifierDirection.Decrease) when mValue > 1 => 1f,
                (ParameterModifierType.Multiply, PPModifierDirection.Decrease) => mValue,
                (_,_) => mValue,
            };

        // 対象パラメータに対応するパラメータ ID
        // コストと行動回数上限のみ追加パラメータ側の ID を返す点に注意
        protected string ParamId
            => mTargetParam switch
            {
                PPModifierTargetParam.Attack => ParameterSet.ParamIdAttack,
                PPModifierTargetParam.Defense => ParameterSet.ParamIdDefense,
                PPModifierTargetParam.Speed => ParameterSet.ParamIdSpeed,
                PPModifierTargetParam.Hp => ParameterSet.ParamIdMaxHp,
                PPModifierTargetParam.Cost => PPParameterSet.ParameterIdAttackCost,
                PPModifierTargetParam.ActionCount => PPParameterSet.ParameterIdActionCount,
                _ => string.Empty,
            };

        // エフェクトへパラメータ修飾の振る舞いを 1 件積む
        // aEffect : 設定対象のエフェクト
        // aSource : エフェクトの付与元ユニット
        // aTarget : 付与される対象ユニット
        // aContext : バトルコンテキスト
        protected override void ConfigureBehaviours(StatusEffect aEffect, BattleUnit aSource, BattleUnit aTarget, BattleContext aContext)
        {
            aEffect.AddBehaviour(new ParameterModifierBehaviour(ParamId, mModifierType, ResolveModifier()));
        }

        // 設定内容の組み合わせからエフェクト ID を組み立てる
        protected override string BuildAutoEffectId()
            => $"Param_{mTargetParam}_{mDirection}_{mModifierType}_{mValue}_{mDuration}";

        public override string BuildString()
            => $"{mTargetParam} {mDirection}：{mValue}（{mModifierType}）";
    }
}
