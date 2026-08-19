/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file VFXSequenceRootNode.cs
 * @author hqrse
 * @date 2026/08/19
 * @brief Play()の唯一の開始点となるノード(VFXは持たない)
 * =====================================*/

using System;

namespace VFXUtility
{
    // Play()の唯一の開始点となるノード。VFXは持たず、接続先を並列に開始するためだけのマーカー
    // グラフ内に1つだけ配置する想定(0個/2個以上はシーケンサーウィンドウ上で警告される)
    // 他ノードからの入射接続は受け付けない(NodeView側で入力ポート自体を持たせない)
    [Serializable]
    public class VFXSequenceRootNode : VFXSequenceNodeBase
    {
    }
}
