/* =====================================
 * Copyright WabisabiAndons. All rights reserved.
 * @file PPModifierBehaviours.cs
 * @author hqrse
 * @date 2026/09/04
 * @brief 行動制限・被ダメージ倍率・パラメータ補正の振る舞い（定義とランタイム）
 * =====================================*/

using System;
using UnityEngine;
using UnityEngine.Serialization;
using AttributeUtility;

namespace PPCore
{
    // 掛かっている間、対象へ行動制限を課す振る舞い。失敗確率は効果の発生回数で変化させられる
    // ActionFailChance が上書きし合うため、制限は 1 エフェクトにつき 1 つを想定している
    public sealed class ActionRestrictionBehaviour : StatusEffectBehaviour
    {
        /* 状態 */

        // 課す行動制限
        private readonly ActionRestriction mRestriction;
        // true なら確率抽選せず必ず行動を阻害する（睡眠など）
        private readonly bool mIsAlwaysBlock;
        // 発生回数 0 の時点での行動阻害確率（0～1）
        private readonly float mBaseChance;
        // 発生 1 回あたりの確率の増分。負の値なら経つほど動けるようになる
        private readonly float mChancePerElapsedTick;

        /* 生成 */

        public ActionRestrictionBehaviour(ActionRestriction aRestriction, bool aIsAlwaysBlock = true,
            float aBaseChance = 1f, float aChancePerElapsedTick = 0f)
        {
            mRestriction = aRestriction;
            mIsAlwaysBlock = aIsAlwaysBlock;
            mBaseChance = aBaseChance;
            mChancePerElapsedTick = aChancePerElapsedTick;
        }

        /* 操作 */

        // 現在の発生回数から確率を求め、エフェクトへ行動制限を設定する
        // aContext : 振る舞いの実行コンテキスト
        private void ApplyRestriction(StatusEffectContext aContext)
        {
            var effect = aContext.Effect;
            if (effect == null) return;

            // 確定阻害は ActionFailChance に null を入れる（RollActionBlocked が無条件で阻害と判定する）
            float? chance = mIsAlwaysBlock
                ? null
                : Mathf.Clamp01(mBaseChance + mChancePerElapsedTick * effect.ElapsedTicks);

            // 他の振る舞いが積んだ制限を消さないよう、既存のフラグへ足す形で設定する
            effect.WithRestriction(effect.Restriction | mRestriction, chance);
        }

        /* イベントハンドラ */

        // 付与された時点で有効にする。行動判定は付与直後から走るため OnTick 任せにはしない
        public override void OnApply(StatusEffectContext aContext) => ApplyRestriction(aContext);

        // 発生回数で確率が変わるため、効果の発生のたびに設定し直す
        public override void OnTick(StatusEffectContext aContext) => ApplyRestriction(aContext);
    }

    // 掛かっている間、対象へ行動制限を課す振る舞いの定義（確定なら睡眠、確率なら麻痺）
    // 「行動しようとしたときに一定確率で解ける」挙動は、エフェクト側のスタック消費ルールで組む
    [Serializable]
    [PPTypeMenuName("行動制限")]
    public class PPActionRestrictionBehaviourDefinition : PPStatusEffectBehaviourDefinition
    {
        /* インスペクタ設定 */

        [Header("行動制限")]
        [Label("制限内容")]
        [SerializeField]protected ActionRestriction mRestriction = ActionRestriction.CannotAct;
        // false にすると下の確率で抽選する
        [Label("必ず阻害する")]
        [SerializeField]protected bool mIsAlwaysBlock = true;
        // 付与直後（発生回数 0）の阻害確率
        [Label("基本確率")][Range(0f, 1f)]
        [SerializeField]protected float mBaseChance = 0.5f;
        // 発生 1 回あたりの確率の増分。負の値にすると経つほど動けるようになる
        [Label("発生1回あたりの増分")][Range(-1f, 1f)]
        [FormerlySerializedAs("mChancePerElapsedTurn")]
        [SerializeField]protected float mChancePerElapsedTick = 0f;

        /* プロパティ */

        // 表示と実行で扱いを揃えるため、常に 0～1 へ丸めた値を使う
        protected float BaseChance => Mathf.Clamp01(mBaseChance);

        /* 初期化・接続 */

        public override void ConfigureBehaviour(StatusEffect aEffect, BattleUnit aSource, BattleUnit aTarget, BattleContext aContext)
        {
            aEffect.AddBehaviour(new ActionRestrictionBehaviour(
                mRestriction, mIsAlwaysBlock, BaseChance, mChancePerElapsedTick));
        }

        /* 説明文 */

        public override string BuildString()
        {
            string rate = mIsAlwaysBlock ? "確定" : $"{BaseChance:P0}";
            string curve = mIsAlwaysBlock || Mathf.Approximately(mChancePerElapsedTick, 0f)
                ? string.Empty
                : $" 発生ごとに{mChancePerElapsedTick:+0.##%;-0.##%}";
            return $"{ToDisplayName(mRestriction)}：{rate}{curve}";
        }

