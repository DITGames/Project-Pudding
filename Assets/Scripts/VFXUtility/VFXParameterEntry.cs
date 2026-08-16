/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file VFXParameterEntry.cs
 * @author hqrse
 * @date 2026/08/17
 * @brief VFXParameterComponentが管理する1件のVFXパラメータ設定情報
 * =====================================*/

using System;
using CommandBattleCore;
using UnityEngine;

namespace VFXUtility
{
    [Serializable]
    public class VFXParameterEntry
    {
        [Label("対象VFX ID")]
        [SerializeField] private string mVfxId;

        [Label("パラメータ名")]
        [SerializeField] private string mParamName;

        [Label("パラメータ型")]
        [SerializeField] private VFXParameterType mParamType;

        [Label("Float値")]
        [EditCondition(nameof(IsFloatType), true)]
        [SerializeField] private float mFloatValue;

        [Label("Int値")]
        [EditCondition(nameof(IsIntType), true)]
        [SerializeField] private int mIntValue;

        [Label("Bool値")]
        [EditCondition(nameof(IsBoolType), true)]
        [SerializeField] private bool mBoolValue;

        [Label("Vector2値")]
        [EditCondition(nameof(IsVector2Type), true)]
        [SerializeField] private Vector2 mVector2Value;

        [Label("Vector3値")]
        [EditCondition(nameof(IsVector3Type), true)]
        [SerializeField] private Vector3 mVector3Value;

        [Label("Vector4値")]
        [EditCondition(nameof(IsVector4Type), true)]
        [SerializeField] private Vector4 mVector4Value;

        [Label("Color値")]
        [EditCondition(nameof(IsColorType), true)]
        [SerializeField] private Color mColorValue;

        public string VfxId => mVfxId;
        public string ParamName => mParamName;
        public VFXParameterType ParamType => mParamType;

        public float FloatValue => mFloatValue;
        public int IntValue => mIntValue;
        public bool BoolValue => mBoolValue;
        public Vector2 Vector2Value => mVector2Value;
        public Vector3 Vector3Value => mVector3Value;
        public Vector4 Vector4Value => mVector4Value;
        public Color ColorValue => mColorValue;

        // EditConditionAttribute(hides)の条件対象。パラメータ型に応じて対応する値フィールドのみ表示する
        private bool IsFloatType => mParamType == VFXParameterType.Float;
        private bool IsIntType => mParamType == VFXParameterType.Int;
        private bool IsBoolType => mParamType == VFXParameterType.Bool;
        private bool IsVector2Type => mParamType == VFXParameterType.Vector2;
        private bool IsVector3Type => mParamType == VFXParameterType.Vector3;
        private bool IsVector4Type => mParamType == VFXParameterType.Vector4;
        private bool IsColorType => mParamType == VFXParameterType.Color;
    }
}
