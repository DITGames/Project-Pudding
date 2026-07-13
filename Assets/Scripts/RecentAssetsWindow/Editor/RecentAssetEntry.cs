/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file RecentAssetEntry.cs
 * @author hqrse
 * @date 2026/07/10
 * @brief 最近開いたアセット1件分のデータ
 * =====================================*/
using System;

namespace RecentAssetsWindow.Editor
{
    [Serializable]
    public class RecentAssetEntry
    {
        public string Guid;
        public long OpenedAtTicks;

        public RecentAssetEntry(string aGuid, long aOpenedAtTicks)
        {
            Guid = aGuid;
            OpenedAtTicks = aOpenedAtTicks;
        }
    }
}
