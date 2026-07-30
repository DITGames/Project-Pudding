/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file PPBattleUnitView.cs
 * @author hqrse
 * @date 2026/06/25
 * @brief バトル中のユニット表示コンポーネント
 * =====================================*/
using System;
using CommandBattleCore;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PPCore
{
    /// <summary>
    /// 戦場に並ぶユニット 1 体分の表示。
    /// <para>
    /// 見た目（アイコン・アニメーション）の再生と、選択対象としての振る舞いを兼ねる。
    /// 演出メソッドは <see cref="PPBattleUnitViewBinder"/> がバトルイベントを受けて呼び出す。
    /// </para>
    /// <para>
    /// メニューの表示位置は <see cref="MenuAnchor"/> として公開し、
    /// 入力ステート側がここを基準にコマンドメニューを配置する。
    /// </para>
    /// </summary>
    public class PPBattleUnitView : MonoBehaviour, ISelectHandler, IDeselectHandler
    {
        /// <summary>名前と HP を表示するウィジェット。</summary>
        [Label("ステータスウィジェット")]
        [SerializeField] private PPUnitStatusWidget mStatusWidget;
        /// <summary>ユニットのアイコン。</summary>
        [Label("アイコン")]
        [SerializeField] private Image mUnitIcon;
        /// <summary>演出再生用のアニメーター。</summary>
        [Label("アニメーター")]
        [SerializeField] private Animator mAnimator;
        /// <summary>選択を受け付けるボタン。</summary>
        [Label("選択ボタン")]
        [SerializeField] private Button mSelectButton;
        /// <summary>フォーカス中に表示する枠。</summary>
        [Label("フォーカス枠")]
        [SerializeField] private GameObject mFocusFrame;
        /// <summary>コマンドメニューを配置する位置の基準。</summary>
        [Label("メニューアンカー")]
        [SerializeField] private RectTransform mMenuAnchor;

        /// <summary>このビューが表すユニット。</summary>
        private BattleUnit mBattleUnit;
        /// <summary>このビューが表すユニット。</summary>
        public BattleUnit BattleUnit => mBattleUnit;
        /// <summary>フォーカス対象となるオブジェクト。</summary>
        public GameObject SelectableObject => mSelectButton.gameObject;
        /// <summary>コマンドメニューを配置する位置の基準。</summary>
        public RectTransform MenuAnchor => mMenuAnchor;

        /// <summary>ユニットが決定されたときの通知。</summary>
        public event Action<PPBattleUnitView> OnDecided;

        /// <summary>フォーカスが乗ったときの通知。</summary>
        public event Action<PPBattleUnitView> OnSelected;
        /// <summary>フォーカスが外れたときの通知。</summary>
        public event Action<PPBattleUnitView> OnDeselected;

        /// <summary>選択ボタンの押下を決定通知へ中継する。</summary>
        private void Awake() => mSelectButton.onClick.AddListener(() => OnDecided?.Invoke(this));

        /// <summary>
        /// 表示対象のユニットと見た目を設定する。
        /// 味方は敵と向き合う形にするためアイコンを左右反転させる。
        /// 見た目定義が解決できなかった場合もユニット自体の表示は成立させ、
        /// アイコンとアニメーターだけ未設定のままにする。
        /// </summary>
        /// <param name="aUnit">表示対象のユニット。</param>
        /// <param name="aVisualDefinition">見た目の定義。カタログで解決できなければ null。</param>
        /// <param name="aSide">このユニットの陣営。</param>
        public void Initialize(BattleUnit aUnit, PPUnitVisualDefinition aVisualDefinition, BattleSide aSide)
        {
            mBattleUnit = aUnit;
            if (aVisualDefinition != null)
            {
                mUnitIcon.sprite = aVisualDefinition.UnitIcon;
                mAnimator.runtimeAnimatorController = aVisualDefinition.Animator;
            }
            else
            {
                Debug.LogWarning($"{aUnit.UnitId} のビジュアル定義が解決できませんでした");
            }
            if(aSide == BattleSide.Ally) mUnitIcon.transform.eulerAngles = new Vector3(0, 180, 0);
            mStatusWidget.Bind(new PPBattleUnitStatusSource(mBattleUnit));
        }

        /// <summary>選択できる状態かを切り替える。入力ステートが候補の絞り込みに使う。</summary>
        /// <param name="aSelectable">選択可能にするなら true。</param>
        public void SetSelectable(bool aSelectable)
        {
            mSelectButton.interactable = aSelectable;
        }

        /// <summary>フォーカス枠の表示を切り替える。</summary>
        /// <param name="aFocused">フォーカス中なら true。</param>
        public void SetFocused(bool aFocused)
        {
            if (mFocusFrame != null) mFocusFrame.SetActive(aFocused);
        }

        /// <summary>フォーカスを得たときに枠を出して通知する。</summary>
        public void OnSelect(BaseEventData _)
        {
            SetFocused(true);
            OnSelected?.Invoke(this);
        }

        /// <summary>フォーカスを失ったときに枠を消して通知する。</summary>
        public void OnDeselect(BaseEventData _)
        {
            SetFocused(false);
            OnDeselected?.Invoke(this);
        }

        /// <summary>攻撃モーションを再生する。</summary>
        /// <param name="aCommand">実行されたコマンド。現状は未使用だが、種別で演出を分ける拡張点。</param>
        public void CommandExecuted(BattleCommandBase aCommand)
        {
            mAnimator.SetTrigger("Attack");
        }

        /// <summary>
        /// 被弾モーションを再生する。撃破時は撃破モーションが優先されるため、生存時のみ再生する。
        /// </summary>
        /// <param name="aDmg">受けたダメージ量。現状は未使用。</param>
        public void PlayDamage(float aDmg)
        {
            if(mBattleUnit.IsAlive) mAnimator.SetTrigger("Damaged");
        }

        /// <summary>回復演出を再生する。未実装。</summary>
        /// <param name="aAmt">回復量。</param>
        public void PlayHeal(float aAmt)
        {

        }

        /// <summary>撃破モーションを再生する。</summary>
        public void PlayDefeat()
        {
            mAnimator.SetTrigger("Defeated");
        }

        /// <summary>状態異常アイコンを追加する。未実装。</summary>
        /// <param name="aEffect">付与されたエフェクト。</param>
        public void AddStatusIcon(StatusEffect aEffect)
        {

        }

        /// <summary>状態異常アイコンを除去する。未実装。</summary>
        /// <param name="aEffect">除去されたエフェクト。</param>
        public void RemoveStatusIcon(StatusEffect aEffect)
        {

        }
    }
}
