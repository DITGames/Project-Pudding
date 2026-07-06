/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file PPBattleItemMenuView.cs
 * @author hqrse
 * @date 2026/07/02
 * @brief バトル中のアイテムメニュー
 * =====================================*/

using System;
using System.Collections.Generic;
using CommandBattleCore;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PPCore
{
    public class PPBattleItemMenuView : MonoBehaviour
    {
        [Label("スキルボタンプレハブ")] [SerializeField] private PPBattleItemButton mButtonPrefab;
        [Label("スクロール")] [SerializeField] private ScrollRect mScrollRect;
        [Label("戻るボタン")] [SerializeField] private Button mBackButton;
        
        public event Action<PPItemDefinition> OnItemSelected;
        public event Action OnBackRequested;

        private readonly List<PPBattleItemButton> mItemButtons = new();

        public void Show(PPBattleParty aParty, BattleContext aContext)
        {
            Clear();
            gameObject.SetActive(true);
            var content = mScrollRect.content;
            PPBattleItemButton first = null;
            foreach (var def in aParty.Inventory.UsableItems())
            {
                var btn = Instantiate(mButtonPrefab, content);
                var src = new PPItemStatusSource(def, aParty);
                btn.Setup(def, src, def.Icon, d => OnItemSelected?.Invoke(d));
                btn.OnFocused += ScrollIntoView;
                mItemButtons.Add(btn);
                first ??= btn;
            }
            mBackButton.onClick.AddListener(RaiseBack);
            EventSystem.current.SetSelectedGameObject(first != null ? first.FocusTarget : mBackButton.gameObject);
        }

        public void Hide()
        {
            Clear();
            gameObject.SetActive(false);
        }

        private void RaiseBack()
        {
            OnBackRequested?.Invoke();
        }

        private void ScrollIntoView(PPBattleItemButton aButton)
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
            foreach (var btn in mItemButtons)
            {
                if (btn == null) continue;
                btn.OnFocused -= ScrollIntoView;
                Destroy(btn.gameObject);
            }
            mItemButtons.Clear();
        }
    }
}