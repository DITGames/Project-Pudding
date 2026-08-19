/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file VFXSequenceNodeTypeMenuUtility.cs
 * @author hqrse
 * @date 2026/08/18
 * @brief ノード種別と表示名の対応、およびノード追加メニュー構築を共通化するユーティリティ
 * PPCore/Editor/PPManagedReferencePickerUtility.cs と同様のパターンをVFXUtility内に個別実装したもの
 * (VFXUtilityはPPCore/CommandBattleCoreの2層構造に属さない独立ユーティリティのため直接参照はしない)
 * =====================================*/

using System;

namespace VFXUtility.Editor
{
    internal static class VFXSequenceNodeTypeMenuUtility
    {
        // ノード種別(型)と表示名の対応。ノード追加メニュー・タイトル表示・ノードピッカーで共通利用する
        public static readonly (Type Type, string DisplayName)[] NodeTypes =
        {
            (typeof(VFXSequenceRootNode), "ルートノード"),
            (typeof(VFXSequenceNormalNode), "通常ノード"),
            (typeof(VFXSequenceEventNode), "イベントノード"),
            (typeof(VFXSequencePlayEventTriggerNode), "PlayEvent発火ノード"),
            (typeof(VFXSequenceStopNodeNode), "StopNodeノード"),
            (typeof(VFXSequenceStopVFXNode), "StopVFXノード"),
            (typeof(VFXSequenceStopAllNode), "StopAllノード"),
            (typeof(VFXSequenceSetParameterNode), "SetParameterノード"),
            (typeof(VFXSequenceRandomBranchNode), "ランダム分岐ノード"),
            (typeof(VFXSequenceConditionalBranchNode), "条件分岐ノード"),
            (typeof(VFXSequenceLoopNode), "ループノード"),
            (typeof(VFXSequenceLoopContinueNode), "ループ継続ノード"),
            (typeof(VFXSequenceWaitNode), "待機ノード"),
            (typeof(VFXSequenceGoalNode), "ゴールノード"),
        };

        // ノードインスタンスから表示名を解決する。未知の型はクラス名をそのまま返す
        // aNode : 表示名を解決するノード
        public static string GetDisplayName(VFXSequenceNodeBase aNode)
        {
            Type nodeType = aNode.GetType();
            foreach ((Type type, string displayName) in NodeTypes)
            {
                if (type == nodeType)
                {
                    return displayName;
                }
            }
            return nodeType.Name;
        }
    }
}
