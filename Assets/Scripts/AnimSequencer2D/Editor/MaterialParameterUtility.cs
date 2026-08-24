/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file MaterialParameterUtility.cs
 * @author hqrse
 * @date 2026/08/23
 * @brief Materialのシェーダが公開するFloat/Color/Vectorプロパティを列挙するエディタ専用ユーティリティ
 * =====================================*/

using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace AnimSequencer2D.Editor
{
    internal static class MaterialParameterUtility
    {
        // aMaterial : 列挙対象。nullの場合は空リストを返す
        // 戻り値 : アニメーション対象にできるプロパティ名と型のペア一覧(Hidden/PerRendererDataは除外)
        public static List<(string Name, MaterialParameterType Type)> EnumerateAnimatableProperties(Material aMaterial)
        {
            var result = new List<(string, MaterialParameterType)>();
            if (aMaterial == null)
            {
                return result;
            }

            MaterialProperty[] properties = MaterialEditor.GetMaterialProperties(new Object[] { aMaterial });
            foreach (MaterialProperty property in properties)
            {
                if ((property.propertyFlags & (UnityEngine.Rendering.ShaderPropertyFlags.HideInInspector | UnityEngine.Rendering.ShaderPropertyFlags.PerRendererData)) != 0)
                {
                    continue;
                }

                switch (property.propertyType)
                {
                    case UnityEngine.Rendering.ShaderPropertyType.Float:
                    case UnityEngine.Rendering.ShaderPropertyType.Range:
                        result.Add((property.name, MaterialParameterType.Float));
                        break;
                    case UnityEngine.Rendering.ShaderPropertyType.Color:
                        result.Add((property.name, MaterialParameterType.Color));
                        break;
                    case UnityEngine.Rendering.ShaderPropertyType.Vector:
                        result.Add((property.name, MaterialParameterType.Vector4));
                        break;
                }
            }
            return result;
        }
    }
}
