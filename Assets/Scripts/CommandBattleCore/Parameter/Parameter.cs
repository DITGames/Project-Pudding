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
using UnityEngine;

namespace CommandBattleCore
{
    /// <summary>
    /// 値を読み取って変化を購読できるパラメータの共通インターフェース。
    /// <see cref="Parameter"/> と <see cref="ResourceParameter"/> を、
    /// UI 側が同じ扱い方で表示できるようにするために切ってある。
    /// </summary>
    public interface IReadableParameter
    {
        /// <summary>現在値。</summary>
        public float CurrentValue { get; }
        /// <summary>値が変化したときに発火する。</summary>
        event Action<IReadableParameter> OnValueChanged;
    }

    /// <summary>
    /// 攻撃力・防御力といった、修飾子で増減する単一のパラメータ。
    /// <para>
    /// 恒久的な基礎値と、バフ・デバフの修飾子リストを分けて持つのが要点。
    /// 現在値は修飾子から都度再計算されるため、バフを剥がせば必ず元の値に戻る
    /// （現在値を直接いじる作りにしないことで、バフの掛け外しで値がずれるのを防いでいる）。
    /// </para>
    /// </summary>
    public class Parameter : IReadableParameter
    {
        /// <summary>恒久的な値。修飾子の影響を受けない素の値。</summary>
        protected float mBaseValue;
        /// <summary>現在掛かっている修飾子の一覧。</summary>
        protected readonly List<ParameterModifier> mModifiers = new();

        /// <summary>現在値が再計算されたときに発火する。</summary>
        public event Action<IReadableParameter> OnValueChanged;

        /// <summary>
        /// 基礎値。レベルアップなど恒久的な変化はこちらを書き換える。
        /// なお設定しただけでは再計算されないため、必要なら <see cref="RecalculateCurrentValue"/> を呼ぶ。
        /// </summary>
        public float BaseValue
        {
            get => mBaseValue;
            set => mBaseValue = value;
        }

        /// <summary>バフなどの修飾子で変更される現在値。実際の計算に使うのはこちら。</summary>
        public float CurrentValue { get; protected set; }

        /// <summary>掛かっている修飾子の読み取り専用ビュー。</summary>
        public IReadOnlyList<ParameterModifier> Modifiers => mModifiers;

        /// <param name="aBaseValue">初期の基礎値。現在値もこの値で初期化される。</param>
        public Parameter(float aBaseValue)
        {
            mBaseValue = aBaseValue;
            CurrentValue = aBaseValue;
        }

        /// <summary>
        /// 修飾子を追加して現在値を再計算する。バフ・デバフが掛かったときに呼ぶ。
        /// </summary>
        /// <param name="aModifier">追加する修飾子。</param>
        public void AddModifier(ParameterModifier aModifier)
        {
            mModifiers.Add(aModifier);
            RecalculateCurrentValue();
        }

        /// <summary>
        /// 修飾子を除去して現在値を再計算する。バフ・デバフの効果時間が終わったときに呼ぶ。
        /// </summary>
        /// <param name="aModifier">除去する修飾子。掛かっていなければ何もしない。</param>
        public void RemoveModifier(ParameterModifier aModifier)
        {
            if (mModifiers.Remove(aModifier))
            {
                RecalculateCurrentValue();
            }
        }

        /// <summary>
        /// 指定した付与元から掛かっている修飾子をまとめて外す。
        /// 1 つのエフェクトが複数パラメータへ修飾子を撒いている場合の一括解除に使う。
        /// </summary>
        /// <param name="aSource">付与元。参照比較で照合する。</param>
        public void RemoveModifiersFromSource(object aSource)
        {
            int removed = mModifiers.RemoveAll(m => ReferenceEquals(m.Source, aSource));
            if (removed > 0)
            {
                RecalculateCurrentValue();
            }
        }

        /// <summary>
        /// 基礎値と全修飾子から現在値を計算し直し、変化を通知する。
        /// Override が 1 つでもあれば優先度最上位の値で確定し、無ければ加算 → 乗算の順に適用する。
        /// </summary>
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
                CurrentValue = (mBaseValue + add) * mul;
            }
            else
            {
                // 優先度が最も高いものを適用
                CurrentValue = overrides.OrderByDescending(m => m.Priority).First().Value;
            }

            OnValueChanged?.Invoke(this);
        }
    }

    /// <summary>
    /// HP や MP など、実行中に消費・回復される値を表すパラメータ。
    /// <para>
    /// 上限値そのものを <see cref="Parameter"/> として持つため、最大 HP にもバフを掛けられる。
    /// 上限が下がった場合は現在値がはみ出さないよう自動で切り詰める。
    /// 現在値は常に 0 ～ 上限に丸められる。
    /// </para>
    /// </summary>
    public class ResourceParameter : IReadableParameter
    {
        /// <summary>上限値。これ自体が修飾子を受け付ける <see cref="Parameter"/>。</summary>
        public Parameter Max { get; }
        /// <summary>現在値。0 ～ <see cref="Max"/> の範囲に保たれる。</summary>
        public float Current { get; protected set; }

        /// <summary>現在値（<see cref="IReadableParameter"/> 用）。</summary>
        public float CurrentValue => Current;

        /// <summary>現在値または上限が変化したときに発火する。</summary>
        public event Action<IReadableParameter> OnValueChanged;

        /// <summary>
        /// 上限値を指定して生成する。現在値は満タンから始まる。
        /// 上限の変化を購読し、上限が下がった際に現在値を追従させる。
        /// </summary>
        /// <param name="aMax">上限値の初期値。</param>
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

        /// <summary>現在値を減らす。0 未満にはならない。</summary>
        /// <param name="aAmount">減少量。0 以下なら何もしない。</param>
        public void Damage(float aAmount)
        {
            if (aAmount <= 0f) return;
            SetCurrent(Current - aAmount);
        }

        /// <summary>現在値を増やす。上限を超えた分は切り捨てられる。</summary>
        /// <param name="aAmount">回復量。0 以下なら何もしない。</param>
        public void Recover(float aAmount)
        {
            if (aAmount <= 0f) return;
            SetCurrent(Current + aAmount);
        }

        /// <summary>
        /// 足りている場合のみ消費する。足りなければ現在値を変えずに失敗を返す。
        /// </summary>
        /// <param name="aAmount">消費量。</param>
        /// <returns>消費できた場合 true。</returns>
        public bool TryConsume(float aAmount)
        {
            if (Current < aAmount) return false;
            SetCurrent(Current - aAmount);
            return true;
        }

        /// <summary>
        /// 現在値を 0 ～ 上限に丸めて設定し、変化を通知する。
        /// 増減系メソッドは全てここを経由するので、範囲外の値が入ることはない。
        /// </summary>
        /// <param name="aValue">設定したい値。</param>
        public void SetCurrent(float aValue)
        {
            Current = Mathf.Clamp(aValue, 0f, Max.CurrentValue);
            OnValueChanged?.Invoke(this);
        }
    }
}
