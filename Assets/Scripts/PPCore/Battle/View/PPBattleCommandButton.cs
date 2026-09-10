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

        [Header("入退場演出")]
        // 出現/消滅時に上下へずれる量。ここから本来の位置へ移動しながらフェードインし、
        // 逆方向へ移動しながらフェードアウトして消える
        [Label("移動量")]
        [SerializeField] private float mTransitionOffsetY = 60f;
        // 出現・消滅にかける時間(秒)
        [Label("移動時間(秒)")]
        [SerializeField] private float mTransitionDuration = 0.2f;
        // 出現・消滅の進み具合を変換するカーブ
        [Label("移動カーブ")]
        [SerializeField] private AnimationCurve mTransitionCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

        // 決定時に呼ぶコールバック
        private Action mOnDecided;

        // 自身の RectTransform。位置演出の対象
        private RectTransform mRect;
        // フェード演出用。プレハブに無ければ自動で付与する
        private CanvasGroup mCanvasGroup;
        // 定位置（レイアウトが配置した本来の位置）。外部から配置された後の位置を基準にするため、
        // 初回ホバー時、もしくは入退場演出の開始時に遅延キャプチャする
        private Vector2? mHomeAnchoredPosition;
        // 再生中の位置演出コルーチン。ホバーと入退場が同時に動いて破綻しないよう 1 本にまとめて都度差し替える
        private Coroutine mMotionCoroutine;

        // 初期フォーカスを当てる対象
        public GameObject FocusTarget => mButton.gameObject;
        // このボタンの RectTransform
        public RectTransform Rect => mRect;

        private void Awake()
        {
            mRect = (RectTransform)transform;
            mCanvasGroup = GetComponent<CanvasGroup>();
            if (mCanvasGroup == null) mCanvasGroup = gameObject.AddComponent<CanvasGroup>();
        }

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

        // 本来の位置から aDirection の逆側にずれた位置からフェードインしながら現れる演出を再生する
        // レイアウトグループが配置した直後の位置を定位置として記録するため、
        // 呼び出しは LayoutRebuilder.ForceRebuildLayoutImmediate で配置を確定させた後に行うこと
        // aDirection : 演出の流れる向き。Down なら上から、Up なら下から現れる
        public void PlayEnter(PPBattleTransitionDirection aDirection)
        {
            // 入場中はポインタ操作を受け付けない（マウスカーソルが乗っているとホバー演出と競合し、
            // 位置がおかしくなるため）。完了後に解放する
            mCanvasGroup.blocksRaycasts = false;

            mHomeAnchoredPosition = mRect.anchoredPosition;
            float startY = mHomeAnchoredPosition.Value.y +
                (aDirection == PPBattleTransitionDirection.Down ? mTransitionOffsetY : -mTransitionOffsetY);
            mRect.anchoredPosition = new Vector2(mHomeAnchoredPosition.Value.x, startY);
            mCanvasGroup.alpha = 0f;
            PlayMotion(mHomeAnchoredPosition.Value, 1f, mTransitionDuration, mTransitionCurve,
                () => mCanvasGroup.blocksRaycasts = true);
        }

        // 本来の位置から aDirection 側へフェードアウトしながら消える演出を再生し、完了後にコールバックを呼ぶ
        // 退場中はポインタ操作を受け付けない（マウスカーソルが乗っているとホバー演出と競合し、
        // 位置がおかしくなるため。押下も無効化される）
        // aDirection : 演出の流れる向き。Down なら下へ、Up なら上へ抜ける
        // aStableParent : 退場中の一時的な親。null 以外なら見た目の位置を保ったまま付け替える。
        //   元のボタン列は ContentSizeFitter で自動収縮するコンテナのため、退場中のボタンを
        //   そこに残したままだと、他のボタンがすべて退場してコンテナが収縮した瞬間に
        //   アンカー位置の基準（コンテナの辺）ごとずれて「ワープしてからフェードする」ように見えてしまう。
        //   ContentSizeFitter の影響を受けない安定した親（メニューのルートなど）へ退避させることで防ぐ
        // aOnComplete : 演出が終わったときに呼ぶコールバック（破棄処理などに使う）
        public void PlayExit(PPBattleTransitionDirection aDirection, Transform aStableParent, Action aOnComplete)
        {
            mCanvasGroup.blocksRaycasts = false;

            if (aStableParent != null)
            {
                mRect.SetParent(aStableParent, true);
            }

            Vector2 home = mRect.anchoredPosition;
            float endY = home.y + (aDirection == PPBattleTransitionDirection.Down ? -mTransitionOffsetY : mTransitionOffsetY);
            PlayMotion(new Vector2(home.x, endY), 0f, mTransitionDuration, mTransitionCurve, aOnComplete);
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
        // 入退場演出中（blocksRaycasts=false）は競合を避けるため反応しない。
        // 通常は raycast が通らずそもそも呼ばれないはずだが、念のための保険
        public void OnPointerEnter(PointerEventData aEventData)
        {
            if (!mCanvasGroup.blocksRaycasts) return;
            mHomeAnchoredPosition ??= mRect.anchoredPosition;
            PlayMotion(mHomeAnchoredPosition.Value + new Vector2(0f, mHoverOffsetY), mCanvasGroup.alpha, mHoverDuration, mHoverCurve, null);
        }

        // マウスカーソルが離れたときに元の位置へ戻す
        // 入退場演出中は反応しない（理由は OnPointerEnter と同じ）
        public void OnPointerExit(PointerEventData aEventData)
        {
            if (!mCanvasGroup.blocksRaycasts) return;
            mHomeAnchoredPosition ??= mRect.anchoredPosition;
            PlayMotion(mHomeAnchoredPosition.Value, mCanvasGroup.alpha, mHoverDuration, mHoverCurve, null);
        }

        // 目標位置・目標アルファへ向けた演出を再生する。ホバー・入退場のどちらもこれを介して行うことで、
        // 同時に動いて破綻しないよう常に 1 本のコルーチンにまとめる
        // aTargetPos : 移動先のアンカー位置
        // aTargetAlpha : 移動先の不透明度
        // aDuration : 演出にかける時間(秒)
        // aCurve : 進み具合を変換するカーブ
        // aOnComplete : 演出が終わったときに呼ぶコールバック
        private void PlayMotion(Vector2 aTargetPos, float aTargetAlpha, float aDuration, AnimationCurve aCurve, Action aOnComplete)
        {
            if (mMotionCoroutine != null) StopCoroutine(mMotionCoroutine);
            mMotionCoroutine = StartCoroutine(AnimateMotion(aTargetPos, aTargetAlpha, aDuration, aCurve, aOnComplete));
        }

        // 現在位置・現在アルファから目標へ、aCurve に従って aDuration 秒かけて変化させる
        private IEnumerator AnimateMotion(Vector2 aTargetPos, float aTargetAlpha, float aDuration, AnimationCurve aCurve, Action aOnComplete)
        {
            Vector2 startPos = mRect.anchoredPosition;
            float startAlpha = mCanvasGroup.alpha;

            if (aDuration <= 0f)
            {
                mRect.anchoredPosition = aTargetPos;
                mCanvasGroup.alpha = aTargetAlpha;
                mMotionCoroutine = null;
                aOnComplete?.Invoke();
                yield break;
            }

            float elapsed = 0f;
            while (elapsed < aDuration)
            {
                // メニュー表示中は timeScale=0（コマンド入力中の停止演出）のため unscaledDeltaTime で進める
                elapsed += Time.unscaledDeltaTime;
                float progress = aCurve.Evaluate(Mathf.Clamp01(elapsed / aDuration));
                mRect.anchoredPosition = Vector2.LerpUnclamped(startPos, aTargetPos, progress);
                mCanvasGroup.alpha = Mathf.Lerp(startAlpha, aTargetAlpha, progress);
                yield return null;
            }

            mRect.anchoredPosition = aTargetPos;
            mCanvasGroup.alpha = aTargetAlpha;
            mMotionCoroutine = null;
            aOnComplete?.Invoke();
        }

        // 破棄時に購読を解除する
        private void OnDestroy()
        {
            mButton.onClick.RemoveListener(HandleClick);
        }
    }
}
