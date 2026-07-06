/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file PPBattleCommandMenuView.cs
 * @author hqrse
 * @date 2026/07/02
 * @brief バトル中のコマンドメニュー
 * =====================================*/
using System;
using CommandBattleCore;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class PPBattleCommandMenuView : MonoBehaviour
{
    [Label("攻撃")] [SerializeField] private Button mAttackButton;
    [Label("スキル")] [SerializeField] private Button mSkillButton;
    [Label("アイテム")][SerializeField] private Button mItemButton;
    [Label("いれかえ")] [SerializeField] private Button mSwapButton;
    [Label("戻る")][SerializeField] private Button mBackButton;
    
    public event Action OnAttack, OnSkill, OnItem,  OnSwap, OnBackRequested;

    public void Show(bool aCanSkill, bool aCanItem, bool aCanSwap)
    {
        gameObject.SetActive(true);
        mSkillButton.interactable = aCanSkill;
        mItemButton.interactable = aCanItem;
        mSwapButton.interactable = aCanSwap;
        mAttackButton.onClick.AddListener(() => OnAttack?.Invoke());
        mSkillButton.onClick.AddListener(() => OnSkill?.Invoke());
        mItemButton.onClick.AddListener(() => OnItem?.Invoke());
        mSwapButton.onClick.AddListener(() => OnSwap?.Invoke());
        mBackButton.onClick.AddListener(() => OnBackRequested?.Invoke());
        EventSystem.current.SetSelectedGameObject(mAttackButton.gameObject);
    }

    public void Hide()
    {
        foreach(var b in new[]{mAttackButton, mSkillButton, mItemButton, mSwapButton, mBackButton})
            b.onClick.RemoveAllListeners();
        gameObject.SetActive(false);
    }
}
