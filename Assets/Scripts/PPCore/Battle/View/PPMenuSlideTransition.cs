/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file PPMenuSlideTransition.cs
 * @author hqrse
 * @date 2026/09/10
 * @brief メニューのスライド＋フェード入れ替え演出を再生する共通コルーチン
 * =====================================*/

using System;
using System.Collections;
using UnityEngine;

namespace PPCore
{
    // RectTransformの位置とCanvasGroupの透明度を同時に補間するだけの汎用コルーチン
    // 各Viewは開始値・終了値・所要時間を渡して自分の StartCoroutine で回すだけでよい
    // どのメニューが何と入れ替わるかは呼び出し側（各InputState）が決める。ここでは値を補間するだけ
    public static class PPMenuSlideTransition
    {
        // aTarget : 位置を動かす対象
        // aCanvasGroup : 透明度を動かす対象。null ならスライドのみでフェードはしない
        // aFromPos / aToPos : 開始・終了の anchoredPosition
        // aFromAlpha / aToAlpha : 開始・終了の alpha（aCanvasGroup が null のときは無視される）
        // aDuration : 所要時間（秒）。timeScale = 0 中でも動くよう unscaledDeltaTime で進める
        // aOnComplete : 再生完了後に一度だけ呼ばれる。非表示化などの後始末に使う
        public static IEnumerator Play(
            RectTransform aTarget,
            CanvasGroup aCanvasGroup,
            Vector2 aFromPos,
            Vector2 aToPos,
            float aFromAlpha,
            float aToAlpha,
            float aDuration,
            Action aOnComplete)
        {
            // 再生中は入力を受け付けない。二重入力や、動いている途中のボタンを押せてしまうのを防ぐ
            if (aCanvasGroup != null)
            {
                aCanvasGroup.interactable = false;
                aCanvasGroup.blocksRaycasts = false;
            }

            aTarget.anchoredPosition = aFromPos;
            if (aCanvasGroup != null) aCanvasGroup.alpha = aFromAlpha;

            float elapsed = 0f;
            while (elapsed < aDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = aDuration > 0f ? Mathf.Clamp01(elapsed / aDuration) : 1f;
                float eased = t * t * (3f - 2f * t); // smoothstep。動き始めと止まり際を滑らかにする

                aTarget.anchoredPosition = Vector2.Lerp(aFromPos, aToPos, eased);
                if (aCanvasGroup != null) aCanvasGroup.alpha = Mathf.Lerp(aFromAlpha, aToAlpha, eased);

                yield return null;
            }

            aTarget.anchoredPosition = aToPos;
            if (aCanvasGroup != null)
            {
                aCanvasGroup.alpha = aToAlpha;
                aCanvasGroup.interactable = true;
                aCanvasGroup.blocksRaycasts = true;
            }

            aOnComplete?.Invoke();
        }
    }
}
