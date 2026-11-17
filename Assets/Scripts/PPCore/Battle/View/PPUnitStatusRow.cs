/* =====================================
 * Copyright WabisabiAndons. All rights reserved.
 * @file PPUnitStatusRow.cs
 * @author hqrse
 * @date 2026/11/18
 * @brief 状態パネルに並ぶユニット 1 体分の行
 * =====================================*/

using System;
using AttributeUtility;
using UnityEngine;
using UnityEngine.UI;

namespace PPCore
{
    // 状態パネルの 1 行。ユニットのアイコン・HP・行動とスキルのゲージ・選択中のスキル・狙う相手を出す
    public class PPUnitStatusRow : MonoBehaviour
    {
        /* インスペクタ設定 */

        // HP・各ゲージ・選択中のスキル名を出す共通ウィジェット
        [Label("ステータスウィジェット")]
        [SerializeField] private PPUnitStatusWidget mStatusWidget;
        [Label("ユニットのアイコン")]
        [SerializeField] private Image mUnitIcon;
        [Label("狙う相手のアイコン")]
        [SerializeField] private Image mTargetIcon;
        // 撃破されたユニットの行を薄くするために使う
        [Label("透明度")]
        [SerializeField] private CanvasGroup mCanvasGroup;
        [Label("撃破後の透明度")]
        [Range(0f, 1f)]
        [SerializeField] private float mDefeatedAlpha = 0.35f;

        /* 状態 */

        private BattleUnit mBattleUnit;
        // ウィジェットへ渡した表示ソース。破棄時に購読を解除するため保持する
        private PPBattleUnitStatusSource mStatusSource;
        // 狙う相手のアイコンを引く関数
        private Func<BattleUnit, Sprite> mIconResolver;

        /* Unity イベント */

        // 破棄時に表示ソースの購読を解除する
        private void OnDestroy()
        {
            if (mStatusWidget != null) mStatusWidget.Unbind();
            ReleaseSource();
        }

        /* 初期化・接続 */

        // 表示するユニットを設定し、値が変わるたびに狙う相手と撃破の表示を更新する
        public void Bind(BattleUnit aUnit, Func<BattleUnit, Sprite> aIconResolver)
        {
            mBattleUnit = aUnit;
            mIconResolver = aIconResolver;
            var icon = aIconResolver?.Invoke(aUnit);
            mUnitIcon.sprite = icon;
            mUnitIcon.enabled = icon != null;

            ReleaseSource();
            mStatusSource = new PPBattleUnitStatusSource(aUnit);
            mStatusWidget.Bind(mStatusSource);
            mStatusSource.Changed += Refresh;
            Refresh();
        }

        // 表示ソースの購読を解除して破棄する
        private void ReleaseSource()
        {
            if (mStatusSource == null) return;
            mStatusSource.Changed -= Refresh;
            mStatusSource.Dispose();
            mStatusSource = null;
        }

        /* 表示 */

        // 狙う相手のアイコンを今の指定に合わせ、撃破されていれば行を薄くする
        private void Refresh()
        {
            var target = (mBattleUnit as PPBattleUnit)?.DesignatedTarget;
            var icon = target != null ? mIconResolver?.Invoke(target) : null;
            mTargetIcon.sprite = icon;
            mTargetIcon.gameObject.SetActive(icon != null);
            if (mCanvasGroup != null) mCanvasGroup.alpha = mBattleUnit.IsAlive ? 1f : mDefeatedAlpha;
        }
    }
}
