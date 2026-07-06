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
        [Label("スクロール")] [SerializeField] private ScrollRect mScrollRect;
        [Label("戻るボタン")] [SerializeField] private Button mBackButton;
        [Label("スキルカタログ")] [SerializeField] private PPSkillVisualCatalog mIconCatalog;

        public event Action<BattleSkill> OnSkillSelected;
        public event Action OnBackRequested;

        private readonly List<PPBattleSkillButton> mSkillButtons = new();

        public void Show(BattleUnit aUnit, BattleContext aContext)
        {
            Clear();
            gameObject.SetActive(true);

            var content = mScrollRect.content;
            PPBattleSkillButton firstBtn = null;
            foreach (var skill in aUnit.Skills)
            {
                var btn = Instantiate(mButtonPrefab, content);
                var src = new PPBattleSkillStatusSource(skill, aUnit, aContext);
                var icon = mIconCatalog != null
                    ? mIconCatalog.Resolve(skill.SkillId).SkillIcon
                    : null;
                btn.Setup(skill, src, icon, s => OnSkillSelected?.Invoke(s));
                btn.OnFocused += ScrollIntoView;
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

        // スクロール追従処理
        private void ScrollIntoView(PPBattleSkillButton aButton)
        {
            var viewport = (RectTransform)mScrollRect.viewport;
            var content = mScrollRect.content;
            var item = aButton.Rect;

            Vector3[] vpc = new Vector3[4];
            viewport.GetWorldCorners(vpc);
            Vector3[] itc = new Vector3[4];
            item.GetWorldCorners(itc);
            float vpTop = vpc[1].y, vpBottom = vpc[0].y;
            float itTop = itc[1].y, itBottom = itc[0].y;

            float dy = 0f;
            if(itTop > vpTop) dy = itTop - vpTop;   // 上にはみ出してるので下げる
            else if(itBottom < vpBottom) dy = itBottom - vpBottom;  // 下にはみ出してるので上げる
            if (Mathf.Abs(dy) > 0.01f)
                content.anchoredPosition += new Vector2(0f, -dy);
        }

        private void Clear()
        {
            mBackButton.onClick.RemoveListener(RaiseBack);
            foreach (var btn in mSkillButtons)
            {
                if(btn == null) continue;
                btn.OnFocused -= ScrollIntoView;
                Destroy(btn.gameObject);
            }
            mSkillButtons.Clear();
        }
    }
}