/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file PPTypeMenuNameAttribute.cs
 * @author hqrse
 * @date 2026/08/06
 * @brief 型選択メニューに表示する日本語名を付与する属性
 * =====================================*/

using System;

namespace PPCore
{
    // [SerializeReference] な型を選択するエディタ用ツリーに表示する位置を型へ付与する属性
    // SkillEffect・StatusEffect のどちらの派生型にも共通して使う
    // ランタイム側の型に付与するため、この属性自体は Editor アセンブリに置かない
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public sealed class PPTypeMenuNameAttribute : Attribute
    {
        // "/" 区切りのツリー表示パス
        public string Path { get; }

        // aPath : ツリー上の表示パス（"/" で階層を表す。区切りが無ければ単なる葉の名前になる）
        public PPTypeMenuNameAttribute(string aPath)
        {
            Path = aPath;
        }
    }
}
