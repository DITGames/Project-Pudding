/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file MCPFieldOnlyContractResolver.cs
 * @author hqrse
 * @date 2026/08/20
 * @brief 専用コンバーターを持たない型をフィールドのみでシリアライズするContractResolver
 * Unityの型はnormalized/magnitude/linear/gamma/lossyScale等、呼ぶたびに新しい値を返す
 * 計算プロパティを多く持つ。これらは同一インスタンスへの循環参照ではないため
 * ReferenceLoopHandling.Ignoreでは止まらず、無限再帰でスタックオーバーフローに至る
 * (スタックオーバーフローはcatchできずEditorごと落ちる)。プロパティを一切辿らないことで
 * この種の再帰を構造的に排除する。フィールドのみを見る方針はUnity自身の
 * シリアライズモデルとも一致する
 * =====================================*/

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace MCPBridge.Editor.Tools.Serialization
{
    internal sealed class MCPFieldOnlyContractResolver : DefaultContractResolver
    {
        // 階層を自前で遡るためDeclaredOnlyを付け、各段で宣言されたフィールドのみを取る
        private const BindingFlags FieldFlags =
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly;

        protected override List<MemberInfo> GetSerializableMembers(Type aObjectType)
        {
            var members = new List<MemberInfo>();
            var seenNames = new HashSet<string>();

            // GetFieldsは基底クラスのprivateフィールドを返さないため、継承階層を遡って収集する
            // (DefaultContractResolverもGetChildPrivateFieldsで同等の補完を行っている)
            for (var type = aObjectType; type != null && type != typeof(object); type = type.BaseType)
            {
                foreach (var field in type.GetFields(FieldFlags))
                {
                    if (field.IsDefined(typeof(NonSerializedAttribute), false) ||
                        field.IsDefined(typeof(JsonIgnoreAttribute), true))
                    {
                        continue;
                    }

                    // 派生側で同名フィールドを隠蔽している場合は先に見た派生側を優先する
                    if (!seenNames.Add(field.Name))
                    {
                        continue;
                    }

                    members.Add(field);
                }
            }

            return members;
        }

        protected override JsonProperty CreateProperty(MemberInfo aMember, MemberSerialization aMemberSerialization)
        {
            var property = base.CreateProperty(aMember, aMemberSerialization);

            // DefaultContractResolverは、既定の検索フラグ(Public|Instance)にNonPublicが
            // 含まれないことを根拠にprivateメンバのReadable/Writableをfalseにする。
            // その状態ではシリアライザが黙って読み飛ばすため、[SerializeField] private を
            // 標準とする本プロジェクトではフィールドが1件も出力されなくなる。
            // フィールドのみを対象とする本Resolverでは明示的に許可し直す
            property.Readable = true;
            property.Writable = true;

            // 自動プロパティのバッキングフィールドは "<Name>k__BackingField" という名前を持つ。
            // 除外すると自動プロパティの値が出力から丸ごと欠落するため、フィールド自体は残しつつ
            // JSONのキー名だけを元のプロパティ名へ戻す。UnderlyingNameはフィールド名のままなので
            // 読み戻し時も同じフィールドへ書き込まれ、ラウンドトリップが成立する
            if (aMember.IsDefined(typeof(CompilerGeneratedAttribute), false))
            {
                property.PropertyName = ExtractBackingFieldPropertyName(property.PropertyName);
            }

            return property;
        }

        // "<Name>k__BackingField" から "Name" を取り出す。想定外の形式ならそのまま返す
        private static string ExtractBackingFieldPropertyName(string aName)
        {
            if (string.IsNullOrEmpty(aName) || aName[0] != '<')
            {
                return aName;
            }

            var close = aName.IndexOf('>');
            return close > 1 ? aName[1..close] : aName;
        }
    }
}
