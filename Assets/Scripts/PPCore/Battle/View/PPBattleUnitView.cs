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
    // 戦場に並ぶユニット 1 体分の表示
    // 見た目（アイコン・アニメーション）の再生と、選択対象としての振る舞いを兼ねる
    // 演出メソッドは PPBattleUnitViewBinder がバトルイベントを受けて呼び出す
    // メニューの表示位置は MenuAnchor として公開し、
    // 入力ステート側がここを基準にコマンドメニューを配置する
    public class PPBattleUnitView : MonoBehaviour, ISelectHandler, IDeselectHandler
    {
        [Label("ステータスウィジェット")]
        [SerializeField] private PPUnitStatusWidget mStatusWidget;
        [Label("アイコン")]
        [SerializeField] private Image mUnitIcon;
        [Label("アニメーター")]
        [SerializeField] private Animator mAnimator;
        [Label("選択ボタン")]
        [SerializeField] private Button mSelectButton;
        [Label("フォーカス枠")]
        [SerializeField] private GameObject mFocusFrame;
        [Label("メニューアンカー")]
        [SerializeField] private RectTransform mMenuAnchor;

        // このビューが表すユニット
        private BattleUnit mBattleUnit;
        // ステータスウィジェットへ渡した表示ソース。破棄時に購読を解除するため保持する
        private PPBattleUnitStatusSource mStatusSource;
        // このビューが表すユニット
        public BattleUnit BattleUnit => mBattleUnit;
        // フォーカス対象となるオブジェクト
        public GameObject SelectableObject => mSelectButton.gameObject;
        // コマンドメニューを配置する位置の基準
        public RectTransform MenuAnchor => mMenuAnchor;

        // ユニットが決定されたときの通知
        public event Action<PPBattleUnitView> OnDecided;

        // フォーカスが乗ったときの通知
        public event Action<PPBattleUnitView> OnSelected;
        // フォーカスが外れたときの通知
        public event Action<PPBattleUnitView> OnDeselected;

        // 選択ボタンの押下を決定通知へ中継する
        private void Awake() => mSelectButton.onClick.AddListener(() => OnDecided?.Invoke(this));

        // 表示対象のユニットと見た目を設定する
        // 味方は敵と向き合う形にするためアイコンを左右反転させる
        // 見た目定義が解決できなかった場合もユニット自体の表示は成立させ、
        // アイコンとアニメーターだけ未設定のままにする
        // aUnit : 表示対象のユニット
        // aVisualDefinition : 見た目の定義。カタログで解決できなければ null
        // aSide : このユニットの陣営
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

            // 初期化が繰り返された場合に前回分の購読が残らないよう作り直す
            mStatusSource?.Dispose();
            mStatusSource = new PPBattleUnitStatusSource(mBattleUnit);
            mStatusWidget.Bind(mStatusSource);
        }

        // 破棄時にステータス表示の購読を解除する
        // ウィジェット側とソース側で購読先が違うため、両方を明示的に切る
        private void OnDestroy()
        {
            // 子オブジェクトの破棄順は保証されないため、先に破棄済みの可能性を見る
            if (mStatusWidget != null) mStatusWidget.Unbind();
            mStatusSource?.Dispose();
            mStatusSource = null;
        }

        // 選択できる状態かを切り替える。入力ステートが候補の絞り込みに使う
        // aSelectable : 選択可能にするなら true
        public void SetSelectable(bool aSelectable)
        {
            mSelectButton.interactable = aSelectable;
        }

        // フォーカス枠の表示を切り替える
        // aFocused : フォーカス中なら true
        public void SetFocused(bool aFocused)
        {
            if (mFocusFrame != null) mFocusFrame.SetActive(aFocused);
        }

        // フォーカスを得たときに枠を出して通知する
        public void OnSelect(BaseEventData _)
        {
            SetFocused(true);
            OnSelected?.Invoke(this);
        }

        // フォーカスを失ったときに枠を消して通知する
        public void OnDeselect(BaseEventData _)
        {
            SetFocused(false);
            OnDeselected?.Invoke(this);
        }

        // 攻撃モーションを再生する
        // aCommand : 実行されたコマンド。現状は未使用だが、種別で演出を分ける拡張点
        public void CommandExecuted(BattleCommandBase aCommand)
        {
            mAnimator.SetTrigger("Attack");
        }

        // 被弾モーションを再生する。撃破時は撃破モーションが優先されるため、生存時のみ再生する
        // aDmg : 受けたダメージ量。現状は未使用
        public void PlayDamage(float aDmg)
        {
            if(mBattleUnit.IsAlive) mAnimator.SetTrigger("Damaged");
        }

        // 回復演出を再生する。未実装
        // aAmt : 回復量
        public void PlayHeal(float aAmt)
        {

        }

        // 撃破モーションを再生する
        public void PlayDefeat()
        {
            mAnimator.SetTrigger("Defeated");
        }

        // 状態異常アイコンを追加する。未実装
        // aEffect : 付与されたエフェクト
        public void AddStatusIcon(StatusEffect aEffect)
        {

        }

        // 状態異常アイコンを除去する。未実装
        // aEffect : 除去されたエフェクト
        public void RemoveStatusIcon(StatusEffect aEffect)
        {

        }
    }
}
