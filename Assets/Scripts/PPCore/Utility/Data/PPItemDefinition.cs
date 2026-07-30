/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file PPItemDefinition.cs
 * @author hqrse
 * @date 2026/07/02
 * @brief アイテム定義
 * =====================================*/
using System.Collections.Generic;
using CommandBattleCore;
using UnityEngine;

namespace PPCore
{
    /// <summary>
    /// アイテム定義（ScriptableObject）の抽象基底。
    /// <para>
    /// スキルと違い、定義アセット自身が <see cref="IItemEffect"/> を実装して
    /// 効果本体を兼ねる。ランタイム用の別インスタンスは生成しない。
    /// </para>
    /// </summary>
    public abstract class PPItemDefinition : ScriptableObject, IItemEffect
    {
        /// <summary>アイテムID。</summary>
        [Header("デフォルト")]
        [Label("アイテムID")] [SerializeField] private string mItemId;
        /// <summary>UI 表示名。</summary>
        [Label("表示名")][SerializeField] private string mDisplayName;
        /// <summary>UI に出すアイコン。</summary>
        [Label("アイコン")][SerializeField] private Sprite mIcon;
        /// <summary>効果の対象範囲。</summary>
        [Label("対象")][SerializeField] private TargetScope mTarget = TargetScope.SingleAlly;
        /// <summary>使用に必要なリソースコスト。</summary>
        /// <remarks>
        /// 既知の未整理箇所: <see cref="PPResourceCost"/> は <c>[Serializable]</c> を持たず
        /// コンストラクタも private なため、Unity のシリアライズ対象にならない。
        /// このフィールドは常に null のままで、実質アイテムのコストは機能していない。
        /// スキル側と同様に <see cref="PPResourceAmount"/> の配列で保持し、
        /// <see cref="PPResourceCost.From"/> で組み立てる形にする必要がある。
        /// </remarks>
        [Label("コスト")] [SerializeField] private PPResourceCost mCost;

        /// <summary>アイテムID。</summary>
        public string ItemId => mItemId;
        /// <summary>UI 表示名。</summary>
        public string DisplayName => mDisplayName;
        /// <summary>アイコン。</summary>
        public Sprite Icon => mIcon;
        /// <summary>効果の対象範囲。</summary>
        public TargetScope Target => mTarget;
        /// <summary>使用に必要なリソースコスト。上記の理由により現状は常に null。</summary>
        public PPResourceCost Cost => mCost;

        /// <summary>
        /// アイテムの効果を実行する。派生側で実装する。
        /// </summary>
        /// <param name="aSource">使用者。</param>
        /// <param name="aTargets">解決済みの対象リスト。</param>
        /// <param name="aContext">バトルコンテキスト。</param>
        public abstract void Use(BattleUnit aSource, List<BattleUnit> aTargets, BattleContext aContext);
    }
}
