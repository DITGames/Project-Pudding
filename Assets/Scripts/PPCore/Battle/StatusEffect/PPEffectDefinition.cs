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
    /// <summary>
    /// エフェクト定義（ScriptableObject）の抽象基底。
    /// <para>
    /// 状態異常（<see cref="PPStatusEffectDefinition"/>）とバフデバフ
    /// （<see cref="PPParameterEffectDefinition"/>）に共通する、
    /// ID・表示名・持続期間・スタック挙動といった枠組みを定める。
    /// </para>
    /// <para>
    /// エフェクト ID はインスペクタでの手入力ではなく <see cref="BuildAutoEffectId"/> で
    /// 設定内容から自動生成する。同じ効果に別 ID が付いてスタック判定が働かなくなるのを防ぐため。
    /// </para>
    /// </summary>
    public abstract class PPEffectDefinition : ScriptableObject
    {
        /// <summary>エフェクトID。<see cref="BuildAutoEffectId"/> により自動設定されるため手入力しない。</summary>
        [Header("エフェクト")]
        [Label("エフェクトID")]
        [SerializeField]protected string mEffectId;
        /// <summary>UI 表示名。</summary>
        [Label("表示名")]
        [SerializeField]protected string mDisplayName;
        /// <summary>持続ターン数。</summary>
        [Label("期間")]
        [SerializeField]protected int mDuration = 3;
        /// <summary>同一 ID が重ねて付与されたときの挙動。</summary>
        [Label("スタックポリシー")]
        [SerializeField]protected StatusEffectStackPolicy mStackPolicy = StatusEffectStackPolicy.Refresh;
        /// <summary>スタック数の上限。</summary>
        [Label("最大スタック")]
        [SerializeField]protected int mMaxStack = 1;

        /// <summary>エフェクトID。</summary>
        public string EffectId => mEffectId;
        /// <summary>UI 表示名。</summary>
        public string DisplayName => mDisplayName;
        /// <summary>持続ターン数。</summary>
        public int Duration => mDuration;
        /// <summary>スタックポリシー。</summary>
        public StatusEffectStackPolicy StackPolicy => mStackPolicy;

        /// <summary>
        /// この定義からランタイムのステータスエフェクトを生成する。
        /// 付与元と対象を受け取るのは、効果量が両者のパラメータに依存しうるため。
        /// </summary>
        /// <param name="aSource">エフェクトの付与元ユニット。</param>
        /// <param name="aTarget">付与される対象ユニット。</param>
        /// <param name="aContext">バトルコンテキスト。</param>
        /// <returns>生成されたステータスエフェクト。</returns>
        public abstract StatusEffect CreateRuntimeStatusEffect(BattleUnit aSource, BattleUnit aTarget, BattleContext aContext);

        /// <summary>
        /// 生成したエフェクトへ固有の効果（継続ダメージ・パラメータ修飾子など）を設定する。
        /// 共通部分の設定が済んだ後に呼ばれる。
        /// </summary>
        /// <param name="aEffect">設定対象のエフェクト。</param>
        /// <param name="aSource">エフェクトの付与元ユニット。</param>
        /// <param name="aTarget">付与される対象ユニット。</param>
        /// <param name="aContext">バトルコンテキスト。</param>
        protected abstract void ConfigureEffect(StatusEffect aEffect, BattleUnit aSource, BattleUnit aTarget, BattleContext aContext);

        /// <summary>
        /// 設定内容からエフェクト ID を組み立てる。派生側で実装する。
        /// 同じ効果の定義が同じ ID になるようにすること（スタック判定の単位になるため）。
        /// </summary>
        /// <returns>自動生成されたエフェクトID。</returns>
        protected abstract string BuildAutoEffectId();

        /// <summary>
        /// インスペクタでの変更時にエフェクト ID を再生成して同期させる。
        /// </summary>
        private void OnValidate()
        {
            var autoId = BuildAutoEffectId();
            mEffectId = autoId;
        }
    }
}
