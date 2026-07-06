/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file PPBattleSwapMenuView.cs
 * @author hqrse
 * @date 2026/07/02
 * @brief バトル中の入れ替えメニュー
 * =====================================*/

using System;
using System.Collections.Generic;
using CommandBattleCore;
using UnityEngine;
using UnityEngine.UI;

namespace PPCore
{
    public class PPBattleSwapMenuView : MonoBehaviour
    {
        [Label("キャラクターボタンプレハブ")] [SerializeField] private PPBattleUnitButton mButtonPrefab;
        [Label("レイアウトグループ")][SerializeField] private HorizontalLayoutGroup mLayoutGroup;
        [Label("戻るボタン")] [SerializeField] private Button mBackButton;
        [Label("キャラクターカタログ")] [SerializeField] private PPUnitVisualCatalog mVisualCatalog;

        public event Action<BattleUnit> OnUnitSelected;
        public event Action OnBackRequested;

        private readonly List<PPBattleUnitButton> mUnitButtons = new();

        public void Show(List<BattleUnit> units)
        {
            Clear();
            gameObject.SetActive(true);

            PPBattleUnitButton first = null;
            foreach (var unit in units)
            {
                var btn = Instantiate(mButtonPrefab, mLayoutGroup.transform);
                var src = new PPBattleUnitStatusSource(unit);
                var icon = mVisualCatalog != null
                    ? mVisualCatalog.Resolve(unit.UnitId).UnitIcon
                    : null;
            }
        }

        private void Clear()
        {
            
        }
    }
}