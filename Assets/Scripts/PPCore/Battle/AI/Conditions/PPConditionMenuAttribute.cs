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
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public sealed class PPConditionMenuAttribute : Attribute
    {
        public string Path {get;}
        public string FolderPath {get;}

        public PPConditionMenuAttribute(string aPath, string aFolderPath)
        {
            Path = aPath;
            FolderPath = aFolderPath;
        }
    }
}