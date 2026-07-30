/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file PPBattleSkillMenuView.cs
 * @author hqrse
 * @date 2026/06/30
 * @brief バトル中のスキル一メニュー
 * =====================================*/
using System;
using System.Collections.Generic;
using CommandBattleCore;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PPCore
{
    /// <summary>
    /// ユニットの所持スキルをボタンとして並べるメニュー。
    /// <para>
    /// 表示するたびにボタンを生成し直し、閉じるときに破棄する。
    /// ユニットごとにスキル数が違うため、使い回さず作り直す方針。
    /// </para>
    /// </summary>
    public class PPBattleSkillMenuView : MonoBehaviour
    {
        /// <summary>複製元のスキルボタン。</summary>
        [Label("スキルボタンプレハブ")]
        [SerializeField] private PPBattleSkillButton mButtonPrefab;
        /// <summary>ボタンを並べる親。</summary>
        [Label("スキルリスト表示領域")]
        [SerializeField] private RectTransform mContent;
        /// <summary>戻るボタン。</summary>
        [Label("戻るボタン")]
        [SerializeField] private Button mBackButton;
        /// <summary>スキル ID からアイコンを引くカタログ。</summary>
        [Label("スキルカタログ")]
        [SerializeField] private PPSkillVisualCatalog mIconCatalog;

        /// <summary>スキルが選択されたときに発火する。</summary>
        public event Action<BattleSkill> OnSkillSelected;
        /// <summary>戻るが押されたときに発火する。</summary>
        public event Action OnBackRequested;

        /// <summary>生成済みのスキルボタン。閉じるときにまとめて破棄する。</summary>
        private readonly List<PPBattleSkillButton> mSkillButtons = new();

        /// <summary>
        /// メニューを指定した位置へ移動させる。レイアウトを崩さないよう worldPositionStays は false。
        /// </summary>
        /// <param name="aAnchor">配置先の親となる RectTransform。</param>
        public void AttachTo(RectTransform aAnchor)
        {
            var rt = (RectTransform)transform;
            rt.SetParent(aAnchor, false);
            rt.anchoredPosition = Vector2.zero;
        }

        /// <summary>
        /// ユニットの所持スキル分のボタンを生成して表示する。
        /// 各ボタンには発動可否を判定できるステータスソースを渡すため、
        /// リソース不足のスキルは自動的に押せない状態になる。
        /// </summary>
        /// <param name="aUnit">スキルを表示する対象ユニット。</param>
        /// <param name="aContext">発動可否の判定に使うバトルコンテキスト。</param>
        public void Show(BattleUnit aUnit, BattleContext aContext)
        {
            if (mContent == null)
            {
                Debug.LogWarning("mContent is null");
                return;
            }

            Clear();
            gameObject.SetActive(true);

            PPBattleSkillButton firstBtn = null;
            foreach (var skill in aUnit.Skills)
            {
                var btn = Instantiate(mButtonPrefab, mContent);
                var src = new PPBattleSkillStatusSource(skill, aUnit, aContext);
                // カタログ未設定、または該当アイコン未登録のどちらもアイコンなしとして扱う
                var icon = mIconCatalog != null
                    ? mIconCatalog.Resolve(skill.SkillId)?.SkillIcon
                    : null;
                btn.Setup(skill, src, icon, s => OnSkillSelected?.Invoke(s));
                mSkillButtons.Add(btn);

                // 初期フォーカス設定
                firstBtn ??= btn;
            }

            // 直後にフォーカスを当てるため、レイアウトの反映を次フレームまで待たない
            LayoutRebuilder.ForceRebuildLayoutImmediate(mContent);

            mBackButton.onClick.AddListener(RaiseBack);

            // 初期フォーカスを設定する(スキルがない場合はBackButtonにフォーカス)
            var focus = firstBtn != null ? firstBtn.FocusTarget : mBackButton.gameObject;
            EventSystem.current.SetSelectedGameObject(focus);
        }

        /// <summary>メニューを閉じ、生成したボタンを破棄する。</summary>
        public void Hide()
        {
            Clear();
            gameObject.SetActive(false);
        }

        /// <summary>戻る操作を外部へ通知する。</summary>
        private void RaiseBack()
        {
            OnBackRequested?.Invoke();
        }

        /// <summary>
        /// 戻るボタンの購読を解除し、生成済みのスキルボタンをすべて破棄する。
        /// 表示のたびに作り直すため、開く前と閉じるときの両方から呼ばれる。
        /// </summary>
        private void Clear()
        {
            mBackButton.onClick.RemoveListener(RaiseBack);
            foreach (var btn in mSkillButtons)
            {
                if(btn == null) continue;
                Destroy(btn.gameObject);
            }
            mSkillButtons.Clear();
        }
    }
}
