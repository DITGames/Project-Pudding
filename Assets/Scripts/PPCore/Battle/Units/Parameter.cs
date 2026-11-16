/* =====================================
 * Copyright WabisabiAndons. All rights reserved.
 * @file Parameter.cs
 * @author hqrse
 * @date 2026/06/13
 * @brief パラメータと補正値
 * =====================================*/

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace PPCore
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
    // 基礎値と修飾子リストを分けて持ち、現在値は都度再計算するため、バフを剥がせば必ず元の値に戻る
    public class Parameter : IReadableParameter
    {
        /* 状態 */

        // バフなどの修飾子で変更される現在値。実際の計算に使うのはこちら
        public float CurrentValue { get; protected set; }
        // 恒久的な値。修飾子の影響を受けない素の値
        protected float mBaseValue;
        // 現在掛かっている修飾子の一覧
        protected readonly List<ParameterModifier> mModifiers = new();

        /* プロパティ */

        // 基礎値。レベルアップなど恒久的な変化はこちらを書き換える
        // 設定しただけでは再計算されない（まとめて行う場合は SetBaseValue を使う）
        public float BaseValue
        {
            get => mBaseValue;
            set => mBaseValue = value;
        }

        // 掛かっている修飾子の読み取り専用ビュー
        public IReadOnlyList<ParameterModifier> Modifiers => mModifiers;

        /* イベント */

        // 現在値が再計算されたときに発火する
        public event Action<IReadableParameter> OnValueChanged;

        /* 生成 */

        // aBaseValue : 初期の基礎値。現在値もこの値で初期化される
        public Parameter(float aBaseValue)
        {
            mBaseValue = aBaseValue;
            CurrentValue = aBaseValue;
        }

        /* 操作 */

        // 割合の合計値を倍率へ変換する（+20% と +30% の合計 0.5 → 1.5 倍）
        // 倍率は 0 未満にならないよう丸める
        public static float RatioSumToMultiplier(float aRatioSum) => Mathf.Max(0f, 1f + aRatioSum);

        // 基礎値を設定し、そのまま現在値を再計算して変化を通知する
        // BaseValue の setter は再計算しないため、設定と再計算を 1 回で済ませたい場合はこちらを使う
        public void SetBaseValue(float aValue)
        {
            mBaseValue = aValue;
            RecalculateCurrentValue();
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
        public void RemoveModifiersFromSource(object aSource)
        {
            int removed = mModifiers.RemoveAll(m => ReferenceEquals(m.Source, aSource));
            if (removed > 0)
            {
                RecalculateCurrentValue();
            }
        }

        // 基礎値と全修飾子から現在値を計算し直し、変化を通知する。Override があれば優先度最上位の値で確定し、
        // 無ければ (基礎値 + 加算の合計) × max(0, 1 + 割合の合計) × 乗算の総乗 で求める
        public void RecalculateCurrentValue()
        {
            // オーバーライドタイプを優先する
            var overrides = mModifiers.Where(m => m.Type == ParameterModifierType.Override).ToList();
            if (overrides.Count == 0)
            {
                // 加算の合計値
                float add = mModifiers.Where(m => m.Type == ParameterModifierType.Add).Sum(m => m.Value);
                // 割合加算。同種は合計してから倍率にするため、+40% と -10% は 1.3 倍になる
                float percent = RatioSumToMultiplier(
                    mModifiers.Where(m => m.Type == ParameterModifierType.Percent).Sum(m => m.Value));
                // 乗算
                float mul = mModifiers.Where(m => m.Type == ParameterModifierType.Multiply)
                    .Aggregate(1f, (acc, m) => acc * m.Value);
                CurrentValue = (mBaseValue + add) * percent * mul;
            }
            else
            {
                // 優先度が最も高いものを適用
                CurrentValue = overrides.OrderByDescending(m => m.Priority).First().Value;
            }

            OnValueChanged?.Invoke(this);
        }
    }

    // HP や MP など、実行中に消費・回復される値を表すパラメータ。現在値は常に 0 ～ 上限に丸められる
    // 上限値を Parameter として持つため、最大 HP にもバフを掛けられる
    public class ResourceParameter : IReadableParameter
    {
        /* 状態 */

        // 上限値。これ自体が修飾子を受け付ける Parameter
        public Parameter Max { get; }
        // 現在値。0 ～ Max の範囲に保たれる
        public float Current { get; protected set; }

        /* プロパティ */

        // 現在値（IReadableParameter 用）
        public float CurrentValue => Current;
        public float Ratio => Max.CurrentValue > 0 ? Current / Max.CurrentValue : 0f;

        /* イベント */

        // 現在値または上限が変化したときに発火する
        public event Action<IReadableParameter> OnValueChanged;

        /* 生成 */

        // 上限値を指定して生成する。現在値は満タンから始まる
        // 上限の変化を購読し、上限が下がった際に現在値を追従させる
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

        /* 操作 */

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
        public bool TryConsume(float aAmount)
        {
            if (Current < aAmount) return false;
            SetCurrent(Current - aAmount);
            return true;
        }

        // 現在値を 0 ～ 上限に丸めて設定し、変化を通知する
        // 増減系メソッドは全てここを経由するので、範囲外の値が入ることはない
        public void SetCurrent(float aValue)
        {
            Current = Mathf.Clamp(aValue, 0f, Max.CurrentValue);
            OnValueChanged?.Invoke(this);
        }
    }

    // パラメータ修飾子の適用方式。Override が最優先で、無い場合は Add → Percent → Multiply の順に適用する
    // アセットへ整数値で保存されるため、要素の追加は必ず末尾に行うこと
    public enum ParameterModifierType
    {
        // 基礎値に加算する。同種は合計される
        [InspectorName("加算")]
        Add,
        // 加算後の値に乗算する。同種は掛け合わされる
        [InspectorName("乗算")]
        Multiply,
        // 他を無視して値を上書きする。複数あれば優先度が最も高いものが勝つ
        [InspectorName("上書き")]
        Override,
        // 割合で増減させる（+20% は 0.2、-10% は -0.1）。同種は合計してから 1 を足した倍率として掛ける
        // 倍率は 0 未満にならないよう丸めるため、減少が 100% を超えても値が負になることはない
        [InspectorName("割合加算")]
        Percent,
    }

    // パラメータへ掛かる 1 件分の修飾子（バフ・デバフ・パッシブ補正など）。生成後は変更されない
    // 付与元を Source に持ち、Parameter.RemoveModifiersFromSource でまとめて剥がせる
    public sealed class ParameterModifier
    {
        /* 状態 */

        // 適用方式（加算・乗算・上書き・割合加算）
        public ParameterModifierType Type { get; }
        // 修飾値。方式によって加算値・倍率・上書き値・割合（0.2 = +20%）のいずれかとして扱われる
        public float Value { get; }
        // この修飾子の付与元。解除時の照合に使うため参照比較できるものを渡す
        public object Source { get; }
        // Override 同士が競合したときの優先度。高い方が採用される
        public int Priority { get; }

        /* 生成 */

        public ParameterModifier(ParameterModifierType aType, object aSource, float aValue, int aPriority = 0)
        {
            Type = aType;
            Source = aSource;
            Value = aValue;
            Priority = aPriority;
        }
    }
}
