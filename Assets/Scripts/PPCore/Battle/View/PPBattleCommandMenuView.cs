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
    [Label("攻撃")]
    [SerializeField] private Button mAttackButton;
    [Label("スキル")] 
    [SerializeField] private Button mSkillButton;
    [Label("詳細")]
    [SerializeField] private Button mDetailButton;
    [Label("戻る")]
    [SerializeField] private Button mBackButton;
    
    public event Action OnAttack, OnSkill, OnDetail, OnBackRequested;

    public void AttachTo(RectTransform aAnchor)
    {
        var rt = (RectTransform)transform;
        rt.SetParent(aAnchor);
        rt.anchoredPosition = Vector2.zero;
    }

    public void Show(bool aCanSkill)
    {
        gameObject.SetActive(true);
        mSkillButton.interactable = aCanSkill;
        mAttackButton.onClick.AddListener(() => OnAttack?.Invoke());
        mSkillButton.onClick.AddListener(() => OnSkill?.Invoke());
        mDetailButton.onClick.AddListener(() => OnDetail?.Invoke());
        mBackButton.onClick.AddListener(() => OnBackRequested?.Invoke());
        EventSystem.current.SetSelectedGameObject(mAttackButton.gameObject);
    }

    public void Hide()
    {
        foreach(var b in new[]{mAttackButton, mSkillButton, mDetailButton, mBackButton})
            b.onClick.RemoveAllListeners();
        gameObject.SetActive(false);
    }
}
