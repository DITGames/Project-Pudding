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
    // 値を読み取って変化を購読できるパラメータの共通インターフェース
    // Parameter と ResourceParameter を、UI 側が同じ扱い方で表示できるようにするために切ってある
    public interface IReadableParameter
    {
        // 現在値
        public float CurrentValue { get; }
        // 値が変化したときに発火する
        event Action<IReadableParameter> OnValueChanged;
    }

    // 攻撃力・防御力といった、修飾子で増減する単一のパラメータ
    // 恒久的な基礎値と、バフ・デバフの修飾子リストを分けて持つのが要点
    // 現在値は修飾子から都度再計算されるため、バフを剥がせば必ず元の値に戻る
    // （現在値を直接いじる作りにしないことで、バフの掛け外しで値がずれるのを防いでいる）
    public class Parameter : IReadableParameter
    {
        // 恒久的な値。修飾子の影響を受けない素の値
        protected float mBaseValue;
        // 現在掛かっている修飾子の一覧
        protected readonly List<ParameterModifier> mModifiers = new();

        // 現在値が再計算されたときに発火する
        public event Action<IReadableParameter> OnValueChanged;

        // 基礎値。レベルアップなど恒久的な変化はこちらを書き換える
        // なお設定しただけでは再計算されないため、必要なら RecalculateCurrentValue を呼ぶ
        public float BaseValue
        {
            get => mBaseValue;
            set => mBaseValue = value;
        }

        // バフなどの修飾子で変更される現在値。実際の計算に使うのはこちら
        public float CurrentValue { get; protected set; }

        // 掛かっている修飾子の読み取り専用ビュー
        public IReadOnlyList<ParameterModifier> Modifiers => mModifiers;

        // aBaseValue : 初期の基礎値。現在値もこの値で初期化される
        public Parameter(float aBaseValue)
        {
            mBaseValue = aBaseValue;
            CurrentValue = aBaseValue;
        }

        // 修飾子を追加して現在値を再計算する。バフ・デバフが掛かったときに呼ぶ
        // aModifier : 追加する修飾子
        public void AddModifier(ParameterModifier aModifier)
        {
            mModifiers.Add(aModifier);
            RecalculateCurrentValue();
        }

        // 修飾子を除去して現在値を再計算する。バフ・デバフの効果時間が終わったときに呼ぶ
        // aModifier : 除去する修飾子。掛かっていなければ何もしない
        public void RemoveModifier(ParameterModifier aModifier)
        {
            if (mModifiers.Remove(aModifier))
            {
                RecalculateCurrentValue();
            }
        }

        // 指定した付与元から掛かっている修飾子をまとめて外す
        // 1 つのエフェクトが複数パラメータへ修飾子を撒いている場合の一括解除に使う
        // aSource : 付与元。参照比較で照合する
        public void RemoveModifiersFromSource(object aSource)
        {
            int removed = mModifiers.RemoveAll(m => ReferenceEquals(m.Source, aSource));
            if (removed > 0)
            {
                RecalculateCurrentValue();
            }
        }

        // 基礎値と全修飾子から現在値を計算し直し、変化を通知する
        // Override が 1 つでもあれば優先度最上位の値で確定し、無ければ加算 → 乗算の順に適用する
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

    // HP や MP など、実行中に消費・回復される値を表すパラメータ
    // 上限値そのものを Parameter として持つため、最大 HP にもバフを掛けられる
    // 上限が下がった場合は現在値がはみ出さないよう自動で切り詰める
    // 現在値は常に 0 ～ 上限に丸められる
    public class ResourceParameter : IReadableParameter
    {
        // 上限値。これ自体が修飾子を受け付ける Parameter
        public Parameter Max { get; }
        // 現在値。0 ～ Max の範囲に保たれる
        public float Current { get; protected set; }

        // 現在値（IReadableParameter 用）
        public float CurrentValue => Current;

        // 現在値または上限が変化したときに発火する
        public event Action<IReadableParameter> OnValueChanged;

        // 上限値を指定して生成する。現在値は満タンから始まる
        // 上限の変化を購読し、上限が下がった際に現在値を追従させる
        // aMax : 上限値の初期値
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

        // 現在値を減らす。0 未満にはならない
        // aAmount : 減少量。0 以下なら何もしない
        public void Damage(float aAmount)
        {
            if (aAmount <= 0f) return;
            SetCurrent(Current - aAmount);
        }

        // 現在値を増やす。上限を超えた分は切り捨てられる
        // aAmount : 回復量。0 以下なら何もしない
        public void Recover(float aAmount)
        {
            if (aAmount <= 0f) return;
            SetCurrent(Current + aAmount);
        }

        // 足りている場合のみ消費する。足りなければ現在値を変えずに失敗を返す
        // aAmount : 消費量
        // return : 消費できた場合 true
        public bool TryConsume(float aAmount)
        {
            if (Current < aAmount) return false;
            SetCurrent(Current - aAmount);
            return true;
        }

        // 現在値を 0 ～ 上限に丸めて設定し、変化を通知する
        // 増減系メソッドは全てここを経由するので、範囲外の値が入ることはない
        // aValue : 設定したい値
        public void SetCurrent(float aValue)
        {
            Current = Mathf.Clamp(aValue, 0f, Max.CurrentValue);
            OnValueChanged?.Invoke(this);
        }
    }
}
