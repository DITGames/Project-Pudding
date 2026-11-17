/* =====================================
 * Copyright WabisabiAndons. All rights reserved.
 * @file PPBattleDetailMenuView.cs
 * @author hqrse
 * @date 2026/07/08
 * @brief バトル中のユニット詳細ビュー
 * =====================================*/

using System;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Serialization;
using UnityEngine.UI;
using AttributeUtility;

namespace PPCore
{
    // ユニットのステータス・所持スキル・状態異常を一覧表示する詳細ビュー
    // 表示は Show を呼んだ時点のスナップショットで、変更を購読しない
    public class PPBattleDetailMenuView : MonoBehaviour
    {
        /* インスペクタ設定 */

        [Label("ユニット名")]
        [SerializeField] private TMP_Text mNameLabel;

        // HP ラベル（現在値/最大値）
        [FormerlySerializedAs("mHPLabel")]
        [Label("HP")]
        [SerializeField] private TMP_Text mHpLabel;

        [Label("攻撃力")]
        [SerializeField] private TMP_Text mAttackLabel;
        [Label("防御力")]
        [SerializeField] private TMP_Text mDefenseLabel;
        [Label("素早さ")]
        [SerializeField] private TMP_Text mSpeedLabel;
        [Label("スキル一覧")]
        [SerializeField]private TMP_Text mSkillListLabel;
        [Label("状態異常一覧")]
        [SerializeField] private TMP_Text mStatusEffectListLabel;
        [Label("戻るボタン")]
        [SerializeField] private Button mBackButton;

        /* イベント */

        // 戻るが押されたときに発火する
        public event Action OnBackRequested;

        /* 初期化・接続 */

        // ビューを指定した位置へ移動させる。レイアウトを崩さないよう worldPositionStays は false
        // aAnchor : 配置先の親となる RectTransform
        public void AttachTo(RectTransform aAnchor)
        {
            var rt = (RectTransform)transform;
            rt.SetParent(aAnchor, false);
            rt.anchoredPosition = Vector2.zero;
        }

        /* 操作 */

        // 戻る操作を外部へ通知する
        private void RaiseBack() => OnBackRequested?.Invoke();

        /* 表示 */

        // ユニットの情報を各ラベルへ流し込んで表示する
        // 表示中のパラメータはバフ込みの現在値。スキルと状態異常は空のときにプレースホルダを出す
        public void Show(BattleUnit aUnit)
        {
            gameObject.SetActive(true);

            mNameLabel.text = aUnit.DisplayName;
            mHpLabel.text = $"{aUnit.Parameters.Hp.CurrentValue}/{aUnit.Parameters.Hp.Max.CurrentValue:0}";
            mAttackLabel.text = aUnit.Parameters.Attack.CurrentValue.ToString("0");
            mDefenseLabel.text = aUnit.Parameters.Defense.CurrentValue.ToString("0");
            mSpeedLabel.text = aUnit.Parameters.Speed.CurrentValue.ToString("0");

            // アイコン実装でアイコンを出す形に変更する？
            mSkillListLabel.text = aUnit.Skills.Count > 0
                ? string.Join("\n", aUnit.Skills.Select(s => s.DisplayName))
                : "-";

            // 状態異常アイコン実装時にアイコンと残り時間を出す形に変更する？
            mStatusEffectListLabel.text = aUnit.ActiveStatusEffects.Count > 0
                ? string.Join("\n", aUnit.ActiveStatusEffects.Select(s => s.DisplayName))
                : "なし";

            mBackButton.onClick.AddListener(RaiseBack);
            EventSystem.current.SetSelectedGameObject(mBackButton.gameObject);
        }

        // ビューを閉じ、戻るボタンのリスナーを解除する
        public void Hide()
        {
            mBackButton.onClick.RemoveListener(RaiseBack);
            gameObject.SetActive(false);
        }
    }
}
