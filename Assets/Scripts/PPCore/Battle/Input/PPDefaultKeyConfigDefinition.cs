/* =====================================
 * Copyright WabisabiAndons. All rights reserved.
 * @file PPDefaultKeyConfigDefinition.cs
 * @author hqrse
 * @date 2026/11/18
 * @brief キー設定で変更できる操作の既定の割り当て（操作 1 つ分の定義・既定の割り当ての定義）
 * =====================================*/

using System;
using System.Collections.Generic;
using AttributeUtility;
using UnityEngine;

namespace PPCore
{
    // 操作 1 つ分の既定の割り当て。キーボード 2 枠・ゲームパッド 2 枠を、入力の場所の文字列で持つ
    // 場所は「<Keyboard>/space」「<Mouse>/rightButton」「<Gamepad>/buttonSouth」の形で書く。空なら未割り当て
    [Serializable]
    public sealed class PPDefaultKeyBindingEntry
    {
        /* インスペクタ設定 */

        // 入力アクションの Battle マップにある操作の名前(Submit など)
        [Label("操作の名前")]
        [SerializeField] private string mActionName;
        [Label("キーボード1")]
        [SerializeField] private string mKeyboard1;
        [Label("キーボード2")]
        [SerializeField] private string mKeyboard2;
        [Label("パッド1")]
        [SerializeField] private string mGamepad1;
        [Label("パッド2")]
        [SerializeField] private string mGamepad2;

        /* プロパティ */

        public string ActionName => mActionName;

        /* 問い合わせ */

        // 指定したデバイスの何枠目の場所を返す。枠の番号が範囲外なら null
        public string GetPath(bool aIsGamepad, int aSlot) => (aIsGamepad, aSlot) switch
        {
            (false, 0) => mKeyboard1 ?? string.Empty,
            (false, 1) => mKeyboard2 ?? string.Empty,
            (true, 0) => mGamepad1 ?? string.Empty,
            (true, 1) => mGamepad2 ?? string.Empty,
            _ => null,
        };
    }

    // キー設定で変更できる操作の既定の割り当て。PPRuntimeSettings から参照し、「既定に戻す」もこの値に戻す
    // 載っていない操作は、入力アクションのアセットに書かれた割り当てを既定として使う
    [CreateAssetMenu(fileName = "PDKC_Default", menuName = "Project-Pudding/Settings/PPDefaultKeyConfigDefinition")]
    public class PPDefaultKeyConfigDefinition : ScriptableObject
    {
        /* インスペクタ設定 */

        [Label("既定の割り当て", true)]
        [SerializeField] private List<PPDefaultKeyBindingEntry> mEntries = new();

        /* 問い合わせ */

        // 操作の、指定したデバイスの何枠目の既定の場所を返す。載っていなければ false
        public bool TryGetPath(string aActionName, bool aIsGamepad, int aSlot, out string aPath)
        {
            foreach (var entry in mEntries)
            {
                if (entry == null || entry.ActionName != aActionName) continue;
                aPath = entry.GetPath(aIsGamepad, aSlot);
                return aPath != null;
            }
            aPath = null;
            return false;
        }
    }
}
