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
        [Label("スキルボタンプレハブ")] [SerializeField] private PPBattleSkillButton mButtonPrefab;
        [Label("スクロール")] [SerializeField] private SlotListComponent mSlotList;
        [Label("戻るボタン")] [SerializeField] private Button mBackButton;
        [Label("スキルカタログ")] [SerializeField] private PPSkillVisualCatalog mIconCatalog;

        public event Action<BattleSkill> OnSkillSelected;
        public event Action OnBackRequested;

        private readonly List<PPBattleSkillButton> mSkillButtons = new();

        public void Show(BattleUnit aUnit, BattleContext aContext)
        {
            if (mSlotList == null)
            {
                Debug.LogWarning("mSlotList is null");
                return;
            }
            
            Clear();
            gameObject.SetActive(true);
            
            PPBattleSkillButton firstBtn = null;
            int idx = 0;
            foreach (var skill in aUnit.Skills)
            {
                var slot = mSlotList.GetSlot(idx);
                if (slot == null)
                {
                    Debug.LogWarning($"Invalid slot: {idx}");
                    continue;
                }
                var btn = Instantiate(mButtonPrefab, slot.mTransform);
                var src = new PPBattleSkillStatusSource(skill, aUnit, aContext);
                var icon = mIconCatalog != null
                    ? mIconCatalog.Resolve(skill.SkillId).SkillIcon
                    : null;
                btn.Setup(skill, src, icon, s => OnSkillSelected?.Invoke(s));
                mSkillButtons.Add(btn);
                
                // 初期フォーカス設定
                firstBtn = firstBtn == null 
                    ? btn
                    : firstBtn;
            }
            
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