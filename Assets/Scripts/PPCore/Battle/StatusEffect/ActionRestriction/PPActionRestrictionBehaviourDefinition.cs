/* =====================================
 * Copyright WabisabiAndons. All rights reserved.
 * @file PPActionRestrictionBehaviourDefinition.cs
 * @author hqrse
 * @date 2026/09/04
 * @brief 行動制限を課す振る舞いのデータ定義
 * =====================================*/

using System;
using CommandBattleCore;
using UnityEngine;
using AttributeUtility;

namespace PPCore
{
    // 掛かっている間、対象へ行動制限を課す振る舞いの定義
    // 「確定で行動不能」なら睡眠、「確率で行動不能」なら麻痺として使える
    // 確率は付与からの経過ターン数で変化させられるため、時間経過で動けるようになる麻痺なども作れる
    //
    // 睡眠のように「行動しようとしたときに一定確率で解ける」挙動は、
    // この振る舞いではなくエフェクト側のスタック消費ルール（行動時＋確率で全消費）で組む
    [Serializable]
    [PPTypeMenuName("行動制限")]
    public class PPActionRestrictionBehaviourDefinition : PPStatusEffectBehaviourDefinition
    {
        [Header("行動制限")]
        [Label("制限内容")]
        [SerializeField]protected ActionRestriction mRestriction = ActionRestriction.CannotAct;
        // false にすると下の確率で抽選する
        [Label("必ず阻害する")]
        [SerializeField]protected bool mIsAlwaysBlock = true;
        // 付与直後（経過ターン 0）の阻害確率
        [Label("基本確率")][Range(0f, 1f)]
        [SerializeField]protected float mBaseChance = 0.5f;
        // 経過ターン 1 つあたりの確率の増分。負の値にすると経つほど動けるようになる
        [Label("1ターンあたりの増分")][Range(-1f, 1f)]
        [SerializeField]protected float mChancePerElapsedTurn = 0f;

        // 表示と実行で扱いを揃えるため、常に 0～1 へ丸めた値を使う
        protected float BaseChance => Mathf.Clamp01(mBaseChance);

        // aEffect : 組み立て先のエフェクト
        // aSource : エフェクトの付与元ユニット
        // aTarget : 付与される対象ユニット
        // aContext : バトルコンテキスト
        public override void ConfigureBehaviour(StatusEffect aEffect, BattleUnit aSource, BattleUnit aTarget, BattleContext aContext)
        {
            aEffect.AddBehaviour(new ActionRestrictionBehaviour(
                mRestriction, mIsAlwaysBlock, BaseChance, mChancePerElapsedTurn));
        }

        public override string BuildString()
        {
            string rate = mIsAlwaysBlock ? "確定" : $"{BaseChance:P0}";
            string curve = mIsAlwaysBlock || Mathf.Approximately(mChancePerElapsedTurn, 0f)
                ? string.Empty
                : $" 毎ターン{mChancePerElapsedTurn:+0.##%;-0.##%}";
            return $"{ToDisplayName(mRestriction)}：{rate}{curve}";
        }

        // 行動制限の日本語表示名を得る
        // 複数フラグの組み合わせは列挙値そのままを返す
        // aRestriction : 対象の行動制限
        // return : 表示名
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
}
