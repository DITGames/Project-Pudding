/* =====================================
 * Copyright WabisabiAndons. All rights reserved.
 * @file PPStageUnitViewBinder.cs
 * @author hqrse
 * @date 2026/11/17
 * @brief バトル画面のユニット表示とバトルユニットの対応付け
 * =====================================*/

using System.Collections.Generic;
using AttributeUtility;
using CustomConsole;
using UnityEngine;

namespace PPCore
{
    // バトル画面の道の上にユニットの表示を出し、BattleManager のイベントを該当する表示の動きへ振り分ける
    public class PPStageUnitViewBinder : MonoBehaviour
    {
        /* インスペクタ設定 */

        [Label("ユニット表示のプレハブ")]
        [SerializeField] private PPStageUnitView mUnitViewPrefab;
        // 道の上に生まれたユニットを置き、移動に合わせて動かす
        [Label("バトルの道")]
        [SerializeField] private PPBattleField mField;
        // 未設定なら、このオブジェクトの下に置く
        [Label("ユニット表示を置く親")]
        [SerializeField] private Transform mUnitRoot;
        [Label("ビジュアルカタログ")]
        [SerializeField] private PPUnitVisualCatalog mUnitVisualCatalog;

        /* 状態 */

        private readonly Dictionary<BattleUnit, PPStageUnitView> mViews = new();
        // 道の上のユニットと、その表示。位置を毎フレーム表示へ写す
        private readonly List<(PPFieldUnit FieldUnit, PPStageUnitView View)> mFieldViews = new();
        // これまでに出した表示の数。同じレイヤー優先度どうしの前後に使う
        private int mSpawnCount;

        /* Unity イベント */

        // 道の上のユニットの位置を表示へ写す
        private void LateUpdate()
        {
            if (mField == null) return;
            for (int i = mFieldViews.Count - 1; i >= 0; i--)
            {
                var (fieldUnit, view) = mFieldViews[i];
                // 撃破後に消した表示は追いかける対象から外す
                if (view == null)
                {
                    mFieldViews.RemoveAt(i);
                    continue;
                }
                view.transform.position = mField.GetPointAtDistance(fieldUnit.Position);
            }
        }

        /* 初期化・接続 */

        // バトルイベントを購読し、該当する表示の動きへ振り分ける。道の上に生まれたユニットは表示を出す
        public void Bind(BattleManager aManager)
        {
            aManager.OnFieldUnitSpawned += SpawnFieldView;
            aManager.OnDamageResolved += (d) =>
            {
                // 攻撃元を持たないダメージ(状態異常など)は攻撃の動きの対象にならない
                if (d.Amount > 0 && d.Source != null) GetView(d.Source)?.CommandExecuted((d.Reason as AttackDamageReason)?.Command);
            };
            aManager.OnDamageTaken += (u, dmg) => GetView(u)?.PlayDamage(dmg);
            aManager.OnHealed += (u, hp) => GetView(u)?.PlayHeal(hp);
            aManager.OnUnitDefeated += HandleDefeated;
            aManager.OnStatsEffectAdded += (u, e) => GetView(u)?.AddStatusIcon(e);
            aManager.OnStatsEffectRemoved += (u, e) => GetView(u)?.RemoveStatusIcon(e);
        }

        /* 操作 */

        // ユニットの表示を道の上の aPosition に出す。既に出ているユニットなら、その表示を返す
        public PPStageUnitView SpawnView(BattleUnit aUnit, BattleSide aSide, Vector3 aPosition)
        {
            if (mViews.TryGetValue(aUnit, out var existing)) return existing;
            if (mUnitViewPrefab == null)
            {
                CustomConsoleLog.Error("Battle", $"ユニット表示のプレハブが未設定のため {aUnit.UnitId} を表示できません。PPStageUnitViewBinder の「ユニット表示のプレハブ」を割り当ててください", this);
                return null;
            }
            var parent = mUnitRoot != null ? mUnitRoot : transform;
            var view = Instantiate(mUnitViewPrefab, aPosition, Quaternion.identity, parent);
            view.Initialize(aUnit, ResolveVisual(aUnit), aSide, mSpawnCount);
            mSpawnCount++;
            mViews.Add(aUnit, view);
            return view;
        }

        // 撃破の動きを再生し、道の上のユニットなら動きが終わってから表示を消す
        private void HandleDefeated(BattleUnit aUnit)
        {
            var view = GetView(aUnit);
            if (view == null) return;
            view.PlayDefeat();
            if (!IsFieldView(view)) return;
            mViews.Remove(aUnit);
            Destroy(view.gameObject, view.DefeatDuration + 0.1f);
        }

        // 道の上に生まれたユニットの表示を出し、位置を追いかける対象に加える
        private void SpawnFieldView(PPFieldUnit aFieldUnit)
        {
            if (mField == null)
            {
                CustomConsoleLog.Warning("Battle", $"バトルの道が未設定のため {aFieldUnit.Unit.UnitId} を表示できません。PPStageUnitViewBinder の「バトルの道」を割り当ててください", this);
                return;
            }
            var view = SpawnView(aFieldUnit.Unit, aFieldUnit.Side, mField.GetPointAtDistance(aFieldUnit.Position));
            if (view != null) mFieldViews.Add((aFieldUnit, view));
        }

        /* 問い合わせ */

        public PPStageUnitView GetView(BattleUnit aUnit) => aUnit != null ? mViews.GetValueOrDefault(aUnit) : null;

        private bool IsFieldView(PPStageUnitView aView)
        {
            foreach (var (_, view) in mFieldViews)
            {
                if (view == aView) return true;
            }
            return false;
        }

        /* ヘルパー */

        private PPUnitVisualDefinition ResolveVisual(BattleUnit aUnit)
            => mUnitVisualCatalog != null && aUnit != null ? mUnitVisualCatalog.Resolve(aUnit.UnitId) : null;
    }
}
