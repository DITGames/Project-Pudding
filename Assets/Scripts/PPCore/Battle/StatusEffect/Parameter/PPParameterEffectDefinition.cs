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
    /// <summary>
    /// バフ・デバフの対象となるパラメータ。
    /// 実際のパラメータ ID への対応は <see cref="PPParameterEffectDefinition.ParamId"/> が行う。
    /// </summary>
    public enum PPModifierTargetParam
    {
        /// <summary>攻撃力。</summary>
        [InspectorName(PPParameterEffectCategoryDefinition.NameAttack)]
        Attack,
        /// <summary>防御力。</summary>
        [InspectorName(PPParameterEffectCategoryDefinition.NameDefense)]
        Defense,
        /// <summary>素早さ。</summary>
        [InspectorName(PPParameterEffectCategoryDefinition.NameSpeed)]
        Speed,
        /// <summary>最大HP。</summary>
        [InspectorName(PPParameterEffectCategoryDefinition.NameHp)]
        Hp,
        /// <summary>通常攻撃コスト。追加パラメータ側にある。</summary>
        [InspectorName(PPParameterEffectCategoryDefinition.NameCost)]
        Cost,
    }

    /// <summary>
    /// 変動の向き。設定側は常に正の変動量を入れ、増減の別はこちらで指定する。
    /// </summary>
    public enum PPModifierDirection
    {
        /// <summary>上昇（バフ）。</summary>
        [InspectorName("バフ")]
        Increase,
        /// <summary>低下（デバフ）。</summary>
        [InspectorName("デバフ")]
        Decrease,
    }

    /// <summary>
    /// パラメータを増減させるバフ・デバフの定義。
    /// <para>
    /// 「どのパラメータを」「上げるか下げるか」「加算か乗算か」「どれだけ」の 4 つを
    /// インスペクタで組み合わせて表現する。変動量は常に正の値で入力し、
    /// 符号や倍率への変換は <see cref="ResolveModifier"/> が引き受ける。
    /// </para>
    /// </summary>
    [CreateAssetMenu(fileName = "PPParameterEffectDefinition", menuName = "Project-Pudding/Effect/PPParameterEffectDefinition")]
    public class PPParameterEffectDefinition : PPEffectDefinition
    {
        /// <summary>変動させる対象パラメータ。</summary>
        [Header("パラメータエフェクト")]
        [Label("対象パラメータ")]
        [SerializeField]protected PPModifierTargetParam mTargetParam = PPModifierTargetParam.Attack;
        /// <summary>バフかデバフか。</summary>
        [Label("種別")]
        [SerializeField]protected PPModifierDirection mDirection = PPModifierDirection.Increase;
        /// <summary>加算・乗算・上書きのいずれか。</summary>
        [Label("変動タイプ")]
        [SerializeField]protected ParameterModifierType mModifierType = ParameterModifierType.Add;
        /// <summary>変動量。常に正の値で入力する。</summary>
        [Label("変動量")][Min(0)]
        [SerializeField]protected float mValue = 10f;

        /// <summary>変動させる対象パラメータ。</summary>
        public PPModifierTargetParam TargetParam => mTargetParam;
        /// <summary>バフかデバフか。</summary>
        public PPModifierDirection Direction => mDirection;
        /// <summary>変動タイプ。</summary>
        public ParameterModifierType ModifierType => mModifierType;
        /// <summary>変動量。</summary>
        public float Value => mValue;

        /// <summary>
        /// 対象パラメータと変動方向の組み合わせから、解除判定用の種別を決める。
        /// </summary>
        /// <returns>対応する種別。組み合わせが不明な場合は None。</returns>
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

        /// <summary>
        /// 入力された変動量を、実際に修飾子へ渡す値へ変換する。
        /// 加算はデバフなら符号を反転させる。乗算は変動方向と矛盾する値
        /// （バフなのに 1 未満、デバフなのに 1 超）が設定された場合、
        /// 意図と逆の効果になるのを避けて等倍へ丸める。
        /// </summary>
        /// <returns>修飾子に設定する値。</returns>
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

        /// <summary>
        /// ターン経過で切れるパラメータ変動エフェクトのランタイム実体を生成する。
        /// </summary>
        /// <param name="aSource">エフェクトの付与元ユニット。</param>
        /// <param name="aTarget">付与される対象ユニット。</param>
        /// <param name="aContext">バトルコンテキスト。</param>
        /// <returns>生成されたエフェクト。</returns>
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

        /// <summary>
        /// 対象パラメータに対応するパラメータ ID。
        /// コストのみ追加パラメータ側の ID を返す点に注意。
        /// </summary>
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

        /// <summary>
        /// エフェクトへパラメータ修飾子を 1 件登録する。
        /// </summary>
        /// <param name="aEffect">設定対象のエフェクト。</param>
        /// <param name="aSource">エフェクトの付与元ユニット。</param>
        /// <param name="aTarget">付与される対象ユニット。</param>
        /// <param name="aContext">バトルコンテキスト。</param>
        protected override void ConfigureEffect(StatusEffect aEffect, BattleUnit aSource, BattleUnit aTarget, BattleContext aContext)
        {
            if (aEffect is PPParameterEffect effect)
            {
                aEffect.AddModifier(ParamId, mModifierType, ResolveModifier());
            }
        }

        /// <summary>設定内容の組み合わせからエフェクト ID を組み立てる。</summary>
        protected override string BuildAutoEffectId()
            => $"Param_{mTargetParam}_{mDirection}_{mModifierType}_{mValue}_{mDuration}";
    }
}
