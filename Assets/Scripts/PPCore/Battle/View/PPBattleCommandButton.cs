/* =====================================
 * Copyright DITGames. All rights reserved.
 * @file PPBattleCommandButton.cs
 * @author DITGames
 * @date 2026/09/07
 * @brief バトル入力で使う汎用ボタン要素（画像/テキスト/画像+テキストの3パターン兼用）
 * =====================================*/

using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using AttributeUtility;

namespace PPCore
{
    // ユニット選択・スキル選択など、コマンド入力中に並ぶボタンをすべてこれ1つで賄う汎用ボタン
    // 画像のみ／テキストのみ／画像+テキストの3パターンを、渡された画像とテキストの有無だけで自動的に切り替える
    //   ・画像のみ : 画像を中央に表示（ユニット選択など、名前を出さない用途）
    //   ・テキストのみ : テキストを中央に大きく表示（「戻る」「詳細」など文言だけのボタン）
    //   ・画像+テキスト : 画像の下にテキストを小さく表示（スキル選択のコスト表示など）
    // ホバーで少し上へずれる演出も併せ持つ
    public class PPBattleCommandButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [Label("アイコン")]
        [SerializeField] private Image mIcon;
        [Label("テキスト")]
        [SerializeField] private TMP_Text mLabel;
        [Label("ボタン")]
        [SerializeField] private Button mButton;

        [Header("テキストのみの場合のレイアウト")]
        [Label("フォントサイズ")]
        [SerializeField] private float mTextOnlyFontSize = 20f;
        [Label("アンカー位置")]
        [SerializeField] private Vector2 mTextOnlyAnchoredPosition = Vector2.zero;
        [Label("サイズ")]
        [SerializeField] private Vector2 mTextOnlySizeDelta = new(90, 90);

        [Header("画像+テキストの場合のテキストレイアウト")]
        [Label("フォントサイズ")]
        [SerializeField] private float mCaptionFontSize = 14f;
        [Label("アンカー位置")]
        [SerializeField] private Vector2 mCaptionAnchoredPosition = new(0, -35);
        [Label("サイズ")]
        [SerializeField] private Vector2 mCaptionSizeDelta = new(90, 20);

        [Header("ホバー演出")]
        // ホバー時に上へずれる量（RectTransformのローカル座標系、単位はUIのピクセル相当）
        [Label("上昇量")]
        [SerializeField] private float mHoverOffsetY = 20f;
        // 上昇・復帰にかける時間(秒)
        [Label("移動時間(秒)")]
        [SerializeField] private float mHoverDuration = 0.15f;
        // 0〜1の経過時間を移動の進み具合へ変換するカーブ。急加速・オーバーシュートなどの調整に使う
        [Label("移動カーブ")]
        [SerializeField] private AnimationCurve mHoverCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

        // 決定時に呼ぶコールバック
        private Action mOnDecided;

        // 自身の RectTransform。ホバー演出の対象
        private RectTransform mRect;
        // ホバー前の位置。外部から配置された後の位置を基準にするため、初回ホバー時に遅延キャプチャする
        private Vector2? mHomeAnchoredPosition;
        // 再生中のホバー演出コルーチン。入り→抜けが連続しても破綻しないよう都度差し替える
        private Coroutine mHoverCoroutine;

        // 初期フォーカスを当てる対象
        public GameObject FocusTarget => mButton.gameObject;
        // このボタンの RectTransform
        public RectTransform Rect => mRect;

        private void Awake() => mRect = (RectTransform)transform;

        // ボタンを初期化する。画像とテキストの有無から表示パターンを決め、押下コールバックを登録する
        // aIcon : 表示する画像。null なら非表示（テキストのみ表示になる）
        // aText : 表示するテキスト。null/空文字なら非表示（画像のみ表示になる）
        // aOnDecided : 決定時に呼ぶコールバック
        public void Setup(Sprite aIcon, string aText, Action aOnDecided)
        {
            mOnDecided = aOnDecided;
            ApplyContent(aIcon, aText);
            mButton.onClick.AddListener(HandleClick);
        }

