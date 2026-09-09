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

    // パラメータを増減させるバフ・デバフの振る舞い定義
    // 「どのパラメータを」「加算か乗算か」「どれだけ」の 3 つをインスペクタで組み合わせて表現する
    // バフ／デバフの別は個別のフィールドを持たず、変動量の符号（加算）・1 を挟んだ大小（乗算）でそのまま表す
    // （対になるバフ・デバフが互いを検索して打ち消す実装は避け、最終計算時の合算に委ねる設計のため、
    //   個々の振る舞い定義に「向き」という概念自体を持たせない）
    [Serializable]
    [PPTypeMenuName("パラメータ変動")]
    public class PPParameterModifierBehaviourDefinition : PPStatusEffectBehaviourDefinition
    {
        [Header("パラメータ変動")]
        [Label("対象パラメータ")]
        [SerializeField]protected PPModifierTargetParam mTargetParam = PPModifierTargetParam.Attack;
        [Label("変動タイプ")]
        [SerializeField]protected ParameterModifierType mModifierType = ParameterModifierType.Add;
        // 加算：正で増加・負で減少。乗算：最終倍率をそのまま入力する（1.5で1.5倍＝バフ、0.5で半分＝デバフ）
        [Label("変動量")]
        [SerializeField]protected float mAmount = 10f;

        public PPModifierTargetParam TargetParam => mTargetParam;
        public ParameterModifierType ModifierType => mModifierType;
        public float Amount => mAmount;

        // 変動量がバフ・デバフのどちら向きかを表示用に判定する
        // 加算は符号、乗算は 1 を基準に判定する
        private bool IsBuff
            => mModifierType == ParameterModifierType.Multiply ? mAmount >= 1f : mAmount >= 0f;

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
            aEffect.AddBehaviour(new ParameterModifierBehaviour(ParamId, mModifierType, mAmount));
        }

        public override string BuildString()
            => $"{mTargetParam} {(IsBuff ? "バフ" : "デバフ")}：{mAmount}（{mModifierType}）";
    }
}
