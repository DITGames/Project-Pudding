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
using AttributeUtility;

namespace PPCore
{
    // スキルメニューに並ぶ 1 項目分のボタン
    // 表示内容は IPPSkillStatusSource から取り、変更通知を購読して
    // コスト表示と押下可否を自動更新する。リソースが溜まった瞬間にボタンが有効化される
    // フォーカスの出入りを ISelectHandler / IDeselectHandler で拾ってイベントとして流すのは、
    // ゲームパッド操作時にリストを自動スクロールさせるため
    public class PPBattleSkillButton : MonoBehaviour, ISelectHandler, IDeselectHandler
    {
        [Label("ボタン")] [SerializeField] private Button mButton;
        // スキルアイコン。設定が無い場合は非表示にする
        [Label("アイコン")] [SerializeField] private Image mIcon;
        [Label("スキル名")] [SerializeField] private TMP_Text mNameLabel;
        [Label("消費コイン")] [SerializeField] private TMP_Text mCostLabel;

        // このボタンが表すスキル
        private BattleSkill mSkill;
        // 表示情報の供給元
        private IPPSkillStatusSource mSource;
        // 決定時に呼ぶコールバック
        private Action<BattleSkill> mOnDecided;

        // 初期フォーカスを当てる対象
        public GameObject FocusTarget => mButton.gameObject;
        // このボタンの RectTransform。スクロール位置の算出に使う
        public RectTransform Rect => (RectTransform)transform;

        // フォーカスが乗ったときの通知(ゲームパッドのリストスクロールに使用)
        public event Action<PPBattleSkillButton> OnSelected;
        // フォーカスが外れたときの通知
        public event Action<PPBattleSkillButton> OnDeselected;

        // ボタンを初期化する。表示内容を流し込み、押下と変更通知を購読して初回描画を行う
        // aSkill : このボタンが表すスキル
        // aSource : 表示情報の供給元
        // aIcon : スキルアイコン。無ければ null
        // aOnSelected : 決定時に呼ぶコールバック
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

        // 押下時にスキルを添えてコールバックを呼ぶ
        private void HandleClick() => mOnDecided?.Invoke(mSkill);

        // フォーカスを得たことを通知する
        public void OnSelect(BaseEventData _) => OnSelected?.Invoke(this);
        // フォーカスを失ったことを通知する
        public void OnDeselect(BaseEventData _) => OnDeselected?.Invoke(this);

        // コスト表示と押下可否を現在の状態へ更新する
        private void Refresh()
        {
            mCostLabel.text = mSource.Cost.ToString();
            mButton.interactable = mSource.IsCastable;
        }

        // 破棄時に購読を解除する
        // 供給元がリソースを購読している場合があるため、そちらの Dispose も通しておく
        private void OnDestroy()
        {
            mButton.onClick.RemoveListener(HandleClick);
            if(mSource != null) mSource.Changed -= Refresh;
            (mSource as IDisposable)?.Dispose();
        }
    }
}
