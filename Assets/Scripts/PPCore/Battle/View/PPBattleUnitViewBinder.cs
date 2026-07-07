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
        [Label("味方表示スロット")]
        [SerializeField] private SlotListComponent mAllyAnchorRoot;
        [Label("敵表示スロット")]
        [SerializeField] private SlotListComponent mEnemyAnchorRoot;
        [Label("ビジュアルカタログ")]
        [SerializeField] private PPUnitVisualCatalog mUnitVisualCatalog;

        private readonly Dictionary<BattleUnit, PPBattleUnitView> mViews = new();

        public void Bind(BattleManager aManager)
        {
            SpawnViews(aManager.Context.AllyParty, mAllyAnchorRoot, BattleSide.Ally);
            SpawnViews(aManager.Context.EnemyParty, mEnemyAnchorRoot, BattleSide.Enemy);

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
            aManager.OnUnitSwapped += (o, i) => HandleSwap(o, i);
        }

        private void SpawnViews(PPBattleParty aParty, SlotListComponent aSlotList, BattleSide aSide)
        {
            if (aSlotList == null)
            {
                Debug.LogWarning("SlotList is null");
                return;
            }
            
            int idx = 0;
            foreach (var unit in aParty.ActiveMembers)
            {
                var slot = aSlotList.GetSlot(idx);
                if (slot == null)
                {
                    Debug.LogWarning($"Invalid slot: {idx}");
                    continue;
                }
                var view = Instantiate(mUnitViewPrefab, slot.mTransform);
                slot.AddAttachedObject(view.gameObject);

                var visual = mUnitVisualCatalog.Resolve(unit.UnitId);
                view.Initialize(unit, visual, aSide);
                mViews.Add(unit, view);
                idx++;
            }
        }

        private void SpawnViews(BattleParty aParty, SlotListComponent aSlotList, BattleSide aSide)
        {
            if (aSlotList == null)
            {
                Debug.LogWarning("SlotList is null");
                return;
            }
            
            int idx = 0;
            foreach (var unit in aParty.ActiveMembers)
            {
                var slot = aSlotList.GetSlot(idx);
                if (slot == null)
                {
                    Debug.LogWarning($"Invalid slot: {idx}");
                    continue;
                }
                var view = Instantiate(mUnitViewPrefab, slot.mTransform);
                slot.AddAttachedObject(view.gameObject);

                var visual = mUnitVisualCatalog.Resolve(unit.UnitId);
                view.Initialize(unit, visual, aSide);
                mViews.Add(unit, view);
                idx++;
            }
        }

        private void HandleSwap(BattleUnit aOut, BattleUnit aIn)
        {
            
        }
        
        public PPBattleUnitView GetView(BattleUnit aUnit) => mViews.GetValueOrDefault(aUnit);
    }
}