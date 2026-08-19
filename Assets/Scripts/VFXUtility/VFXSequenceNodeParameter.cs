/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file VFXSequenceNodeParameter.cs
 * @author hqrse
 * @date 2026/08/18
 * @brief ノードに直接埋め込む、VFXパラメータ1件分の設定
 * =====================================*/

using System;
using AttributeUtility;
using UnityEngine;

namespace VFXUtility
{
    [Serializable]
    public class VFXSequenceNodeParameter : VFXParameterValueBase
    {
        [Label("パラメータ名")]
        [SerializeField] private string mParamName;

        [Label("公開名(空なら外部から上書き不可)")]
        [SerializeField] private string mExposedName;

        public string ParamName => mParamName;

        // 外部からの上書き・オーバーライドセットで参照するための名前。空の場合は上書き対象にならない
        public string ExposedName => mExposedName;
    }
}
