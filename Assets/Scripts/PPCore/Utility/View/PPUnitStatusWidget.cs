/* =====================================
 * Copyright hqrse. All rights reserved.
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
    // 名前と HP バーを表示する共通ウィジェット
    // 表示元を IPPUnitStatusSource として受け取るため、
    // バトルユニットに限らず同じインターフェースを満たすものなら何でも表示できる
    // バインド中は変更通知を購読し、値が変わるたびに再描画する
    public class PPUnitStatusWidget : MonoBehaviour
    {
        // HP バーの塗り部分。fillAmount で残量を表現する
        [Label("HPバー")]
        [SerializeField] private Image mHpFill;

        [Label("名前ラベル")]
        [SerializeField] private TMP_Text mNameLabel;

        // 表示元。未バインドなら null
        private IPPUnitStatusSource mSource;

        // 表示元を差し替える
        // 前のバインドが残っていても二重購読にならないよう、先に解除してから繋ぎ直す
        // aSource : 表示元
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

        // 表示元の現在値で名前と HP バーを更新する。最大 HP が 0 なら空表示にする
        private void Refresh()
        {
            mNameLabel.text = mSource.DisplayName;
            mHpFill.fillAmount = mSource.MaxHP > 0 ? mSource.CurrentHP / mSource.MaxHP : 0;
        }
    }
}
