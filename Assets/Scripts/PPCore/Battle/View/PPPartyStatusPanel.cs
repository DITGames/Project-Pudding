/* =====================================
 * Copyright WabisabiAndons. All rights reserved.
 * @file PPPartyStatusPanel.cs
 * @author hqrse
 * @date 2026/11/18
 * @brief パーティ全員の状態をまとめて出す画面端のパネル
 * =====================================*/

using System;
using AttributeUtility;
using UnityEngine;

namespace PPCore
{
    // パーティのアクティブメンバー分の行を並べ、画面端にまとめて状態を出す
    public class PPPartyStatusPanel : MonoBehaviour
    {
        /* インスペクタ設定 */

        [Label("行のプレハブ")]
        [SerializeField] private PPUnitStatusRow mRowPrefab;
        // 行を並べる親。上から順に並べる
        [Label("並べる場所")]
        [SerializeField] private RectTransform mContent;

        /* 初期化・接続 */

        // パーティのアクティブメンバー分の行を作り直す
        public void Bind(BattleParty aParty, Func<BattleUnit, Sprite> aIconResolver)
        {
            for (int i = mContent.childCount - 1; i >= 0; i--) Destroy(mContent.GetChild(i).gameObject);
            foreach (var unit in aParty.ActiveMembers)
            {
                var row = Instantiate(mRowPrefab, mContent);
                row.Bind(unit, aIconResolver);
            }
        }
    }
}
