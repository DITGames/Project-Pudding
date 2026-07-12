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

        public event Action<BattleSkill> OnSkillSelected;
        public event Action OnBackRequested;

        private readonly List<PPBattleSkillButton> mSkillButtons = new();

        public void AttachTo(RectTransform aAnchor)
        {
            var rt = (RectTransform)transform;
            rt.SetParent(aAnchor, false);
            rt.anchoredPosition = Vector2.zero;
        }

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
                var icon = mIconCatalog != null
                    ? mIconCatalog.Resolve(skill.SkillId).SkillIcon
                    : null;
                btn.Setup(skill, src, icon, s => OnSkillSelected?.Invoke(s));
                mSkillButtons.Add(btn);
                
                // 初期フォーカス設定
                firstBtn ??= btn;
            }
            
            LayoutRebuilder.ForceRebuildLayoutImmediate(mContent);
            
            mBackButton.onClick.AddListener(RaiseBack);
            
            // 初期フォーカスを設定する(スキルがない場合はBackButtonにフォーカス)
            var focus = firstBtn != null ? firstBtn.FocusTarget : mBackButton.gameObject;
            EventSystem.current.SetSelectedGameObject(focus);
        }

        public void Hide()
        {
            Clear();
            gameObject.SetActive(false);
        }

        // 戻り処理
        private void RaiseBack()
        {
            OnBackRequested?.Invoke();
        }

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