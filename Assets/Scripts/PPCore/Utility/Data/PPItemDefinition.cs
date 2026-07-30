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
    // アイテム定義（ScriptableObject）の抽象基底
    // スキルと違い、定義アセット自身が IItemEffect を実装して効果本体を兼ねる
    // ランタイム用の別インスタンスは生成しない
    public abstract class PPItemDefinition : ScriptableObject, IItemEffect
    {
        [Header("デフォルト")]
        [Label("アイテムID")] [SerializeField] private string mItemId;
        [Label("表示名")][SerializeField] private string mDisplayName;
        [Label("アイコン")][SerializeField] private Sprite mIcon;
        [Label("対象")][SerializeField] private TargetScope mTarget = TargetScope.SingleAlly;
        // 属性ごとの消費リソース量。インスペクタ設定用の生データ
        // PPResourceCost 自体はシリアライズできないため、
        // スキル定義と同じくエントリ配列で保持して実行時に組み立てる
        [Label("コスト")] [SerializeField] private PPResourceAmount[] mCost;
        // mCost から一度だけ構築するコストのキャッシュ
        private PPResourceCost mCachedCost;

        public string ItemId => mItemId;
        public string DisplayName => mDisplayName;
        public Sprite Icon => mIcon;
        public TargetScope Target => mTarget;
        // 使用に必要なリソースコスト。初回アクセス時に構築してキャッシュする
        public PPResourceCost Cost => mCachedCost ??= PPResourceCost.From(mCost);

        // アイテムの効果を実行する。派生側で実装する
        // aSource : 使用者
        // aTargets : 解決済みの対象リスト
        // aContext : バトルコンテキスト
        public abstract void Use(BattleUnit aSource, List<BattleUnit> aTargets, BattleContext aContext);
    }
}
