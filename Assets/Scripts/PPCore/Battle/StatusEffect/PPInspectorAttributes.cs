/* =====================================
 * Copyright WabisabiAndons. All rights reserved.
 * @file PPInspectorAttributes.cs
 * @author hqrse
 * @date 2026/09/04
 * @brief インスペクタ用の属性（フィールドラベル・型選択メニュー名）
 * =====================================*/

using System;

namespace PPCore
{
    // [SerializeReference] のフィールドへ日本語の表示名を与えるための属性
    // [Label] は型側の PropertyDrawer より優先され型選択ができなくなるため、描画へ介入しない素の Attribute にしている
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false, Inherited = false)]
    public sealed class PPFieldLabelAttribute : Attribute
    {
        // インスペクタに表示する文字列
        public string Text { get; }

        // aText : 表示名
        public PPFieldLabelAttribute(string aText)
        {
            Text = aText;
        }
    }

    // [SerializeReference] な型を選択するエディタ用ツリーでの表示位置を、型へ付与する属性
    // ランタイム側の型に付与するため Editor アセンブリには置かない
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
