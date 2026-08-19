/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file VFXParameterValueBase.cs
 * @author hqrse
 * @date 2026/08/18
 * @brief パラメータ型と型別の値フィールドを保持する共通基底
 * =====================================*/

using System;
using AttributeUtility;
using UnityEngine;

namespace VFXUtility
{
    // ノードのパラメータ(VFXSequenceNodeParameter)とオーバーライドセットのエントリ(VFXSequenceOverrideEntry)で
    // 型別の値フィールドを共有するための基底。非ポリモーフィックなSerializable継承のため、
    // 派生クラス側でもフィールドはフラットにシリアライズされる
    [Serializable]
    public abstract class VFXParameterValueBase
    {
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

        // パラメータ型に応じた値をobjectとして取得する(Event型は値を持たないためnull)
        public object GetValue()
        {
            return mParamType switch
            {
                VFXParameterType.Float => mFloatValue,
                VFXParameterType.Int => mIntValue,
                VFXParameterType.Bool => mBoolValue,
                VFXParameterType.Vector2 => mVector2Value,
                VFXParameterType.Vector3 => mVector3Value,
                VFXParameterType.Vector4 => mVector4Value,
                VFXParameterType.Color => mColorValue,
                VFXParameterType.Event => null,
                _ => null,
            };
        }

        // 別のパラメータ値から型と値をコピーする(オーバーライドセットへ既定値を流し込む際に使う)
        // aSource : コピー元
        public void CopyValueFrom(VFXParameterValueBase aSource)
        {
            mParamType = aSource.mParamType;
            mFloatValue = aSource.mFloatValue;
            mIntValue = aSource.mIntValue;
            mBoolValue = aSource.mBoolValue;
            mVector2Value = aSource.mVector2Value;
            mVector3Value = aSource.mVector3Value;
            mVector4Value = aSource.mVector4Value;
            mColorValue = aSource.mColorValue;
        }
    }
}
