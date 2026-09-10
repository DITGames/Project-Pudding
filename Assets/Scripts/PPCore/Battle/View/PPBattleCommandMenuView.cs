/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file PPBattleCommandMenuView.cs
 * @author hqrse
 * @date 2026/07/02
 * @brief バトル中のコマンドメニュー
 * =====================================*/

using System;
using AttributeUtility;
using PPCore;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// 攻撃・スキル・詳細・戻るを並べたコマンドメニューの View
// ボタンが押されたことをイベントとして外へ流すだけで、何をするかは持たない
// 実際の遷移は PPCore.PPCommandSelectState が決める
// Show でリスナーを登録し Hide で全解除するため、この 2 つは必ず対で呼ぶこと
// Hide を挟まず Show を重ねるとリスナーが多重登録される
// このクラスだけ namespace の外に置かれており、PPCore の他の View と同じ扱いになっていない
public class PPBattleCommandMenuView : MonoBehaviour
{
    [Label("攻撃")]
    [SerializeField] private Button mAttackButton;
    // スキルボタン。スキルを持たないユニットでは無効化される
    [Label("スキル")]
    [SerializeField] private Button mSkillButton;
    [Label("詳細")]
    [SerializeField] private Button mDetailButton;
    [Label("戻る")]
    [SerializeField] private Button mBackButton;
    // スキル選択メニューとの入れ替え演出で動かす距離（下方向が正の値）
    [Label("入れ替え演出の距離")]
    [SerializeField] private float mSlideDistance = 80f;
    // スキル選択メニューとの入れ替え演出の所要時間
    [Label("入れ替え演出の時間")]
    [SerializeField] private float mTransitionDuration = 0.2f;

    // 各ボタンが押されたときに発火する（攻撃 / スキル / 詳細 / 戻る）
    public event Action OnAttack, OnSkill, OnDetail, OnBackRequested;

    // 再生中のスライド演出。新しい演出を始める際は必ず先に止める
    private Coroutine mTransitionCoroutine;

    // メニューを指定した位置へ移動させる。選択中ユニットの隣に出すために使う
    // aAnchor : 配置先の親となる RectTransform
    public void AttachTo(RectTransform aAnchor)
    {
        var rt = (RectTransform)transform;
        rt.SetParent(aAnchor);
        rt.anchoredPosition = Vector2.zero;
    }

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

    // スキル選択メニューから戻ってきた際に、下から上へスライドしながら表示する（フェードはしない）
    // 表示内容自体は Show と同じなので、そちらを呼んでから開始位置だけ書き換えて演出を被せる
    // aCanSkill : スキルボタンを押せる状態にするか
    public void ShowFromBelow(bool aCanSkill)
    {
        Show(aCanSkill);

        var rt = (RectTransform)transform;
        var to = Vector2.zero;
        var from = new Vector2(to.x, to.y - mSlideDistance);

        if (mTransitionCoroutine != null) StopCoroutine(mTransitionCoroutine);
        mTransitionCoroutine = StartCoroutine(PPMenuSlideTransition.Play(rt, null, from, to, 1f, 1f, mTransitionDuration, null));
    }

    // スキル選択メニューへ遷移する際に、下にスライドしながら閉じる（フェードはしない）
    // 現在位置から動かすため、演出の途中で呼ばれても不自然なジャンプをしない
    public void HideSlideDown()
    {
        foreach(var b in new[]{mAttackButton, mSkillButton, mDetailButton, mBackButton})
            b.onClick.RemoveAllListeners();

        // 既に非表示なら何もしない（Suspend と Exit の両方から Hide 系が呼ばれる経路があるための保険）
        if (!gameObject.activeSelf) return;

        var rt = (RectTransform)transform;
        var from = rt.anchoredPosition;
        var to = new Vector2(from.x, from.y - mSlideDistance);

        if (mTransitionCoroutine != null) StopCoroutine(mTransitionCoroutine);
        mTransitionCoroutine = StartCoroutine(PPMenuSlideTransition.Play(
            rt, null, from, to, 1f, 1f, mTransitionDuration, () => gameObject.SetActive(false)));
    }
}
