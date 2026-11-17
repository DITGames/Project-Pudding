/* =====================================
 * Copyright WabisabiAndons. All rights reserved.
 * @file PPBattleDetailMenuView.cs
 * @author hqrse
 * @date 2026/07/08
 * @brief バトル中のユニット詳細ビュー
 * =====================================*/

using System;
using System.Linq;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Serialization;
using UnityEngine.UI;
using AttributeUtility;

namespace PPCore
{
    // ユニットの詳細を画面いっぱいに表示するビュー。左にアイコンと名前、右に「ステータス」「状態」のタブを並べる
    // 表示は Show を呼んだ時点のスナップショットで、変更を購読しない
    public class PPBattleDetailMenuView : MonoBehaviour
    {
        /* 定数 */

        // 値が無いときに表示する文字
        private const string EmptyText = "なし";

        /* インスペクタ設定 */

        [Header("ユニット")]
        [Label("アイコン")]
        [SerializeField] private Image mIcon;
        [Label("ユニット名")]
        [SerializeField] private TMP_Text mNameLabel;
        [Label("属性")]
        [SerializeField] private TMP_Text mAttributeLabel;
        // アイコンを引くカタログ。未設定ならアイコンを隠す
        [Label("ユニットビジュアルカタログ")]
        [SerializeField] private PPUnitVisualCatalog mUnitVisualCatalog;

        [Header("タブ")]
        [Label("ステータスタブ")]
        [SerializeField] private Button mStatusTabButton;
        [Label("状態タブ")]
        [SerializeField] private Button mStateTabButton;
        [Label("ステータスの中身")]
        [SerializeField] private GameObject mStatusPanel;
        [Label("状態の中身")]
        [SerializeField] private GameObject mStatePanel;
        [Label("選択中のタブの色")]
        [SerializeField] private Color mSelectedTabColor = new Color(1f, 0.82f, 0.1f);
        [Label("選択していないタブの色")]
        [SerializeField] private Color mUnselectedTabColor = new Color(0.92f, 0.92f, 0.92f);

        [Header("ステータス")]
        // HP ラベル（現在値/最大値）
        [FormerlySerializedAs("mHPLabel")]
        [Label("HP")]
        [SerializeField] private TMP_Text mHpLabel;
        // HP バーの塗り部分。未設定なら表示しない
        [Label("HPバー")]
        [SerializeField] private Image mHpFill;
        [Label("攻撃力")]
        [SerializeField] private TMP_Text mAttackLabel;
        [Label("防御力")]
        [SerializeField] private TMP_Text mDefenseLabel;
        [Label("素早さ")]
        [SerializeField] private TMP_Text mSpeedLabel;
        [Label("きようさ")]
        [SerializeField] private TMP_Text mDexterityLabel;
        [Label("行動コスト")]
        [SerializeField] private TMP_Text mActionCostLabel;
        [Label("コインゲージ")]
        [SerializeField] private TMP_Text mCoinGaugeLabel;
        // コインゲージのバーの塗り部分。未設定なら表示しない
        [Label("コインゲージのバー")]
        [SerializeField] private Image mCoinFill;
        [Label("スキルゲージ")]
        [SerializeField] private TMP_Text mSkillGaugeLabel;
        // スキルゲージのバーの塗り部分。未設定なら表示しない
        [Label("スキルゲージのバー")]
        [SerializeField] private Image mSkillFill;
        [Label("スキル一覧")]
        [SerializeField] private TMP_Text mSkillListLabel;

        [Header("状態")]
        [Label("状態異常一覧")]
        [SerializeField] private TMP_Text mStatusEffectListLabel;

        [Header("操作")]
        [Label("戻るボタン")]
        [SerializeField] private Button mBackButton;

        /* イベント */

        // 戻るが押されたときに発火する
        public event Action OnBackRequested;

        /* 初期化・接続 */

        // ビューを指定した位置へ移動させる。画面いっぱいに出すため、通常は使わない
        public void AttachTo(RectTransform aAnchor)
        {
            var rt = (RectTransform)transform;
            rt.SetParent(aAnchor, false);
            rt.anchoredPosition = Vector2.zero;
        }

        /* 操作 */

        // 戻る操作を外部へ通知する
        private void RaiseBack() => OnBackRequested?.Invoke();

        // 指定したタブへ切り替える。中身の表示と、タブの色を合わせる
        private void SelectTab(bool aIsStatus)
        {
            if (mStatusPanel != null) mStatusPanel.SetActive(aIsStatus);
            if (mStatePanel != null) mStatePanel.SetActive(!aIsStatus);
            PaintTab(mStatusTabButton, aIsStatus);
            PaintTab(mStateTabButton, !aIsStatus);
        }

        /* 表示 */

        // ユニットの情報を各ラベルへ流し込んで、ステータスタブを開いた状態で表示する
        // 表示中のパラメータはバフ込みの現在値
        public void Show(BattleUnit aUnit)
        {
            gameObject.SetActive(true);
            // 他の UI より手前に出す
            transform.SetAsLastSibling();

            ShowUnit(aUnit);
            ShowStatus(aUnit);
            SetText(mStatusEffectListLabel, BuildStatusEffectText(aUnit));

            mBackButton.onClick.AddListener(RaiseBack);
            if (mStatusTabButton != null) mStatusTabButton.onClick.AddListener(HandleStatusTab);
            if (mStateTabButton != null) mStateTabButton.onClick.AddListener(HandleStateTab);

            SelectTab(true);
            EventSystem.current.SetSelectedGameObject(mStatusTabButton != null ? mStatusTabButton.gameObject : mBackButton.gameObject);
        }

