/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file Parameter.cs
 * @author hqrse
 * @date 2026/06/13
 * @brief 単一パラメータ定義
 * =====================================*/
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace CommandBattleCore
{
    public interface IReadableParameter
    {
        public float CurrentValue { get; }
        event Action<IReadableParameter> OnValueChanged;
    }

    public class Parameter : IReadableParameter
    {
        // 恒久的な値
        protected float mBaseValue;
        protected readonly List<ParameterModifier> mModifiers = new();

        public event Action<IReadableParameter> OnValueChanged;

        public float BaseValue
        {
            get => mBaseValue;
            set => mBaseValue = value;
        }

        // バフなどの修飾子で変更される現在値
        public float CurrentValue { get; protected set; }

        public IReadOnlyList<ParameterModifier> Modifiers => mModifiers;

        public Parameter(float aBaseValue)
        {
            mBaseValue = aBaseValue;
            CurrentValue = aBaseValue;
        }

        // バフ・デバフが追加されたときに処理する
        public void AddModifier(ParameterModifier aModifier)
        {
            mModifiers.Add(aModifier);
            RecalculateCurrentValue();
        }

        // バフ・デバフの効果時間が終わった際に処理する
        public void RemoveModifier(ParameterModifier aModifier)
        {
            if (mModifiers.Remove(aModifier))
            {
                RecalculateCurrentValue();
            }
        }

        // 指定SourceのModifierを外す
        // パッシブによるステータス補正の除去処理など
        public void RemoveModifiersFromSource(object aSource)
        {
            int removed = mModifiers.RemoveAll(m => ReferenceEquals(m.Source, aSource));
            if (removed > 0)
            {
                RecalculateCurrentValue();
            }
        }

        public void RecalculateCurrentValue()
        {
            // オーバーライドタイプを優先する
            var overrides = mModifiers.Where(m => m.Type == ParameterModifierType.Override).ToList();
            if (overrides.Count == 0)
            {
                // 加算の合計値
                float add = mModifiers.Where(m => m.Type == ParameterModifierType.Add).Sum(m => m.Value);
                // 乗算
                float mul = mModifiers.Where(m => m.Type == ParameterModifierType.Multiply)
                    .Aggregate(1f, (acc, m) => acc * m.Value);
                CurrentValue = (mBaseValue + add) + mul;
            }
            else
            {
                // 優先度が最も高いものを適用
                CurrentValue = overrides.OrderByDescending(m => m.Priority).First().Value;
            }

            OnValueChanged?.Invoke(this);
        }
    }

    // HPやMPなど実行中に消費されるものをこちらで定義する
    public class ResourceParameter : IReadableParameter
    {
        public Parameter Max { get; }
        public float Current { get; protected set; }

        public float CurrentValue => Current;

        public event Action<IReadableParameter> OnValueChanged;

        public ResourceParameter(float aMax)
        {
            Max = new Parameter(aMax);
            Current = aMax;

            Max.OnValueChanged += _ =>
            {
                if (Current > Max.CurrentValue) Current = Max.CurrentValue;
                OnValueChanged?.Invoke(this);
            };
        }

        public void Damage(float aAmount)
        {
            if (aAmount <= 0f) return;
            SetCurrent(Current - aAmount);
        }

        public void Recover(float aAmount)
        {
            if (aAmount <= 0f) return;
            SetCurrent(Current + aAmount);
        }

        public bool TryConsume(float aAmount)
        {
            if (Current < aAmount) return false;
            SetCurrent(Current - aAmount);
            return true;
        }

        public void SetCurrent(float aValue)
        {
            Current = Mathf.Clamp(aValue, 0f, Max.CurrentValue);
            OnValueChanged?.Invoke(this);
        }
    }
}