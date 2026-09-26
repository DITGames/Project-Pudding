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
        // きようさ（会心率の元）。追加パラメータ側にある
        // シリアライズ済みの整数値を保つため、要素の追加は末尾に行う
        [InspectorName(PPParameterEffectCategoryDefinition.NameDexterity)]
        Dexterity,
    }

    // パラメータを増減させるバフ・デバフの振る舞い定義
    // 「どのパラメータを」「どの方式で」「どれだけ」の 3 つをインスペクタで組み合わせて表現する
    // 能力値のバフ・デバフは割合加算（+20% は 0.2、-10% は -0.1）を基本とする
    // バフ／デバフの別は個別のフィールドを持たず、変動量の符号（加算・割合加算）・1 を挟んだ大小（乗算）でそのまま表す
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
        // 割合加算：割合を入力する（0.2で+20%＝バフ、-0.1で-10%＝デバフ）。同じパラメータへの割合は合計してから倍率になる
        [Label("変動量")]
        [SerializeField]protected float mAmount = 10f;

        public PPModifierTargetParam TargetParam => mTargetParam;
        public ParameterModifierType ModifierType => mModifierType;
        public float Amount => mAmount;

        // 変動量がバフ・デバフのどちら向きかを表示用に判定する
        // 加算・割合加算は符号、乗算は 1 を基準に判定する
        private bool IsBuff
            => mModifierType == ParameterModifierType.Multiply ? mAmount >= 1f : mAmount >= 0f;

        // 対象パラメータに対応するパラメータ ID
        // コスト・行動回数上限・きようさは追加パラメータ側の ID を返す点に注意
        protected string ParamId
            => mTargetParam switch
            {
                PPModifierTargetParam.Attack => ParameterSet.ParamIdAttack,
                PPModifierTargetParam.Defense => ParameterSet.ParamIdDefense,
                PPModifierTargetParam.Speed => ParameterSet.ParamIdSpeed,
                PPModifierTargetParam.Hp => ParameterSet.ParamIdMaxHp,
                PPModifierTargetParam.Cost => PPParameterSet.ParameterIdAttackCost,
                PPModifierTargetParam.ActionCount => PPParameterSet.ParameterIdActionCount,
                PPModifierTargetParam.Dexterity => PPParameterSet.ParameterIdDexterity,
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

        // 変動量の表示用文字列。割合加算は百分率（0.2 → +20%）で表す
        private string AmountText
            => mModifierType == ParameterModifierType.Percent
                ? (mAmount * 100f).ToString("+0.##;-0.##;0") + "%"
                : mAmount.ToString();

        public override string BuildString()
            => $"{mTargetParam} {(IsBuff ? "バフ" : "デバフ")}：{AmountText}（{mModifierType}）";
    }
}
