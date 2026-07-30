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
    /// <summary>
    /// 名前と HP バーを表示する共通ウィジェット。
    /// <para>
    /// 表示元を <see cref="IPPUnitStatusSource"/> として受け取るため、
    /// バトルユニットに限らず同じインターフェースを満たすものなら何でも表示できる。
    /// バインド中は変更通知を購読し、値が変わるたびに再描画する。
    /// </para>
    /// </summary>
    public class PPUnitStatusWidget : MonoBehaviour
    {
        /// <summary>HP バーの塗り部分。fillAmount で残量を表現する。</summary>
        [Label("HPバー")]
        [SerializeField] private Image mHpFill;

        /// <summary>名前ラベル。</summary>
        [Label("名前ラベル")]
        [SerializeField] private TMP_Text mNameLabel;

        /// <summary>表示元。未バインドなら null。</summary>
        private IPPUnitStatusSource mSource;

        /// <summary>
        /// 表示元を差し替える。
        /// 前のバインドが残っていても二重購読にならないよう、先に解除してから繋ぎ直す。
        /// </summary>
        /// <param name="aSource">表示元。</param>
        public void Bind(IPPUnitStatusSource aSource)
        {
            Unbind();
            mSource = aSource;
            mSource.Changed += Refresh;
            Refresh();
        }

        /// <summary>表示元との接続を切る。購読を残さないため破棄時にも呼ぶ。</summary>
        public void Unbind()
        {
            if (mSource != null) mSource.Changed -= Refresh;
            mSource = null;
        }

        /// <summary>表示元の現在値で名前と HP バーを更新する。最大 HP が 0 なら空表示にする。</summary>
        private void Refresh()
        {
            mNameLabel.text = mSource.DisplayName;
            mHpFill.fillAmount = mSource.MaxHP > 0 ? mSource.CurrentHP / mSource.MaxHP : 0;
        }
    }
}
