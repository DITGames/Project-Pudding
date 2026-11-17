/* =====================================
 * Copyright WabisabiAndons. All rights reserved.
 * @file PPStageUnitViewBinder.cs
 * @author hqrse
 * @date 2026/11/17
 * @brief ステージ上のユニット表示とバトルユニットの対応付け
 * =====================================*/

using System.Collections.Generic;
using AttributeUtility;
using CustomConsole;
using UnityEngine;

namespace PPCore
{
    // ステージの立ち位置にユニットの表示を並べ、BattleManager のイベントを該当する表示の動きへ振り分ける
    // 入力中は、選んでいるユニット以外を暗くして目立たせる
    public class PPStageUnitViewBinder : MonoBehaviour
    {
        /* インスペクタ設定 */

        [Label("ユニット表示のプレハブ")]
        [SerializeField] private PPStageUnitView mUnitViewPrefab;
        // 並び順にアクティブメンバーを立たせる
        [Label("味方の立ち位置", true)]
        [SerializeField] private Transform[] mAllyStandPoints = new Transform[0];
        [Label("敵の立ち位置", true)]
        [SerializeField] private Transform[] mEnemyStandPoints = new Transform[0];
        [Label("ビジュアルカタログ")]
        [SerializeField] private PPUnitVisualCatalog mUnitVisualCatalog;
        // 画面端に味方の HP・ゲージ・選択中のスキル・狙う相手をまとめて出す。未設定なら出さない
        [Label("味方の状態パネル")]
        [SerializeField] private PPPartyStatusPanel mAllyStatusPanel;
        [Label("敵の状態パネル")]
        [SerializeField] private PPPartyStatusPanel mEnemyStatusPanel;

        /* 状態 */

        private readonly Dictionary<BattleUnit, PPStageUnitView> mViews = new();

        /* 初期化・接続 */

        // 両パーティの表示を立ち位置に並べて状態パネルを作り、バトルイベントを購読して動きへ振り分ける
        public void Bind(BattleManager aManager)
        {
            SpawnViews(aManager.Context.AllyParty, mAllyStandPoints, BattleSide.Ally);
            SpawnViews(aManager.Context.EnemyParty, mEnemyStandPoints, BattleSide.Enemy);
            if (mAllyStatusPanel != null) mAllyStatusPanel.Bind(aManager.Context.AllyParty, GetIcon);
            if (mEnemyStatusPanel != null) mEnemyStatusPanel.Bind(aManager.Context.EnemyParty, GetIcon);

            aManager.OnDamageResolved += (d) =>
            {
                // 攻撃元を持たないダメージ(状態異常など)は攻撃の動きの対象にならない
                if (d.Amount > 0 && d.Source != null) GetView(d.Source)?.CommandExecuted((d.Reason as AttackDamageReason)?.Command);
            };
            aManager.OnDamageTaken += (u, dmg) => GetView(u)?.PlayDamage(dmg);
            aManager.OnHealed += (u, hp) => GetView(u)?.PlayHeal(hp);
            aManager.OnUnitDefeated += (u) => GetView(u)?.PlayDefeat();
            aManager.OnStatsEffectAdded += (u, e) => GetView(u)?.AddStatusIcon(e);
            aManager.OnStatsEffectRemoved += (u, e) => GetView(u)?.RemoveStatusIcon(e);
        }

        // アクティブメンバーを立ち位置の順に立たせる。立ち位置が足りないメンバーは表示しない
        private void SpawnViews(BattleParty aParty, Transform[] aStandPoints, BattleSide aSide)
        {
            int index = 0;
            foreach (var unit in aParty.ActiveMembers)
            {
                if (index >= aStandPoints.Length || aStandPoints[index] == null)
                {
                    CustomConsoleLog.Warning("Battle", $"{aSide} の立ち位置が足りないため {unit.UnitId} を表示しません。ステージのバインダーに立ち位置を追加してください", this);
                    index++;
                    continue;
                }
                var view = Instantiate(mUnitViewPrefab, aStandPoints[index].position, Quaternion.identity, aStandPoints[index]);
                view.Initialize(unit, ResolveVisual(unit), aSide);
                mViews.Add(unit, view);
                index++;
            }
        }

        /* 操作 */

        // 指定したユニットだけを元の色にし、ほかを暗くする。null なら全員を暗くする
        public void SetFocus(BattleUnit aFocused)
        {
            foreach (var pair in mViews) pair.Value.SetDimmed(pair.Key != aFocused);
        }

        // 全員を元の色に戻す
        public void ClearFocus()
        {
            foreach (var view in mViews.Values) view.SetDimmed(false);
        }

        /* 問い合わせ */

        public PPStageUnitView GetView(BattleUnit aUnit) => aUnit != null ? mViews.GetValueOrDefault(aUnit) : null;

        // ユニットのアイコンを返す。見た目定義が無ければ null
        public Sprite GetIcon(BattleUnit aUnit) => ResolveVisual(aUnit)?.UnitIcon;

        /* ヘルパー */

        private PPUnitVisualDefinition ResolveVisual(BattleUnit aUnit)
            => mUnitVisualCatalog != null && aUnit != null ? mUnitVisualCatalog.Resolve(aUnit.UnitId) : null;
    }
}
