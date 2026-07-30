/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file PPConditionMenuAttribute.cs
 * @author hqrse
 * @date 2026/07/23
 * @brief 条件クラスをピッカーのツリー上でどこに表示するかを指定する属性
 * =====================================*/

using System;

namespace PPCore
{
    // AI 条件クラスの、ピッカー UI 上での配置場所を指定する属性
    // PPConditionTreeView がツリー表示の階層を、
    // PPConditionAssetFactory がアセットの生成先フォルダを、それぞれこの属性から決める
    // 新しい条件クラスを追加するときは PPPartyConditionValidator の継承と合わせて必ず付ける
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public sealed class PPConditionMenuAttribute : Attribute
    {
        // ピッカーのツリー上での表示パス
        public string Path {get;}
        // 条件アセットの生成先フォルダパス
        public string FolderPath {get;}

        // aPath : ツリー上での表示パス
        // aFolderPath : アセットの生成先フォルダパス
        public PPConditionMenuAttribute(string aPath, string aFolderPath)
        {
            Path = aPath;
            FolderPath = aFolderPath;
        }
    }
}
