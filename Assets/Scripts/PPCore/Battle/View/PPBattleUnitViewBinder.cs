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
    // BattleUnit と PPBattleUnitView を対応付ける橋渡し役
    // 担うのは 2 つ。パーティのメンバー分だけビューを生成して辞書に登録することと、
    // BattleManager のイベントを購読して該当ユニットのビューへ演出を振り分けること
    // これにより、バトルロジック側はビューの存在を知らないまま演出が動く
    // 入力ステートが対象ユニットのビューを引く際も、この GetView を使う
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

        // ユニットとビューの対応表
        private readonly Dictionary<BattleUnit, PPBattleUnitView> mViews = new();

        // 両パーティのビューを生成し、バトルイベントを購読して演出へ振り分ける
        // aManager : 購読対象のバトルマネージャ
        public void Bind(BattleManager aManager)
        {
            SpawnViews(aManager.Context.AllyParty, mAllyRow, BattleSide.Ally);
            SpawnViews(aManager.Context.EnemyParty, mEnemyRow, BattleSide.Enemy);

            aManager.OnDamageResolved += (d) =>
            {
                // 攻撃元を持たないダメージ(状態異常など)は攻撃モーションの対象にならない。
                // null を辞書の検索キーに渡すと例外になるため先に弾く
                if (d.Amount > 0 && d.Source != null)
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

        // パーティのアクティブメンバー分のビューを生成して並べ、対応表へ登録する
        // 生成直後は選択不可にしておき、入力ステートが必要なタイミングで有効化する
        // aParty : 対象のパーティ
        // aRow : ビューを並べる親
        // aSide : このパーティの陣営。ビューの向きの決定に使う
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
                view.SetSelectable(false);
                mViews.Add(unit, view);
            }
            // 直後にアンカー位置を参照するため、レイアウトの反映を次フレームまで待たない
            LayoutRebuilder.ForceRebuildLayoutImmediate(aRow);
        }

        // ユニットに対応するビューを取得する
        // aUnit : 対象のユニット
        // return : 対応するビュー。未登録なら null
        public PPBattleUnitView GetView(BattleUnit aUnit) => mViews.GetValueOrDefault(aUnit);
    }
}
