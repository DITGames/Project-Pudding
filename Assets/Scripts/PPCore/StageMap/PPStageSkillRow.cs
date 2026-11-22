/* =====================================
 * Copyright WabisabiAndons. All rights reserved.
 * @file PPStageSkillRow.cs
 * @author hqrse
 * @date 2026/11/21
 * @brief ユニット詳細に並べる、スキル 1 つ分の行
 * =====================================*/

using TMPro;
using UnityEngine;
using AttributeUtility;

namespace PPCore
{
    // スキルの名前・説明・消費ゲージを出す 1 行
    public class PPStageSkillRow : MonoBehaviour
    {
        /* インスペクタ設定 */

        [Label("名前")]
        [SerializeField] private TMP_Text mName;
        [Label("説明")]
        [SerializeField] private TMP_Text mDescription;
        [Label("消費ゲージ")]
        [SerializeField] private TMP_Text mCost;

        /* 表示 */

        public void Set(string aName, string aDescription, string aCost)
        {
            if (mName != null) mName.text = aName;
            if (mDescription != null) mDescription.text = aDescription;
            if (mCost != null) mCost.text = aCost;
        }
    }
}
