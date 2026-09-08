/* =====================================
 * Copyright WabisabiAndons. All rights reserved.
 * @file PPParameterModifierBehaviourDefinition.cs
 * @author hqrse
 * @date 2026/09/03
 * @brief パラメータを増減させる振る舞いのデータ定義
 * =====================================*/

using System;
using CommandBattleCore;
using UnityEngine;
using AttributeUtility;

namespace PPCore
{
    // バフ・デバフの対象となるパラメータ
    // 実際のパラメータ ID への対応は ParamId が行う
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

    // パラメータを増減させるバフ・デバフの振る舞い定義
    // 「どのパラメータを」「上げるか下げるか」「加算か乗算か」「どれだけ」の 4 つを
    // インスペクタで組み合わせて表現する。変動量は常に正の値で入力し、
    // 符号や倍率への変換は ResolveModifier が引き受ける
    [Serializable]
    [PPTypeMenuName("パラメータ変動")]
    public class PPParameterModifierBehaviourDefinition : PPStatusEffectBehaviourDefinition
    {
        [Header("パラメータ変動")]
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

        // aEffect : 組み立て先のエフェクト
        // aSource : エフェクトの付与元ユニット
        // aTarget : 付与される対象ユニット
        // aContext : バトルコンテキスト
        public override void ConfigureBehaviour(StatusEffect aEffect, BattleUnit aSource, BattleUnit aTarget, BattleContext aContext)
        {
            aEffect.AddBehaviour(new ParameterModifierBehaviour(ParamId, mModifierType, ResolveModifier()));
        }

        public override string BuildString()
            => $"{mTargetParam} {mDirection}：{mValue}（{mModifierType}）";
    }
}
