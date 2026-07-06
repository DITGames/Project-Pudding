/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file PPUnitStatusWidget.cs
 * @author hqrse
 * @date 2026/06/25
 * @brief ユニットのステータス表示共通ウィジェット
 * =====================================*/
using CommandBattleCore;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PPCore
{
    public class PPUnitStatusWidget : MonoBehaviour
    {
        [Label("HPバー")]
        [SerializeField] private Image mHpFill;

        [Label("名前ラベル")] 
        [SerializeField] private TMP_Text mNameLabel;

        private IPPUnitStatusSource mSource;

        public void Bind(IPPUnitStatusSource aSource)
        {
            Unbind();
            mSource = aSource;
            mSource.Changed += Refresh;
            Refresh();
        }

        public void Unbind()
        {
            if (mSource != null) mSource.Changed -= Refresh;
            mSource = null;
        }

        private void Refresh()
        {
            mNameLabel.text = mSource.DisplayName;
            mHpFill.fillAmount = mSource.MaxHP > 0 ? mSource.CurrentHP / mSource.MaxHP : 0;
        }
    }
}