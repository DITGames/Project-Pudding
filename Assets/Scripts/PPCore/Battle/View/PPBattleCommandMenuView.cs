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
using UnityEngine.UI;

// 攻撃・スキル・詳細・戻るを並べたコマンドメニューの View。押されたことをイベントとして外へ流すだけで、遷移は持たない
// Show でリスナーを登録し Hide で全解除するため、必ず対で呼ぶこと
public class PPBattleCommandMenuView : MonoBehaviour
{
    /* インスペクタ設定 */

    [Label("攻撃")]
    [SerializeField] private Button mAttackButton;
    // スキルボタン。スキルを持たないユニットでは無効化される
    [Label("スキル")]
    [SerializeField] private Button mSkillButton;
    [Label("詳細")]
    [SerializeField] private Button mDetailButton;
    [Label("戻る")]
    [SerializeField] private Button mBackButton;

    /* イベント */

    // 各ボタンが押されたときに発火する（攻撃 / スキル / 詳細 / 戻る）
    public event Action OnAttack, OnSkill, OnDetail, OnBackRequested;

    /* 初期化・接続 */

    // メニューを指定した位置へ移動させる。選択中ユニットの隣に出すために使う
    // aAnchor : 配置先の親となる RectTransform
    public void AttachTo(RectTransform aAnchor)
    {
        var rt = (RectTransform)transform;
        rt.SetParent(aAnchor);
        rt.anchoredPosition = Vector2.zero;
    }

    /* 表示 */

    // メニューを表示し、各ボタンのリスナーを登録して攻撃ボタンにフォーカスを当てる
    // aCanSkill : スキルボタンを押せる状態にするか
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

    // メニューを閉じ、登録済みのリスナーをすべて解除する
    // ラムダで登録しているため個別解除ができず、まとめて外している
    public void Hide()
    {
        foreach(var b in new[]{mAttackButton, mSkillButton, mDetailButton, mBackButton})
            b.onClick.RemoveAllListeners();
        gameObject.SetActive(false);
    }
}
