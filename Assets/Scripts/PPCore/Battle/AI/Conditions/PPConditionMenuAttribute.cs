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
    /// <summary>
    /// AI 条件クラスの、ピッカー UI 上での配置場所を指定する属性。
    /// <para>
    /// <see cref="PPConditionTreeView"/> がツリー表示の階層を、
    /// <see cref="PPConditionAssetFactory"/> がアセットの生成先フォルダを、それぞれこの属性から決める。
    /// 新しい条件クラスを追加するときは <see cref="PPPartyConditionValidator"/> の継承と合わせて必ず付ける。
    /// </para>
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public sealed class PPConditionMenuAttribute : Attribute
    {
        /// <summary>ピッカーのツリー上での表示パス。</summary>
        public string Path {get;}
        /// <summary>条件アセットの生成先フォルダパス。</summary>
        public string FolderPath {get;}

        /// <param name="aPath">ツリー上での表示パス。</param>
        /// <param name="aFolderPath">アセットの生成先フォルダパス。</param>
        public PPConditionMenuAttribute(string aPath, string aFolderPath)
        {
            Path = aPath;
            FolderPath = aFolderPath;
        }
    }
}
