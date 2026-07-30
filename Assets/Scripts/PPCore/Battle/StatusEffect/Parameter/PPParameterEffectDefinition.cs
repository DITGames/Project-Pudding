/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file PPParameterEffectDefinition.cs
 * @author hqrse
 * @date 2026/07/27
 * @brief パラメータ異常のデータ定義
 * =====================================*/

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
    [CreateAssetMenu(fileName = "PPParameterEffectDefinition", menuName = "Project-Pudding/Effect/PPParameterEffectDefinition")]
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

        // 対象パラメータと変動方向の組み合わせから、解除判定用の種別を決める
        // return : 対応する種別。組み合わせが不明な場合は None
        private PPParameterEffectCategory ResolveCategory()
            => (mTargetParam, mDirection) switch
            {
                (PPModifierTargetParam.Attack, PPModifierDirection.Increase) => PPParameterEffectCategory.AttackBuff,
                (PPModifierTargetParam.Attack, PPModifierDirection.Decrease) => PPParameterEffectCategory.AttackDebuff,
                (PPModifierTargetParam.Defense, PPModifierDirection.Increase) => PPParameterEffectCategory.DefenseBuff,
                (PPModifierTargetParam.Defense, PPModifierDirection.Decrease) => PPParameterEffectCategory.DefenseDebuff,
                (PPModifierTargetParam.Speed, PPModifierDirection.Increase) => PPParameterEffectCategory.SpeedBuff,
                (PPModifierTargetParam.Speed, PPModifierDirection.Decrease) => PPParameterEffectCategory.SpeedDebuff,
                (PPModifierTargetParam.Hp, PPModifierDirection.Increase) => PPParameterEffectCategory.HpBuff,
                (PPModifierTargetParam.Hp, PPModifierDirection.Decrease) => PPParameterEffectCategory.HpDebuff,
                (PPModifierTargetParam.Cost, PPModifierDirection.Increase) => PPParameterEffectCategory.CostBuff,
                (PPModifierTargetParam.Cost, PPModifierDirection.Decrease) => PPParameterEffectCategory.CostDebuff,
                _ => PPParameterEffectCategory.None,
            };

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

        // ターン経過で切れるパラメータ変動エフェクトのランタイム実体を生成する
        // aSource : エフェクトの付与元ユニット
        // aTarget : 付与される対象ユニット
        // aContext : バトルコンテキスト
        // return : 生成されたエフェクト
        public override StatusEffect CreateRuntimeStatusEffect(BattleUnit aSource, BattleUnit aTarget, BattleContext aContext)
        {
            var effect = new PPParameterEffect(mEffectId, mDisplayName, new TurnDurationCondition(mDuration))
            {
                StackPolicy = mStackPolicy,
                MaxStacks = mMaxStack,
                Category = ResolveCategory(),
            };
            ConfigureEffect(effect, aSource, aTarget, aContext);
            return effect;
        }

        // 対象パラメータに対応するパラメータ ID
        // コストのみ追加パラメータ側の ID を返す点に注意
        protected string ParamId
            => mTargetParam switch
            {
                PPModifierTargetParam.Attack => ParameterSet.ParamIdAttack,
                PPModifierTargetParam.Defense => ParameterSet.ParamIdDefense,
                PPModifierTargetParam.Speed => ParameterSet.ParamIdSpeed,
                PPModifierTargetParam.Hp => ParameterSet.ParamIdMaxHp,
                PPModifierTargetParam.Cost => PPParameterSet.ParameterIdAttackCost,
                _ => string.Empty,
            };

        // エフェクトへパラメータ修飾子を 1 件登録する
        // aEffect : 設定対象のエフェクト
        // aSource : エフェクトの付与元ユニット
        // aTarget : 付与される対象ユニット
        // aContext : バトルコンテキスト
        protected override void ConfigureEffect(StatusEffect aEffect, BattleUnit aSource, BattleUnit aTarget, BattleContext aContext)
        {
            if (aEffect is PPParameterEffect effect)
            {
                aEffect.AddModifier(ParamId, mModifierType, ResolveModifier());
            }
        }

        // 設定内容の組み合わせからエフェクト ID を組み立てる
        protected override string BuildAutoEffectId()
            => $"Param_{mTargetParam}_{mDirection}_{mModifierType}_{mValue}_{mDuration}";
    }
}
