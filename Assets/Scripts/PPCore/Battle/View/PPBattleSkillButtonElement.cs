/* =====================================
 * Copyright DITGames. All rights reserved.
 * @file PPBattleSkillButtonElement.cs
 * @author DITGames
 * @date 2026/09/06
 * @brief スキルメニューに並ぶボタン要素（アイコン+ホバー演出のスタイル）
 * =====================================*/

using System;
using System.Collections;
using CommandBattleCore;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using AttributeUtility;

namespace PPCore
{
    // スキルメニューに並ぶ 1 項目分のボタン
    // BattleUnitButtonElement と同様、アイコンのみを表示しホバーで少し上へずれる演出を行うスタイル
    // 表示内容は IPPSkillStatusSource から取り、変更通知を購読してコスト表示と押下可否を自動更新する
    // リストではなく横一列に並べる運用を想定している
    public class PPBattleSkillButtonElement : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [Label("アイコン")]
        [SerializeField] private Image mIcon;
        [Label("ボタン")]
        [SerializeField] private Button mButton;
        [Label("消費コイン")]
        [SerializeField] private TMP_Text mCostLabel;

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

        // このボタンが表すスキル
        private BattleSkill mSkill;
        // 表示情報の供給元
        private IPPSkillStatusSource mSource;
        // 決定時に呼ぶコールバック
        private Action<BattleSkill> mOnDecided;

        // 自身の RectTransform。ホバー演出の対象
        private RectTransform mRect;
        // ホバー前の位置。外部から配置された後の位置を基準にするため、初回ホバー時に遅延キャプチャする
        private Vector2? mHomeAnchoredPosition;
        // 再生中のホバー演出コルーチン。入り→抜けが連続しても破綻しないよう都度差し替える
        private Coroutine mHoverCoroutine;

        // 初期フォーカスを当てる対象
        public GameObject FocusTarget => mButton.gameObject;
        // このボタンの RectTransform
        public RectTransform Rect => (RectTransform)transform;

        private void Awake() => mRect = (RectTransform)transform;

        // ボタンを初期化する。表示内容を流し込み、押下と変更通知を購読して初回描画を行う
        // aSkill : このボタンが表すスキル
        // aSource : 表示情報の供給元
        // aIcon : スキルアイコン。無ければ null
        // aOnSelected : 決定時に呼ぶコールバック
        public void Setup(BattleSkill aSkill, IPPSkillStatusSource aSource, Sprite aIcon,
            Action<BattleSkill> aOnSelected)
        {
            mSkill = aSkill;
            mSource = aSource;
            mOnDecided = aOnSelected;

            if (mIcon != null)
            {
                mIcon.enabled = aIcon != null;
                mIcon.sprite = aIcon;
            }

            mButton.onClick.AddListener(HandleClick);
            mSource.Changed += Refresh;
            Refresh();
        }

        // 押下時にスキルを添えてコールバックを呼ぶ
        private void HandleClick() => mOnDecided?.Invoke(mSkill);

        // コスト表示と押下可否を現在の状態へ更新する
        // コストラベル未設定のスタイルでも成立するよう null を許容する
        private void Refresh()
        {
            if (mCostLabel != null) mCostLabel.text = mSource.Cost.ToString();
            mButton.interactable = mSource.IsCastable;
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
        // 供給元がリソースを購読している場合があるため、そちらの Dispose も通しておく
        private void OnDestroy()
        {
            mButton.onClick.RemoveListener(HandleClick);
            if (mSource != null) mSource.Changed -= Refresh;
            (mSource as IDisposable)?.Dispose();
        }
    }
}
