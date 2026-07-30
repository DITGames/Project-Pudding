/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file PPEffectDefinition.cs
 * @author hqrse
 * @date 2026/07/27
 * @brief PPCore固有のエフェクトデータ定義
 * =====================================*/
using CommandBattleCore;
using UnityEngine;

namespace PPCore
{
    // エフェクト定義（ScriptableObject）の抽象基底
    // 状態異常（PPStatusEffectDefinition）とバフデバフ（PPParameterEffectDefinition）に共通する、
    // ID・表示名・持続期間・スタック挙動といった枠組みを定める
    // エフェクト ID はインスペクタでの手入力ではなく BuildAutoEffectId で設定内容から自動生成する
    // 同じ効果に別 ID が付いてスタック判定が働かなくなるのを防ぐため
    public abstract class PPEffectDefinition : ScriptableObject
    {
        // エフェクトID。BuildAutoEffectId により自動設定されるため手入力しない
        [Header("エフェクト")]
        [Label("エフェクトID")]
        [SerializeField]protected string mEffectId;
        [Label("表示名")]
        [SerializeField]protected string mDisplayName;
        [Label("期間")]
        [SerializeField]protected int mDuration = 3;
        [Label("スタックポリシー")]
        [SerializeField]protected StatusEffectStackPolicy mStackPolicy = StatusEffectStackPolicy.Refresh;
        [Label("最大スタック")]
        [SerializeField]protected int mMaxStack = 1;

        public string EffectId => mEffectId;
        public string DisplayName => mDisplayName;
        public int Duration => mDuration;
        public StatusEffectStackPolicy StackPolicy => mStackPolicy;

        // この定義からランタイムのステータスエフェクトを生成する
        // 付与元と対象を受け取るのは、効果量が両者のパラメータに依存しうるため
        // aSource : エフェクトの付与元ユニット
        // aTarget : 付与される対象ユニット
        // aContext : バトルコンテキスト
        // return : 生成されたステータスエフェクト
        public abstract StatusEffect CreateRuntimeStatusEffect(BattleUnit aSource, BattleUnit aTarget, BattleContext aContext);

        // 生成したエフェクトへ固有の効果（継続ダメージ・パラメータ修飾子など）を設定する
        // 共通部分の設定が済んだ後に呼ばれる
        // aEffect : 設定対象のエフェクト
        // aSource : エフェクトの付与元ユニット
        // aTarget : 付与される対象ユニット
        // aContext : バトルコンテキスト
        protected abstract void ConfigureEffect(StatusEffect aEffect, BattleUnit aSource, BattleUnit aTarget, BattleContext aContext);

        // 設定内容からエフェクト ID を組み立てる。派生側で実装する
        // 同じ効果の定義が同じ ID になるようにすること（スタック判定の単位になるため）
        // return : 自動生成されたエフェクトID
        protected abstract string BuildAutoEffectId();

        // インスペクタでの変更時にエフェクト ID を再生成して同期させる
        private void OnValidate()
        {
            var autoId = BuildAutoEffectId();
            mEffectId = autoId;
        }
    }
}
