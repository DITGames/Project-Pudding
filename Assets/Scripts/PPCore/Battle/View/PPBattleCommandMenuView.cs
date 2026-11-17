/* =====================================
 * Copyright WabisabiAndons. All rights reserved.
 * @file PPBattleCommandMenuView.cs
 * @author hqrse
 * @date 2026/07/02
 * @brief バトル中のコマンドメニュー
 * =====================================*/

using System;
using AttributeUtility;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Serialization;
using UnityEngine.UI;

// ターゲット・スキル・解除・詳細・戻るを並べたコマンドメニューの View。押されたことをイベントとして外へ流すだけで、遷移は持たない
// Show でリスナーを登録し Hide で全解除するため、必ず対で呼ぶこと
public class PPBattleCommandMenuView : MonoBehaviour
{
    /* インスペクタ設定 */

    // 狙う相手を指定するボタン
    [Label("ターゲット")]
    [FormerlySerializedAs("mAttackButton")]
    [SerializeField] private Button mTargetButton;
    [Label("スキル")]
    [SerializeField] private Button mSkillButton;
    // スキルの選択と狙う相手の指定を取り消すボタン。未設定なら表示しない
    [Label("解除")]
    [SerializeField] private Button mClearButton;
    [Label("詳細")]
    [SerializeField] private Button mDetailButton;
    [Label("戻る")]
    [SerializeField] private Button mBackButton;

    /* イベント */

    // 各ボタンが押されたときに発火する（ターゲット / スキル / 解除 / 詳細 / 戻る）
    public event Action OnTarget, OnSkill, OnClear, OnDetail, OnBackRequested;

    /* 表示 */

    // メニューを表示し、各ボタンのリスナーを登録してターゲットボタンにフォーカスを当てる
    public void Show()
    {
        gameObject.SetActive(true);
        mTargetButton.onClick.AddListener(() => OnTarget?.Invoke());
        mSkillButton.onClick.AddListener(() => OnSkill?.Invoke());
        if (mClearButton != null) mClearButton.onClick.AddListener(() => OnClear?.Invoke());
        mDetailButton.onClick.AddListener(() => OnDetail?.Invoke());
        mBackButton.onClick.AddListener(() => OnBackRequested?.Invoke());
        EventSystem.current.SetSelectedGameObject(mTargetButton.gameObject);
    }

    // メニューを閉じ、登録済みのリスナーをすべて解除する
    // ラムダで登録しているため個別解除ができず、まとめて外している
    public void Hide()
    {
        foreach (var b in new[] { mTargetButton, mSkillButton, mClearButton, mDetailButton, mBackButton })
        {
            if (b != null) b.onClick.RemoveAllListeners();
        }
        gameObject.SetActive(false);
    }
}