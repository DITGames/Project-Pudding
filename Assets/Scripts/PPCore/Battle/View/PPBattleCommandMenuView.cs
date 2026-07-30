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

/// <summary>
/// 攻撃・スキル・詳細・戻るを並べたコマンドメニューの View。
/// <para>
/// ボタンが押されたことをイベントとして外へ流すだけで、何をするかは持たない。
/// 実際の遷移は <see cref="PPCore.PPCommandSelectState"/> が決める。
/// </para>
/// <para>
/// <see cref="Show"/> でリスナーを登録し <see cref="Hide"/> で全解除するため、
/// この 2 つは必ず対で呼ぶこと。Hide を挟まず Show を重ねるとリスナーが多重登録される。
/// </para>
/// </summary>
/// <remarks>
/// このクラスだけ namespace の外に置かれており、PPCore の他の View と同じ扱いになっていない。
/// </remarks>
public class PPBattleCommandMenuView : MonoBehaviour
{
    /// <summary>通常攻撃ボタン。</summary>
    [Label("攻撃")]
    [SerializeField] private Button mAttackButton;
    /// <summary>スキルボタン。スキルを持たないユニットでは無効化される。</summary>
    [Label("スキル")]
    [SerializeField] private Button mSkillButton;
    /// <summary>詳細表示ボタン。</summary>
    [Label("詳細")]
    [SerializeField] private Button mDetailButton;
    /// <summary>戻るボタン。</summary>
    [Label("戻る")]
    [SerializeField] private Button mBackButton;

    /// <summary>各ボタンが押されたときに発火する（攻撃 / スキル / 詳細 / 戻る）。</summary>
    public event Action OnAttack, OnSkill, OnDetail, OnBackRequested;

    /// <summary>
    /// メニューを指定した位置へ移動させる。選択中ユニットの隣に出すために使う。
    /// </summary>
    /// <param name="aAnchor">配置先の親となる RectTransform。</param>
    public void AttachTo(RectTransform aAnchor)
    {
        var rt = (RectTransform)transform;
        rt.SetParent(aAnchor);
        rt.anchoredPosition = Vector2.zero;
    }

    /// <summary>
    /// メニューを表示し、各ボタンのリスナーを登録して攻撃ボタンにフォーカスを当てる。
    /// </summary>
    /// <param name="aCanSkill">スキルボタンを押せる状態にするか。</param>
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

    /// <summary>
    /// メニューを閉じ、登録済みのリスナーをすべて解除する。
    /// ラムダで登録しているため個別解除ができず、まとめて外している。
    /// </summary>
    public void Hide()
    {
        foreach(var b in new[]{mAttackButton, mSkillButton, mDetailButton, mBackButton})
            b.onClick.RemoveAllListeners();
        gameObject.SetActive(false);
    }
}
