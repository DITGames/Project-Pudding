/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file PPBattleSkillButton.cs
 * @author hqrse
 * @date 2026/06/30
 * @brief スキルボタンコンポーネント
 * =====================================*/
using System;
using CommandBattleCore;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PPCore
{
    /// <summary>
    /// スキルメニューに並ぶ 1 項目分のボタン。
    /// <para>
    /// 表示内容は <see cref="IPPSkillStatusSource"/> から取り、変更通知を購読して
    /// コスト表示と押下可否を自動更新する。リソースが溜まった瞬間にボタンが有効化される。
    /// </para>
    /// <para>
    /// フォーカスの出入りを <see cref="ISelectHandler"/> / <see cref="IDeselectHandler"/> で拾って
    /// イベントとして流すのは、ゲームパッド操作時にリストを自動スクロールさせるため。
    /// </para>
    /// </summary>
    public class PPBattleSkillButton : MonoBehaviour, ISelectHandler, IDeselectHandler
    {
        /// <summary>押下を受け付けるボタン。</summary>
        [Label("ボタン")] [SerializeField] private Button mButton;
        /// <summary>スキルアイコン。設定が無い場合は非表示にする。</summary>
        [Label("アイコン")] [SerializeField] private Image mIcon;
        /// <summary>スキル名ラベル。</summary>
        [Label("スキル名")] [SerializeField] private TMP_Text mNameLabel;
        /// <summary>消費コインの表示ラベル。</summary>
        [Label("消費コイン")] [SerializeField] private TMP_Text mCostLabel;

        /// <summary>このボタンが表すスキル。</summary>
        private BattleSkill mSkill;
        /// <summary>表示情報の供給元。</summary>
        private IPPSkillStatusSource mSource;
        /// <summary>決定時に呼ぶコールバック。</summary>
        private Action<BattleSkill> mOnDecided;

        /// <summary>初期フォーカスを当てる対象。</summary>
        public GameObject FocusTarget => mButton.gameObject;
        /// <summary>このボタンの RectTransform。スクロール位置の算出に使う。</summary>
        public RectTransform Rect => (RectTransform)transform;

        /// <summary>フォーカスが乗ったときの通知(ゲームパッドのリストスクロールに使用)</summary>
        public event Action<PPBattleSkillButton> OnSelected;
        /// <summary>フォーカスが外れたときの通知</summary>
        public event Action<PPBattleSkillButton> OnDeselected;

        /// <summary>
        /// ボタンを初期化する。表示内容を流し込み、押下と変更通知を購読して初回描画を行う。
        /// </summary>
        /// <param name="aSkill">このボタンが表すスキル。</param>
        /// <param name="aSource">表示情報の供給元。</param>
        /// <param name="aIcon">スキルアイコン。無ければ null。</param>
        /// <param name="aOnSelected">決定時に呼ぶコールバック。</param>
        public void Setup(BattleSkill aSkill, IPPSkillStatusSource aSource, Sprite aIcon,
            Action<BattleSkill> aOnSelected)
        {
            mSkill = aSkill;
            mSource = aSource;
            mOnDecided = aOnSelected;

            if (mIcon != null)
            {
                mIcon.enabled = aIcon != null;
                mIcon.sprite = aIcon;
            }
            mNameLabel.text = aSource.DisplayName;

            mButton.onClick.AddListener(HandleClick);
            mSource.Changed += Refresh;
            Refresh();
        }

        /// <summary>押下時にスキルを添えてコールバックを呼ぶ。</summary>
        private void HandleClick() => mOnDecided?.Invoke(mSkill);

        /// <summary>フォーカスを得たことを通知する。</summary>
        public void OnSelect(BaseEventData _) => OnSelected?.Invoke(this);
        /// <summary>フォーカスを失ったことを通知する。</summary>
        public void OnDeselect(BaseEventData _) => OnDeselected?.Invoke(this);

        /// <summary>コスト表示と押下可否を現在の状態へ更新する。</summary>
        private void Refresh()
        {
            mCostLabel.text = mSource.Cost.ToString();
            mButton.interactable = mSource.IsCastable;
        }

        /// <summary>
        /// 破棄時に購読を解除する。
        /// 供給元がリソースを購読している場合があるため、そちらの Dispose も通しておく。
        /// </summary>
        private void OnDestroy()
        {
            mButton.onClick.RemoveListener(HandleClick);
            if(mSource != null) mSource.Changed -= Refresh;
            (mSource as IDisposable)?.Dispose();
        }
    }
}