        // 表示内容だけを更新する。リソース変動によるコスト表示の更新など、決定コールバックを変えずに使う
        // aIcon : 表示する画像。null なら非表示
        // aText : 表示するテキスト。null/空文字なら非表示
        public void SetContent(Sprite aIcon, string aText) => ApplyContent(aIcon, aText);

        // 押下可否を切り替える
        // aInteractable : 押せる状態にするなら true
        public void SetInteractable(bool aInteractable) => mButton.interactable = aInteractable;

        // 画像・テキストの有無から表示パターンを判定し、アイコンとテキストの見た目を切り替える
        private void ApplyContent(Sprite aIcon, string aText)
        {
            bool hasIcon = aIcon != null;
            bool hasText = !string.IsNullOrEmpty(aText);

            if (mIcon != null)
            {
                mIcon.enabled = hasIcon;
                mIcon.sprite = aIcon;
            }

            if (mLabel != null)
            {
                mLabel.gameObject.SetActive(hasText);
                mLabel.text = aText;

                var rect = mLabel.rectTransform;
                if (hasIcon)
                {
                    // 画像+テキスト : 画像の下に小さく表示
                    mLabel.fontSize = mCaptionFontSize;
                    rect.anchoredPosition = mCaptionAnchoredPosition;
                    rect.sizeDelta = mCaptionSizeDelta;
                }
                else
                {
                    // テキストのみ : 中央に大きく表示
                    mLabel.fontSize = mTextOnlyFontSize;
                    rect.anchoredPosition = mTextOnlyAnchoredPosition;
                    rect.sizeDelta = mTextOnlySizeDelta;
                }
            }
        }

        // 押下時にコールバックを呼ぶ
        private void HandleClick() => mOnDecided?.Invoke();

        // マウスカーソルが乗ったときに上へずらす
        public void OnPointerEnter(PointerEventData aEventData)
        {
            mHomeAnchoredPosition ??= mRect.anchoredPosition;
            PlayHoverAnimation(mHomeAnchoredPosition.Value + new Vector2(0f, mHoverOffsetY));
        }

        // マウスカーソルが離れたときに元の位置へ戻す
        public void OnPointerExit(PointerEventData aEventData)
        {
            mHomeAnchoredPosition ??= mRect.anchoredPosition;
            PlayHoverAnimation(mHomeAnchoredPosition.Value);
        }

        // 目標位置へ向けたホバー演出を再生する。再生中に反転しても現在位置から滑らかに繋げる
        // aTarget : 移動先のアンカー位置
        private void PlayHoverAnimation(Vector2 aTarget)
        {
            if (mHoverCoroutine != null) StopCoroutine(mHoverCoroutine);
            mHoverCoroutine = StartCoroutine(AnimatePosition(aTarget));
        }

        // 現在位置から目標位置まで、mHoverCurve に従って mHoverDuration 秒かけて移動する
        // aTarget : 移動先のアンカー位置
        private IEnumerator AnimatePosition(Vector2 aTarget)
        {
            if (mHoverDuration <= 0f)
            {
                mRect.anchoredPosition = aTarget;
                yield break;
            }

            Vector2 start = mRect.anchoredPosition;
            float elapsed = 0f;
            while (elapsed < mHoverDuration)
            {
                // メニュー表示中は timeScale=0（コマンド入力中の停止演出）のため unscaledDeltaTime で進める
                elapsed += Time.unscaledDeltaTime;
                float progress = mHoverCurve.Evaluate(Mathf.Clamp01(elapsed / mHoverDuration));
                mRect.anchoredPosition = Vector2.LerpUnclamped(start, aTarget, progress);
                yield return null;
            }

            mRect.anchoredPosition = aTarget;
            mHoverCoroutine = null;
        }

        // 破棄時に購読を解除する
        private void OnDestroy()
        {
            mButton.onClick.RemoveListener(HandleClick);
        }
    }
}
