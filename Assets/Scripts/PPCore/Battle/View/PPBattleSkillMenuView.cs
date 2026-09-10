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
using AttributeUtility;

namespace PPCore
{
    // ユニットの所持スキルをボタンとして並べるメニュー
    // 表示するたびにボタンを生成し直し、閉じるときに破棄する
    // ユニットごとにスキル数が違うため、使い回さず作り直す方針
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
        // コマンドメニューとの入れ替え演出で透明度を動かす対象。未設定なら自身から自動取得を試みる
        [Label("キャンバスグループ")]
        [SerializeField] private CanvasGroup mCanvasGroup;
        // コマンドメニューとの入れ替え演出で動かす距離（上方向が正の値）
        [Label("入れ替え演出の距離")]
        [SerializeField] private float mSlideDistance = 80f;
        // コマンドメニューとの入れ替え演出の所要時間
        [Label("入れ替え演出の時間")]
        [SerializeField] private float mTransitionDuration = 0.2f;

        // スキルが選択されたときに発火する
        public event Action<BattleSkill> OnSkillSelected;
        // 戻るが押されたときに発火する
        public event Action OnBackRequested;

        // 生成済みのスキルボタン。閉じるときにまとめて破棄する
        private readonly List<PPBattleSkillButton> mSkillButtons = new();
        // 再生中のスライド演出。新しい演出を始める際は必ず先に止める
        private Coroutine mTransitionCoroutine;

        // インスペクタで未設定の場合、自身にアタッチされた CanvasGroup を使う
        private void Awake()
        {
            if (mCanvasGroup == null) mCanvasGroup = GetComponent<CanvasGroup>();
        }

        // メニューを指定した位置へ移動させる。レイアウトを崩さないよう worldPositionStays は false
        // aAnchor : 配置先の親となる RectTransform
        public void AttachTo(RectTransform aAnchor)
        {
            var rt = (RectTransform)transform;
            rt.SetParent(aAnchor, false);
            rt.anchoredPosition = Vector2.zero;
        }

        // ユニットの所持スキル分のボタンを生成して表示する
        // 各ボタンには発動可否を判定できるステータスソースを渡すため、
        // リソース不足のスキルは自動的に押せない状態になる
        // aUnit : スキルを表示する対象ユニット
        // aContext : 発動可否の判定に使うバトルコンテキスト
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
                // カタログ未設定、または該当アイコン未登録のどちらもアイコンなしとして扱う
                var icon = mIconCatalog != null
                    ? mIconCatalog.Resolve(skill.SkillId)?.SkillIcon
                    : null;
                btn.Setup(skill, src, icon, s => OnSkillSelected?.Invoke(s));
                mSkillButtons.Add(btn);

                // 初期フォーカス設定
                firstBtn ??= btn;
            }

            // 直後にフォーカスを当てるため、レイアウトの反映を次フレームまで待たない
            LayoutRebuilder.ForceRebuildLayoutImmediate(mContent);

            mBackButton.onClick.AddListener(RaiseBack);

            // 初期フォーカスを設定する(スキルがない場合はBackButtonにフォーカス)
            var focus = firstBtn != null ? firstBtn.FocusTarget : mBackButton.gameObject;
            EventSystem.current.SetSelectedGameObject(focus);
        }

        // メニューを閉じ、生成したボタンを破棄する
        public void Hide()
        {
            Clear();
            gameObject.SetActive(false);
        }

        // コマンド選択メニューから遷移してきた際に、上からスライドしつつ透明度を上げながら表示する
        // 中身の生成・アクティブ化自体は Show と同じなので、そちらを呼んでから開始位置と透明度だけ書き換えて演出を被せる
        // aUnit : スキルを表示する対象ユニット
        // aContext : 発動可否の判定に使うバトルコンテキスト
        public void ShowFromAbove(BattleUnit aUnit, BattleContext aContext)
        {
            Show(aUnit, aContext);

            var rt = (RectTransform)transform;
            var to = Vector2.zero;
            var from = new Vector2(to.x, to.y + mSlideDistance);

            if (mTransitionCoroutine != null) StopCoroutine(mTransitionCoroutine);
            mTransitionCoroutine = StartCoroutine(PPMenuSlideTransition.Play(rt, mCanvasGroup, from, to, 0f, 1f, mTransitionDuration, null));
        }

        // コマンド選択メニューへ戻る際に、上にスライドしつつ透明になりながら閉じる
        // 現在位置・現在の透明度から動かすため、演出の途中で呼ばれても不自然なジャンプをしない
        // ボタンの破棄（Clear）は演出が終わるまで遅らせ、リストが表示されたまま消えていくようにする
        public void HideSlideUp()
        {
            // 既に非表示なら購読解除だけ行う（Suspend と Exit の両方から Hide 系が呼ばれる経路があるための保険）
            if (!gameObject.activeSelf)
            {
                Clear();
                return;
            }

            mBackButton.onClick.RemoveListener(RaiseBack);

            var rt = (RectTransform)transform;
            var from = rt.anchoredPosition;
            var to = new Vector2(from.x, from.y + mSlideDistance);
            float fromAlpha = mCanvasGroup != null ? mCanvasGroup.alpha : 1f;

            if (mTransitionCoroutine != null) StopCoroutine(mTransitionCoroutine);
            mTransitionCoroutine = StartCoroutine(PPMenuSlideTransition.Play(
                rt, mCanvasGroup, from, to, fromAlpha, 0f, mTransitionDuration,
                () => { Clear(); gameObject.SetActive(false); }));
        }

        // 戻る操作を外部へ通知する
        private void RaiseBack()
        {
            OnBackRequested?.Invoke();
        }

        // 戻るボタンの購読を解除し、生成済みのスキルボタンをすべて破棄する
        // 表示のたびに作り直すため、開く前と閉じるときの両方から呼ばれる
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