        // ビューを閉じ、ボタンのリスナーを解除する
        public void Hide()
        {
            mBackButton.onClick.RemoveListener(RaiseBack);
            if (mStatusTabButton != null) mStatusTabButton.onClick.RemoveListener(HandleStatusTab);
            if (mStateTabButton != null) mStateTabButton.onClick.RemoveListener(HandleStateTab);
            gameObject.SetActive(false);
        }

        // 左側（アイコン・名前・属性）を表示する
        private void ShowUnit(BattleUnit aUnit)
        {
            SetText(mNameLabel, aUnit.DisplayName);

            var ppUnit = aUnit as PPBattleUnit;
            SetText(mAttributeLabel, ppUnit != null ? ToDisplayString(ppUnit.TypeAttribute) : "");

            if (mIcon == null) return;
            var sprite = mUnitVisualCatalog != null ? mUnitVisualCatalog.Resolve(aUnit.UnitId)?.UnitIcon : null;
            mIcon.sprite = sprite;
            mIcon.enabled = sprite != null;
        }

        // ステータスタブの中身を表示する
        private void ShowStatus(BattleUnit aUnit)
        {
            var hp = aUnit.Parameters.Hp;
            float maxHp = hp.Max.CurrentValue;
            SetText(mHpLabel, $"{hp.CurrentValue:0}/{maxHp:0}");
            SetFill(mHpFill, hp.CurrentValue, maxHp);
            SetText(mAttackLabel, aUnit.Parameters.Attack.CurrentValue.ToString("0"));
            SetText(mDefenseLabel, aUnit.Parameters.Defense.CurrentValue.ToString("0"));
            SetText(mSpeedLabel, aUnit.Parameters.Speed.CurrentValue.ToString("0"));

            if (aUnit is PPBattleUnit ppUnit)
            {
                var extra = ppUnit.ExtraParameters;
                SetText(mDexterityLabel, extra.Dexterity.CurrentValue.ToString("0"));
                SetText(mActionCostLabel, ppUnit.ActionCost.ToString("0.#"));
                SetText(mCoinGaugeLabel, $"{extra.CoinGauge.Current:0.#}/{extra.CoinGauge.Max.CurrentValue:0.#}");
                SetText(mSkillGaugeLabel, $"{extra.SkillGauge.Current:0.#}/{extra.SkillGauge.Max.CurrentValue:0.#}");
                SetFill(mCoinFill, extra.CoinGauge.Current, extra.CoinGauge.Max.CurrentValue);
                SetFill(mSkillFill, extra.SkillGauge.Current, extra.SkillGauge.Max.CurrentValue);
            }

            SetText(mSkillListLabel, aUnit.Skills.Count > 0
                ? string.Join("\n", aUnit.Skills.Select(BuildSkillLine))
                : EmptyText);
        }

        // タブの見た目を、選択中かどうかに合わせて塗る
        private void PaintTab(Button aTab, bool aIsSelected)
        {
            if (aTab == null || aTab.targetGraphic == null) return;
            aTab.targetGraphic.color = aIsSelected ? mSelectedTabColor : mUnselectedTabColor;
        }

        /* 説明文 */

        // スキル 1 つ分の行を組み立てる。必要スキルゲージ量とクールタイム中の残り秒数を添える
        private static string BuildSkillLine(BattleSkill aSkill)
        {
            float cost = (aSkill.SourceDefinition as PPSkillDefinition)?.SkillGaugeCost ?? 0f;
            string line = $"{aSkill.DisplayName}（{cost:0.#}）";
            return aSkill.IsCooldown ? $"{line}　クールタイム 残り{aSkill.RemainingCooldown:0}秒" : line;
        }

        // 掛かっている状態異常の一覧を組み立てる。名前・効果の要約・残り時間を 1 行ずつ並べる
        private static string BuildStatusEffectText(BattleUnit aUnit)
        {
            if (aUnit.ActiveStatusEffects.Count == 0) return EmptyText;

            var sb = new StringBuilder();
            foreach (var effect in aUnit.ActiveStatusEffects)
            {
                if (sb.Length > 0) sb.Append('\n');
                sb.Append(effect.DisplayName);

                string summary = (effect.SourceDefinition as PPEffectDefinition)?.BuildString();
                if (!string.IsNullOrEmpty(summary)) sb.Append("　").Append(summary);

                float remaining = effect.RemainingSeconds;
                sb.Append("　").Append(remaining < 0f ? "永続" : $"残り{Mathf.CeilToInt(remaining)}秒");
            }
            return sb.ToString();
        }

        // 属性の表示名を返す
        private static string ToDisplayString(PPTypeAttribute aAttribute) => aAttribute switch
        {
            PPTypeAttribute.Fire => PPTypeAttributeDefinition.TypeFire,
            PPTypeAttribute.Water => PPTypeAttributeDefinition.TypeWater,
            PPTypeAttribute.Earth => PPTypeAttributeDefinition.TypeEarth,
            PPTypeAttribute.Shine => PPTypeAttributeDefinition.TypeShine,
            PPTypeAttribute.Dark => PPTypeAttributeDefinition.TypeDark,
            _ => PPTypeAttributeDefinition.TypeNormal,
        };

        /* イベントハンドラ */

        // ステータスタブが押されたとき
        private void HandleStatusTab() => SelectTab(true);

        // 状態タブが押されたとき
        private void HandleStateTab() => SelectTab(false);

        /* ヘルパー */

        // ラベルが設定されていれば文字を入れる
        private static void SetText(TMP_Text aLabel, string aText)
        {
            if (aLabel != null) aLabel.text = aText;
        }

        // バーが設定されていれば、現在値 / 上限 で塗る量を決める
        private static void SetFill(Image aFill, float aCurrent, float aMax)
        {
            if (aFill != null) aFill.fillAmount = aMax > 0f ? Mathf.Clamp01(aCurrent / aMax) : 0f;
        }
    }
}