        // 行動制限の日本語表示名を得る
        // 複数フラグの組み合わせは列挙値そのままを返す
        protected static string ToDisplayName(ActionRestriction aRestriction)
            => aRestriction switch
            {
                ActionRestriction.CannotAct => "行動不能",
                ActionRestriction.Confused => "行動ランダム化",
                ActionRestriction.Silenced => "スキル使用不可",
                ActionRestriction.CannotEscape => "逃走不可",
                ActionRestriction.CannotSwap => "入れ替え不可",
                _ => aRestriction.ToString(),
            };
    }

    public sealed class DamageScaleBehaviour : StatusEffectBehaviour
    {
        /* 状態 */

        // 1.0で等倍、0.5で半減、0で無効化、1.5で1.5倍
        private readonly float mScale;
        private readonly int mOrder;
        // true なら状態異常によるダメージ（StatusEffectDamageReason）には倍率を掛けない
        private readonly bool mIsExcludeStatusEffectDamage;

        /* プロパティ */

        public override int Order => mOrder;

        /* 生成 */

        // 増幅は先(Order小)、軽減は後(Order大)に適用されるよう既定値を分けておく
        public DamageScaleBehaviour(float aScale, int aOrder = 0, bool aIsExcludeStatusEffectDamage = false)
        {
            mScale = aScale;
            mOrder = aOrder;
            mIsExcludeStatusEffectDamage = aIsExcludeStatusEffectDamage;
        }

        /* 操作 */

        public override void ModifyIncomingDamage(StatusEffectContext aContext, DamageInfo aDamage)
        {
            if (mIsExcludeStatusEffectDamage && aDamage.Reason is StatusEffectDamageReason) return;

            aDamage.Amount *= mScale;
            if (aDamage.Amount <= 0f) aDamage.IsNullified = true;
        }
    }

    // 被ダメージを倍率で増減させる振る舞いの定義（防御姿勢・被ダメージ半減/無効など）
    [Serializable]
    [PPTypeMenuName("被ダメージ倍率")]
    public class PPDamageScaleBehaviourDefinition : PPStatusEffectBehaviourDefinition
    {
        /* インスペクタ設定 */

        [Header("被ダメージ倍率")]
        // 1で等倍、0.5で半減、0で無効化、1.5で1.5倍
        [Label("倍率")][Min(0)]
        [SerializeField]protected float mScale = 0.5f;

        [Label("適用順")]
        [SerializeField]protected int mOrder = 0;

        // 毒などの状態異常によるダメージには倍率を掛けない（被ダメージアップなど）
        // 既定は false で、防御姿勢などはすべてのダメージに掛かる
        [Label("状態異常ダメージには掛けない")]
        [SerializeField]protected bool mIsExcludeStatusEffectDamage = false;

        /* 初期化・接続 */

        public override void ConfigureBehaviour(StatusEffect aEffect, BattleUnit aSource, BattleUnit aTarget, BattleContext aContext)
        {
            aEffect.AddBehaviour(new DamageScaleBehaviour(mScale, mOrder, mIsExcludeStatusEffectDamage));
        }

        /* 説明文 */

        public override string BuildString()
            => $"被ダメージ倍率：{mScale}倍{(mIsExcludeStatusEffectDamage ? "（状態異常ダメージを除く）" : string.Empty)}";
    }

    public sealed class ParameterModifierBehaviour : StatusEffectBehaviour
    {
        /* 状態 */

        private readonly string mParamId;
        private readonly ParameterModifierType mType;
        // 1スタックあたりの変動量。符号込みで受け取る
        private readonly float mValuePerStack;
        private readonly int mPriority;
        // 実際に適用した修飾子。スタック変動時に差し替えるため保持する
        private ParameterModifier mApplied;

        /* 生成 */

        public ParameterModifierBehaviour(string aParamId, ParameterModifierType aType,
            float aValuePerStack, int aPriority = 0)
        {
            mParamId = aParamId;
            mType = aType;
            mValuePerStack = aValuePerStack;
            mPriority = aPriority;
        }

        /* 操作 */

        // 現在のスタック数に応じた値で修飾子を貼り直す
        private void Reapply(StatusEffectContext aContext)
        {
            var param = aContext.Owner?.ResolveParameter(mParamId);
            if (param == null) return;

            if (mApplied != null) param.RemoveModifier(mApplied);
            mApplied = new ParameterModifier(mType, aContext.Effect, ResolveValue(aContext.Stacks), mPriority);
            param.AddModifier(mApplied);
        }

        /* 問い合わせ */

