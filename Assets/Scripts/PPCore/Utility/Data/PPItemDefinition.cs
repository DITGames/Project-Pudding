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
        /// <summary>
        /// 属性ごとの消費リソース量。インスペクタ設定用の生データ。
        /// <see cref="PPResourceCost"/> 自体はシリアライズできないため、
        /// スキル定義と同じくエントリ配列で保持して実行時に組み立てる。
        /// </summary>
        [Label("コスト")] [SerializeField] private PPResourceAmount[] mCost;
        /// <summary><see cref="mCost"/> から一度だけ構築するコストのキャッシュ。</summary>
        private PPResourceCost mCachedCost;

        /// <summary>アイテムID。</summary>
        public string ItemId => mItemId;
        /// <summary>UI 表示名。</summary>
        public string DisplayName => mDisplayName;
        /// <summary>アイコン。</summary>
        public Sprite Icon => mIcon;
        /// <summary>効果の対象範囲。</summary>
        public TargetScope Target => mTarget;
        /// <summary>使用に必要なリソースコスト。初回アクセス時に構築してキャッシュする。</summary>
        public PPResourceCost Cost => mCachedCost ??= PPResourceCost.From(mCost);

        /// <summary>
        /// アイテムの効果を実行する。派生側で実装する。
        /// </summary>
        /// <param name="aSource">使用者。</param>
        /// <param name="aTargets">解決済みの対象リスト。</param>
        /// <param name="aContext">バトルコンテキスト。</param>
        public abstract void Use(BattleUnit aSource, List<BattleUnit> aTargets, BattleContext aContext);
    }
}
