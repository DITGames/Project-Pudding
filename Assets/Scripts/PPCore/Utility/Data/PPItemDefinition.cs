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
    public abstract class PPItemDefinition : ScriptableObject, IItemEffect
    {
        [Header("デフォルト")]
        [Label("アイテムID")] [SerializeField] private string mItemId;
        [Label("表示名")][SerializeField] private string mDisplayName;
        [Label("アイコン")][SerializeField] private Sprite mIcon;
        [Label("対象")][SerializeField] private TargetScope mTarget = TargetScope.SingleAlly;
        [Label("消費コイン")] [SerializeField] private int mCost;
        
        public string ItemId => mItemId;
        public string DisplayName => mDisplayName;
        public Sprite Icon => mIcon;
        public TargetScope Target => mTarget;
        public int Cost => mCost;
        
        public abstract void Use(BattleUnit aSource, List<BattleUnit> aTargets, BattleContext aContext);
    }
}