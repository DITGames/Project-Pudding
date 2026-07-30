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
    /// <summary>
    /// <see cref="BattleUnit"/> と <see cref="PPBattleUnitView"/> を対応付ける橋渡し役。
    /// <para>
    /// 担うのは 2 つ。パーティのメンバー分だけビューを生成して辞書に登録することと、
    /// <see cref="BattleManager"/> のイベントを購読して該当ユニットのビューへ演出を振り分けること。
    /// これにより、バトルロジック側はビューの存在を知らないまま演出が動く。
    /// </para>
    /// <para>
    /// 入力ステートが対象ユニットのビューを引く際も、この <see cref="GetView"/> を使う。
    /// </para>
    /// </summary>
    public class PPBattleUnitViewBinder : MonoBehaviour
    {
        /// <summary>複製元のユニットビュー。</summary>
        [Label("ユニットビュー")]
        [SerializeField] private PPBattleUnitView mUnitViewPrefab;
        /// <summary>味方ビューを並べる親。</summary>
        [Label("味方表示エリア")]
        [SerializeField] private RectTransform mAllyRow;
        /// <summary>敵ビューを並べる親。</summary>
        [Label("敵表示エリア")]
        [SerializeField] private RectTransform mEnemyRow;
        /// <summary>ユニット ID から見た目定義を引くカタログ。</summary>
        [Label("ビジュアルカタログ")]
        [SerializeField] private PPUnitVisualCatalog mUnitVisualCatalog;

        /// <summary>ユニットとビューの対応表。</summary>
        private readonly Dictionary<BattleUnit, PPBattleUnitView> mViews = new();

        /// <summary>
        /// 両パーティのビューを生成し、バトルイベントを購読して演出へ振り分ける。
        /// </summary>
        /// <param name="aManager">購読対象のバトルマネージャ。</param>
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

        /// <summary>
        /// パーティのアクティブメンバー分のビューを生成して並べ、対応表へ登録する。
        /// 生成直後は選択不可にしておき、入力ステートが必要なタイミングで有効化する。
        /// </summary>
        /// <param name="aParty">対象のパーティ。</param>
        /// <param name="aRow">ビューを並べる親。</param>
        /// <param name="aSide">このパーティの陣営。ビューの向きの決定に使う。</param>
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

        /// <summary>
        /// ユニットに対応するビューを取得する。
        /// </summary>
        /// <param name="aUnit">対象のユニット。</param>
        /// <returns>対応するビュー。未登録なら null。</returns>
        public PPBattleUnitView GetView(BattleUnit aUnit) => mViews.GetValueOrDefault(aUnit);
    }
}
