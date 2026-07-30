/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file PPBattleDetailMenuView.cs
 * @author hqrse
 * @date 2026/07/08
 * @brief バトル中のユニット詳細ビュー
 * =====================================*/

using System;
using System.Linq;
using CommandBattleCore;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace PPCore
{
    /// <summary>
    /// ユニットのステータス・所持スキル・状態異常を一覧表示する詳細ビュー。
    /// <para>
    /// 表示は <see cref="Show"/> を呼んだ時点のスナップショットで、変更を購読しない。
    /// 表示中に値が変わっても更新されないが、入力中は timeScale が 0 で
    /// 盤面が止まっているため実用上は問題にならない。
    /// </para>
    /// </summary>
    public class PPBattleDetailMenuView : MonoBehaviour
    {
        /// <summary>ユニット名ラベル。</summary>
        [Label("ユニット名")]
        [SerializeField] private TMP_Text mNameLabel;
        /// <summary>HP ラベル（現在値/最大値）。</summary>
        [FormerlySerializedAs("mHPLabel")]
        [Label("HP")]
        [SerializeField] private TMP_Text mHpLabel;
        /// <summary>攻撃力ラベル。</summary>
        [Label("攻撃力")]
        [SerializeField] private TMP_Text mAttackLabel;
        /// <summary>防御力ラベル。</summary>
        [Label("防御力")]
        [SerializeField] private TMP_Text mDefenseLabel;
        /// <summary>素早さラベル。</summary>
        [Label("素早さ")]
        [SerializeField] private TMP_Text mSpeedLabel;
        /// <summary>所持スキル名の一覧ラベル。</summary>
        [Label("スキル一覧")]
        [SerializeField]private TMP_Text mSkillListLabel;
        /// <summary>付与中の状態異常名の一覧ラベル。</summary>
        [Label("状態異常一覧")]
        [SerializeField] private TMP_Text mStatusEffectListLabel;
        /// <summary>戻るボタン。</summary>
        [Label("戻るボタン")]
        [SerializeField] private Button mBackButton;

        /// <summary>戻るが押されたときに発火する。</summary>
        public event Action OnBackRequested;

        /// <summary>
        /// ビューを指定した位置へ移動させる。レイアウトを崩さないよう worldPositionStays は false。
        /// </summary>
        /// <param name="aAnchor">配置先の親となる RectTransform。</param>
        public void AttachTo(RectTransform aAnchor)
        {
            var rt = (RectTransform)transform;
            rt.SetParent(aAnchor, false);
            rt.anchoredPosition = Vector2.zero;
        }

        /// <summary>
        /// ユニットの情報を各ラベルへ流し込んで表示する。
        /// 表示中のパラメータはバフ込みの現在値。スキルと状態異常は
        /// 空のときにプレースホルダを出す。
        /// </summary>
        /// <param name="aUnit">表示対象のユニット。</param>
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

            // 状態異常アイコン実装時にアイコンと残りターン数を出す形に変更する？
            mStatusEffectListLabel.text = aUnit.ActiveStatusEffects.Count > 0
                ? string.Join("\n", aUnit.ActiveStatusEffects.Select(s => s.DisplayName))
                : "なし";

            mBackButton.onClick.AddListener(RaiseBack);
            EventSystem.current.SetSelectedGameObject(mBackButton.gameObject);
        }

        /// <summary>ビューを閉じ、戻るボタンのリスナーを解除する。</summary>
        public void Hide()
        {
            mBackButton.onClick.RemoveListener(RaiseBack);
            gameObject.SetActive(false);
        }

        /// <summary>戻る操作を外部へ通知する。</summary>
        private void RaiseBack() => OnBackRequested?.Invoke();
    }
}
