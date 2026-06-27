/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file PPBattleUnitViewBinder.cs
 * @author hqrse
 * @date 2026/06/25
 * @brief PPバトルユニット表示のバインディングコンポーネント
 * =====================================*/

using System.Collections.Generic;
using CommandBattleCore;
using PPCore;
using UnityEngine;

namespace PPCore
{
    public class PPBattleUnitViewBinder : MonoBehaviour
    {
        [Label("ユニットビュー")]
        [SerializeField] private PPBattleUnitView mUnitViewPrefab;
        [Label("味方表示ルート")]
        [SerializeField] private Transform mAllyAnchorRoot;
        [Label("敵表示ルート")]
        [SerializeField] private Transform mEnemyAnchorRoot;
        [Label("ビジュアルカタログ")]
        [SerializeField] private PPUnitVisualCatalog mUnitVisualCatalog;

        private readonly Dictionary<BattleUnit, PPBattleUnitView> mViews = new();

        public void Bind(BattleManager aManager)
        {
            SpawnViews(aManager.Context.AllyParty, mAllyAnchorRoot, BattleSide.Ally);
            SpawnViews(aManager.Context.EnemyParty, mEnemyAnchorRoot, BattleSide.Enemy);

            aManager.OnCommandExecuted += (u, command) =>
            {
                mViews.TryGetValue(u, out var view);
                view?.CommandExecuted(command);
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
            aManager.OnUnitSwapped += (o, i) => HandleSwap(o, i);
        }

        private void SpawnViews(PPBattleParty aParty, Transform aRoot, BattleSide aSide)
        {
            foreach (var unit in aParty.ActiveMembers)
            {
                var view = Instantiate(mUnitViewPrefab, aRoot);

                var visual = mUnitVisualCatalog.Resolve(unit.UnitId);
                view.Initialize(unit, visual, aSide);
                mViews.Add(unit, view);
            }
        }

        private void SpawnViews(BattleParty aParty, Transform aRoot, BattleSide aSide)
        {
            foreach (var unit in aParty.ActiveMembers)
            {
                var view = Instantiate(mUnitViewPrefab, aRoot);

                var visual = mUnitVisualCatalog.Resolve(unit.UnitId);
                view.Initialize(unit, visual, aSide);
                mViews.Add(unit, view);
            }
        }

        private void HandleSwap(BattleUnit aOut, BattleUnit aIn)
        {
            
        }
        
        public PPBattleUnitView GetView(BattleUnit aUnit) => mViews.GetValueOrDefault(aUnit);
    }
}