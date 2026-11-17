/* =====================================
 * Copyright WabisabiAndons. All rights reserved.
 * @file PPUnitStatusWidget.cs
 * @author hqrse
 * @date 2026/06/25
 * @brief ユニットのステータス表示共通ウィジェット
 * =====================================*/

using AttributeUtility;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PPCore
{
    // 名前・HP バー・コインゲージバー・スキルゲージバー・選択中のスキル名・狙う相手の名前を表示する共通ウィジェット。表示元は IPPUnitStatusSource として受け取る
    // バインド中は変更通知を購読し、値が変わるたびに再描画する
    public class PPUnitStatusWidget : MonoBehaviour
    {
        /* 定数 */

        // ターゲットラベルで、相手の名前の前に付ける文字
        private const string TargetPrefix = "→ ";

        /* インスペクタ設定 */

        // HP バーの塗り部分。fillAmount で残量を表現する
        [Label("HPバー")]
        [SerializeField] private Image mHpFill;
        // コインゲージバーの塗り部分。次の行動までの溜まり具合を表す。未設定なら表示しない
        [Label("コインゲージバー")]
        [SerializeField] private Image mCoinFill;
        // スキルゲージバーの塗り部分。上限に対する溜まり具合を表す。未設定なら表示しない
        [Label("スキルゲージバー")]
        [SerializeField] private Image mSkillFill;
        [Label("名前ラベル")]
        [SerializeField] private TMP_Text mNameLabel;
        // 選択中のスキル名を出すラベル。未選択のときは隠す。未設定なら表示しない
        [Label("選択中スキルラベル")]
        [SerializeField] private TMP_Text mSelectedSkillLabel;
        // 狙う相手に指定したユニットの名前を出すラベル。未指定のときは隠す。未設定なら表示しない
        [Label("ターゲットラベル")]
        [SerializeField] private TMP_Text mTargetLabel;

        /* 状態 */

        // 表示元。未バインドなら null
        private IPPUnitStatusSource mSource;

        /* 初期化・接続 */

        // 表示元を差し替える
        // 前のバインドが残っていても二重購読にならないよう、先に解除してから繋ぎ直す
        public void Bind(IPPUnitStatusSource aSource)
        {
            Unbind();
            mSource = aSource;
            mSource.Changed += Refresh;
            Refresh();
        }

        // 表示元との接続を切る。購読を残さないため破棄時にも呼ぶ
        public void Unbind()
        {
            if (mSource != null) mSource.Changed -= Refresh;
            mSource = null;
        }

        /* 表示 */

        // 表示元の現在値で名前・各バー・選択中のスキル名・狙う相手の名前を更新する。最大 HP が 0 なら空表示にする
        private void Refresh()
        {
            mNameLabel.text = mSource.DisplayName;
            mHpFill.fillAmount = mSource.MaxHP > 0 ? mSource.CurrentHP / mSource.MaxHP : 0;
            if (mCoinFill != null) mCoinFill.fillAmount = mSource.CoinGaugeRate;
            if (mSkillFill != null) mSkillFill.fillAmount = mSource.SkillGaugeRate;
            if (mSelectedSkillLabel != null)
            {
                string skillName = mSource.SelectedSkillName;
                mSelectedSkillLabel.text = skillName;
                mSelectedSkillLabel.gameObject.SetActive(!string.IsNullOrEmpty(skillName));
            }
            if (mTargetLabel != null)
            {
                string targetName = mSource.DesignatedTargetName;
                mTargetLabel.text = string.IsNullOrEmpty(targetName) ? string.Empty : TargetPrefix + targetName;
                mTargetLabel.gameObject.SetActive(!string.IsNullOrEmpty(targetName));
            }
        }
    }
}
