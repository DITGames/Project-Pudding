/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file PPBattleUnitViewBinder.cs
 * @author hqrse
 * @date 2026/06/25
 * @brief バトルユニット表示のバインディングコンポーネント
 * =====================================*/
using System.Collections.Generic;
using CommandBattleCore;
using UnityEngine;
using UnityEngine.UI;

namespace PPCore
{
    public class PPBattleUnitViewBinder : MonoBehaviour
    {
        [Label("ユニットビュー")]
        [SerializeField] private PPBattleUnitView mUnitViewPrefab;
        [Label("味方表示エリア")]
        [SerializeField] private RectTransform mAllyRow;
        [Label("敵表示エリア")]
        [SerializeField] private RectTransform mEnemyRow;
        [Label("ビジュアルカタログ")]
        [SerializeField] private PPUnitVisualCatalog mUnitVisualCatalog;

        private readonly Dictionary<BattleUnit, PPBattleUnitView> mViews = new();

        public void Bind(BattleManager aManager)
        {
            SpawnViews(aManager.Context.AllyParty, mAllyRow, BattleSide.Ally);
            SpawnViews(aManager.Context.EnemyParty, mEnemyRow, BattleSide.Enemy);

            aManager.OnDamageResolved += (d) =>
            {
                if (d.Amount > 0)
                {
                    mViews.TryGetValue(d.Source, out var view);
                    view?.CommandExecuted(d.SourceAbility as BattleCommandBase);
                }
            };
            aManager.OnDamageTaken += (u, dmg) =>
            {
                mViews.TryGetValue(u, out var view);
                view?.PlayDamage(dmg);
            };
            aManager.OnHealed += (u, hp) =>
            { 
                mViews.TryGetValue(u, out var view);
                view?.PlayHeal(hp);
            };
            aManager.OnUnitDefeated += (u) =>
            {
                mViews.TryGetValue(u, out var view);
                view?.PlayDefeat();
            };
            aManager.OnStatsEffectAdded += (u, e) =>
            {
                mViews.TryGetValue(u, out var view);
                view?.AddStatusIcon(e);
            };
            aManager.OnStatsEffectRemoved += (u, e) =>
            {
                mViews.TryGetValue(u, out var view);
                view?.RemoveStatusIcon(e);
            };
        }

        private void SpawnViews(PPBattleParty aParty, RectTransform aRow, BattleSide aSide)
        {
            if (aRow == null)
            {
                Debug.LogWarning("Row is null");
                return;
            }
            
            foreach (var unit in aParty.ActiveMembers)
            {
                var view = Instantiate(mUnitViewPrefab, aRow);
                var visual = mUnitVisualCatalog.Resolve(unit.UnitId);
                view.Initialize(unit, visual, aSide);
                mViews.Add(unit, view);
            }
            LayoutRebuilder.ForceRebuildLayoutImmediate(aRow);
        }

        private void SpawnViews(BattleParty aParty, RectTransform aRow, BattleSide aSide)
        {
            if (aRow == null)
            {
                Debug.LogWarning("Row is null");
                return;
            }
            
            foreach (var unit in aParty.ActiveMembers)
            {
                var view = Instantiate(mUnitViewPrefab, aRow);
                var visual = mUnitVisualCatalog.Resolve(unit.UnitId);
                view.Initialize(unit, visual, aSide);
                mViews.Add(unit, view);
            }
            LayoutRebuilder.ForceRebuildLayoutImmediate(aRow);
        }
        
        public PPBattleUnitView GetView(BattleUnit aUnit) => mViews.GetValueOrDefault(aUnit);
    }
}