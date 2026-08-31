/* =====================================
 * Copyright DITGames. All rights reserved.
 * @file BattleUnitButtonElement.cs
 * @author DITGames
 * @date 2026/08/31
 * @brief 自陣モンスター1体分のボタン表示要素（BattleButtonElementプレハブのルート用）
 * =====================================*/

using System;
using System.Collections;
using CommandBattleCore;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using AttributeUtility;

namespace BattleInput
{
    // BattleButtonElement プレハブのルートに付ける表示用コンポーネント
    // 対象ユニットの保持・アイコン反映・マウスクリックの通知に加えて、
    // ホバー時に少し上へずれる演出（離れると元へ戻る）を行う
    public class BattleUnitButtonElement : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [Label("モンスター画像")]
        [SerializeField] private Image mInnerImage;
        [Label("選択ボタン")]
        [SerializeField] private Button mButton;

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

        // このボタンが表す自陣ユニット
        public BattleUnit BattleUnit { get; private set; }

        // クリックされたときの通知(このボタン自身を渡す。BattleUnit はここから引ける)
        public event Action<BattleUnitButtonElement> OnClicked;

        // 自身の RectTransform。ホバー演出の対象
        private RectTransform mRect;
        // ホバー前の位置。外部から配置された後の位置を基準にするため、初回ホバー時に遅延キャプチャする
        private Vector2? mHomeAnchoredPosition;
        // 再生中のホバー演出コルーチン。入り→抜けが連続しても破綻しないよう都度差し替える
        private Coroutine mHoverCoroutine;

        // ボタンのクリックを OnClicked へ中継する
        private void Awake()
        {
            mRect = (RectTransform)transform;
            if (mButton != null)
            {
                mButton.onClick.AddListener(() => OnClicked?.Invoke(this));
            }
        }

        // 対象ユニットとその画像を設定する
        // InnerImage はプレハブ既定で非アクティブなため、アイコンが解決できた場合のみ有効化する
        // aUnit : 対象のユニット
        // aIcon : 表示するアイコン。ビジュアル定義が解決できなければ null
        public void SetUnit(BattleUnit aUnit, Sprite aIcon)
        {
            BattleUnit = aUnit;
            if (mInnerImage == null) return;

            mInnerImage.sprite = aIcon;
            mInnerImage.gameObject.SetActive(aIcon != null);
        }

        // マウスカーソルが乗ったときに上へずらす
        public void OnPointerEnter(PointerEventData aEventData)
        {
            mHomeAnchoredPosition ??= mRect.anchoredPosition;
            PlayHoverAnimation(mHomeAnchoredPosition.Value + new Vector2(0f, mHoverOffsetY));
        }

        // マウスカーソルが離れたときに元の位置へ戻す
        public void OnPointerExit(PointerEventData aEventData)
        {
            // 配置前にホバーが起きることは想定していないが、念のため未キャプチャなら現在地を基準にする
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
                // モンスターボタンは timeScale=0（コマンド入力中の停止演出）の間だけ表示されるため、
                // 影響を受けない unscaledDeltaTime で進める
                elapsed += Time.unscaledDeltaTime;
                float progress = mHoverCurve.Evaluate(Mathf.Clamp01(elapsed / mHoverDuration));
                mRect.anchoredPosition = Vector2.LerpUnclamped(start, aTarget, progress);
                yield return null;
            }

            mRect.anchoredPosition = aTarget;
            mHoverCoroutine = null;
        }
    }
}
