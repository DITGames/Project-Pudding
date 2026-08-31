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
using AttributeUtility;

namespace PPCore
{
    // ユニットの所持スキルをボタンとして並べるメニュー
    // 表示するたびにボタンを生成し直し、閉じるときに破棄する
    // ユニットごとにスキル数が違うため、使い回さず作り直す方針
    public class PPBattleSkillMenuView : MonoBehaviour
    {
        [Label("スキルボタンプレハブ")]
        [SerializeField] private PPBattleSkillButton mButtonPrefab;
        [Label("スキルリスト表示領域")]
        [SerializeField] private RectTransform mContent;
        [Label("戻るボタン")]
        [SerializeField] private Button mBackButton;
        [Label("スキルカタログ")]
        [SerializeField] private PPSkillVisualCatalog mIconCatalog;

        // スキルが選択されたときに発火する
        public event Action<BattleSkill> OnSkillSelected;
        // 戻るが押されたときに発火する
        public event Action OnBackRequested;

        // 生成済みのスキルボタン。閉じるときにまとめて破棄する
        private readonly List<PPBattleSkillButton> mSkillButtons = new();

        // メニューを指定した位置へ移動させる。レイアウトを崩さないよう worldPositionStays は false
        // aAnchor : 配置先の親となる RectTransform
        public void AttachTo(RectTransform aAnchor)
        {
            var rt = (RectTransform)transform;
            rt.SetParent(aAnchor, false);
            rt.anchoredPosition = Vector2.zero;
        }

        // ユニットの所持スキル分のボタンを生成して表示する
        // 各ボタンには発動可否を判定できるステータスソースを渡すため、
        // リソース不足のスキルは自動的に押せない状態になる
        // aUnit : スキルを表示する対象ユニット
        // aContext : 発動可否の判定に使うバトルコンテキスト
        public void Show(BattleUnit aUnit, BattleContext aContext, PPUnitActionLedger aLedger = null)
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
                var src = new PPBattleSkillStatusSource(skill, aUnit, aContext, aLedger);
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

        // メニューを閉じ、生成したボタンを破棄する
        public void Hide()
        {
            Clear();
            gameObject.SetActive(false);
        }

        // 戻る操作を外部へ通知する
        private void RaiseBack()
        {
            OnBackRequested?.Invoke();
        }

        // 戻るボタンの購読を解除し、生成済みのスキルボタンをすべて破棄する
        // 表示のたびに作り直すため、開く前と閉じるときの両方から呼ばれる
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