        // 加算・割合加算・上書きはスタック数倍、乗算はスタック数乗にする
        // 割合加算は +20% × 2 スタック = +40% のように線形に積み上がる
        private float ResolveValue(int aStacks)
            => mType == ParameterModifierType.Multiply
                ? Mathf.Pow(mValuePerStack, aStacks)
                : mValuePerStack * aStacks;

        /* イベントハンドラ */

        public override void OnApply(StatusEffectContext aContext) => Reapply(aContext);

        public override void OnStackChanged(StatusEffectContext aContext) => Reapply(aContext);

        public override void OnRemove(StatusEffectContext aContext)
        {
            if (mApplied == null) return;
            aContext.Owner?.ResolveParameter(mParamId)?.RemoveModifier(mApplied);
            mApplied = null;
        }
    }

    // バフ・デバフの対象となるパラメータ
    // 実際のパラメータ ID への対応は ParamId が行う
    public enum PPModifierTargetParam
    {
        [InspectorName(PPParameterEffectCategoryDefinition.NameAttack)]
        Attack = 0,
        [InspectorName(PPParameterEffectCategoryDefinition.NameDefense)]
        Defense = 1,
        [InspectorName(PPParameterEffectCategoryDefinition.NameSpeed)]
        Speed = 2,
        [InspectorName(PPParameterEffectCategoryDefinition.NameHp)]
        Hp = 3,
        // 行動コスト。追加パラメータ側にある
        [InspectorName(PPParameterEffectCategoryDefinition.NameCost)]
        Cost = 4,
        // きようさ（会心率の元）。追加パラメータ側にある。5 は廃止した行動回数上限が使っていた値で、欠番にしている
        // シリアライズ済みの整数値を保つため、値は変えず、要素の追加は末尾に行う
        [InspectorName(PPParameterEffectCategoryDefinition.NameDexterity)]
        Dexterity = 6,
    }

    // パラメータを増減させるバフ・デバフの振る舞い定義。対象パラメータ・方式・変動量を組み合わせる
    // 割合加算（+20% は 0.2）を基本とし、バフ／デバフの別は変動量の符号や大小で表す
    [Serializable]
    [PPTypeMenuName("パラメータ変動")]
    public class PPParameterModifierBehaviourDefinition : PPStatusEffectBehaviourDefinition
    {
        /* インスペクタ設定 */

        [Header("パラメータ変動")]
        [Label("対象パラメータ")]
        [SerializeField]protected PPModifierTargetParam mTargetParam = PPModifierTargetParam.Attack;
        [Label("変動タイプ")]
        [SerializeField]protected ParameterModifierType mModifierType = ParameterModifierType.Add;

        // 加算：正で増加・負で減少。乗算：最終倍率をそのまま入力する（1.5で1.5倍＝バフ、0.5で半分＝デバフ）
        // 割合加算：割合を入力する（0.2で+20%＝バフ、-0.1で-10%＝デバフ）。同じパラメータへの割合は合計してから倍率になる
        [Label("変動量")]
        [SerializeField]protected float mAmount = 10f;

        /* プロパティ */

        public PPModifierTargetParam TargetParam => mTargetParam;
        public ParameterModifierType ModifierType => mModifierType;
        public float Amount => mAmount;

        // 対象パラメータに対応するパラメータ ID
        // コスト・きようさは追加パラメータ側の ID を返す点に注意
        protected string ParamId
            => mTargetParam switch
            {
                PPModifierTargetParam.Attack => ParameterSet.ParamIdAttack,
                PPModifierTargetParam.Defense => ParameterSet.ParamIdDefense,
                PPModifierTargetParam.Speed => ParameterSet.ParamIdSpeed,
                PPModifierTargetParam.Hp => ParameterSet.ParamIdMaxHp,
                PPModifierTargetParam.Cost => PPParameterSet.ParameterIdAttackCost,
                PPModifierTargetParam.Dexterity => PPParameterSet.ParameterIdDexterity,
                _ => string.Empty,
            };

        // 変動量がバフ・デバフのどちら向きかを表示用に判定する
        // 加算・割合加算は符号、乗算は 1 を基準に判定する
        private bool IsBuff
            => mModifierType == ParameterModifierType.Multiply ? mAmount >= 1f : mAmount >= 0f;

        // 変動量の表示用文字列。割合加算は百分率（0.2 → +20%）で表す
        private string AmountText
            => mModifierType == ParameterModifierType.Percent
                ? (mAmount * 100f).ToString("+0.##;-0.##;0") + "%"
                : mAmount.ToString();

        /* 初期化・接続 */

        public override void ConfigureBehaviour(StatusEffect aEffect, BattleUnit aSource, BattleUnit aTarget, BattleContext aContext)
        {
            aEffect.AddBehaviour(new ParameterModifierBehaviour(ParamId, mModifierType, mAmount));
        }

        /* 説明文 */

        public override string BuildString()
            => $"{mTargetParam} {(IsBuff ? "バフ" : "デバフ")}：{AmountText}（{mModifierType}）";
    }
}
