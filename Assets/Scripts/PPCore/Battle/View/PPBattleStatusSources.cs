/* =====================================
 * Copyright WabisabiAndons. All rights reserved.
 * @file PPBattleStatusSources.cs
 * @author hqrse
 * @date 2026/06/25
 * @brief View へ表示データを供給するステータスソース（契約と実装）
 * =====================================*/

using System;
using UnityEngine;

namespace PPCore
{
    // UI がユニットの表示情報を読み取るためのインターフェース
    // UI は Changed を購読して再描画する
    public interface IPPUnitStatusSource
    {
        /* 状態 */

        // UI 表示名
        string DisplayName { get; }
        // 現在 HP
        float CurrentHP { get; }
        // 最大 HP
        float MaxHP { get; }
        // 次の行動までのコインゲージの溜まり具合（0〜1）。行動コスト分溜まると 1
        float CoinGaugeRate { get; }
        // スキルゲージの溜まり具合（0〜1）。上限まで溜まると 1
        float SkillGaugeRate { get; }
        // 選択中のスキルの表示名。未選択なら空文字
        string SelectedSkillName { get; }
        // 狙う相手に指定したユニットの表示名。未指定なら空文字
        string DesignatedTargetName { get; }

        /* イベント */

        // 表示内容が変化したときに発火する
        event Action Changed;
    }

    // UI がスキルの表示情報を読み取るためのインターフェース
    // スキル名とコストに加えて、発動可否（IsCastable）と残クールダウンを露出させる
    public interface IPPSkillStatusSource
    {
        /* 状態 */

        // UI 表示名
        string DisplayName { get; }
        // 発動に必要なスキルゲージ量
        float SkillGaugeCost { get; }
        // 今このスキルを発動できるか。ボタンの有効・無効に使う
        bool IsCastable { get; }
        // 残りクールタイム（秒）
        float CooldownRemaining { get; }

        /* イベント */

        // 表示内容が変化したときに発火する
        event Action Changed;
    }

    // BattleUnit を UI 向けの表示情報として見せるアダプタ。HP・各ゲージ・スキルの選択の変化を購読して Changed へ中継する
    // UI を破棄する際は必ず Dispose を呼ぶこと
    public class PPBattleUnitStatusSource : IPPUnitStatusSource, IDisposable
    {
        /* 状態 */

        // 表示対象のユニット
        private readonly BattleUnit mBattleUnit;
        // 購読解除済みかどうか。Dispose の多重呼び出しを無害にする
        private bool mIsDisposed;

        /* プロパティ */

        // UI 表示名
        public string DisplayName => mBattleUnit.DisplayName;
        // 現在 HP
        public float CurrentHP => mBattleUnit.Parameters.Hp.CurrentValue;
        // 最大 HP。バフで変動しうるため現在値を返す
        public float MaxHP => mBattleUnit.Parameters.Hp.Max.CurrentValue;

        // 次の行動までのコインゲージの溜まり具合（0〜1）。コインゲージを持たないユニットは 0
        public float CoinGaugeRate
        {
            get
            {
                if (mBattleUnit is not PPBattleUnit unit || unit.ActionCost <= 0f) return 0f;
                return Mathf.Clamp01(unit.ExtraParameters.CoinGauge.Current / unit.ActionCost);
            }
        }

        // スキルゲージの溜まり具合（0〜1）。スキルゲージを持たないユニットは 0
        public float SkillGaugeRate
        {
            get
            {
                if (mBattleUnit is not PPBattleUnit unit) return 0f;
                float max = unit.ExtraParameters.SkillGauge.Max.CurrentValue;
                return max > 0f ? Mathf.Clamp01(unit.ExtraParameters.SkillGauge.Current / max) : 0f;
            }
        }

        // 選択中のスキルの表示名。未選択なら空文字
        public string SelectedSkillName
            => (mBattleUnit as PPBattleUnit)?.SelectedSkill?.DisplayName ?? string.Empty;

        // 狙う相手に指定したユニットの表示名。未指定なら空文字
        public string DesignatedTargetName
            => (mBattleUnit as PPBattleUnit)?.DesignatedTarget?.DisplayName ?? string.Empty;

        /* イベント */

        // 表示内容が変化したときに発火する
        public event Action Changed;

        /* 生成 */

        // aBattleUnit : 表示対象のユニット
        public PPBattleUnitStatusSource(BattleUnit aBattleUnit)
        {
            mBattleUnit = aBattleUnit;
            // 最大HPもバフで変わるため両方購読する
            mBattleUnit.Parameters.Hp.OnValueChanged += HandleChanged;
            mBattleUnit.Parameters.Hp.Max.OnValueChanged += HandleChanged;
            // 行動コストもバフで変わるため、ゲージと併せて購読する
            if (mBattleUnit is PPBattleUnit unit)
            {
                unit.ExtraParameters.CoinGauge.OnValueChanged += HandleChanged;
                unit.ExtraParameters.AttackCost.OnValueChanged += HandleChanged;
                unit.ExtraParameters.SkillGauge.OnValueChanged += HandleChanged;
                unit.ExtraParameters.SkillGauge.Max.OnValueChanged += HandleChanged;
                unit.OnSkillSelectionChanged += HandleSkillSelectionChanged;
                unit.OnDesignatedTargetChanged += HandleSkillSelectionChanged;
            }
        }

        /* 初期化・接続 */

        // ユニットのパラメータへの購読を解除する。UI を破棄する際に呼ぶ
        // 二度呼ばれても安全
        public void Dispose()
        {
            if (mIsDisposed) return;
            mIsDisposed = true;

            mBattleUnit.Parameters.Hp.OnValueChanged -= HandleChanged;
            mBattleUnit.Parameters.Hp.Max.OnValueChanged -= HandleChanged;
            if (mBattleUnit is PPBattleUnit unit)
            {
                unit.ExtraParameters.CoinGauge.OnValueChanged -= HandleChanged;
                unit.ExtraParameters.AttackCost.OnValueChanged -= HandleChanged;
                unit.ExtraParameters.SkillGauge.OnValueChanged -= HandleChanged;
                unit.ExtraParameters.SkillGauge.Max.OnValueChanged -= HandleChanged;
                unit.OnSkillSelectionChanged -= HandleSkillSelectionChanged;
                unit.OnDesignatedTargetChanged -= HandleSkillSelectionChanged;
            }
        }

        /* イベントハンドラ */

        // パラメータの変化を自身のイベントとして中継する
        // 解除できるようラムダではなく名前付きメソッドにしてある
        private void HandleChanged(IReadableParameter _) => Changed?.Invoke();

        // スキルの選択と狙う相手の指定の変化を自身のイベントとして中継する
        private void HandleSkillSelectionChanged(PPBattleUnit _) => Changed?.Invoke();
    }

    // BattleSkill を UI 向けの表示情報として見せるアダプタ。発動可否は ICastValidator へ委ねる
    // 購読するのは保持ユニット自身のスキルゲージだけ
    public class PPBattleSkillStatusSource : IPPSkillStatusSource, IDisposable
    {
        /* 状態 */

        // 表示対象のスキル
        private readonly BattleSkill mSkill;
        // このスキルを持つユニット。発動可否の判定に使う
        private readonly BattleUnit mOwner;
        // 発動可否の判定に使うバトルコンテキスト
        private readonly BattleContext mContext;
        // このティックで既に予約した分を差し引くための台帳。未指定なら予約を考慮しない
        private readonly PPUnitActionLedger mLedger;
        // ゲージ変化を購読する対象のゲージ。保持ユニットがゲージを持たない場合は null
        private readonly ResourceParameter mSkillGauge;
        // 購読解除済みかどうか。Dispose の多重呼び出しを無害にする
        private bool mIsDisposed;

        /* プロパティ */

        // UI 表示名
        public string DisplayName => mSkill.DisplayName;
        // 発動に必要なスキルゲージ量。定義を引けない場合は無コスト扱い
        public float SkillGaugeCost => (mSkill.SourceDefinition as PPSkillDefinition)?.SkillGaugeCost ?? 0f;
        // 残りクールタイム（秒）
        public float CooldownRemaining => mSkill.RemainingCooldown;

        // 今このスキルを発動できるか
        // バリデータの判定に加えて、同じティックに既に予約した行動で使う予定の分も差し引く
        public bool IsCastable
        {
            get
            {
                if (!mContext.Rules.CastValidator.Validate(mOwner, mSkill, mContext).CanCast) return false;
                if (mLedger == null || mOwner is not PPBattleUnit ppOwner) return true;

                return mLedger.CanReserveSkill(ppOwner, SkillGaugeCost);
            }
        }

        /* イベント */

        // 表示内容が変化したときに発火する
        public event Action Changed;

        /* 生成 */

        public PPBattleSkillStatusSource(BattleSkill aSkill, BattleUnit aOwner, BattleContext aContext,
            PPUnitActionLedger aLedger = null)
        {
            mSkill = aSkill;
            mOwner = aOwner;
            mContext = aContext;
            mLedger = aLedger;

            mSkillGauge = (aOwner as PPBattleUnit)?.ExtraParameters.SkillGauge;
            if (mSkillGauge != null)
            {
                mSkillGauge.OnValueChanged += HandleChanged;
            }
        }

        /* 初期化・接続 */

        // メニュー破棄時に呼び出す(購読によるメモリリーク防止用)
        // 二度呼ばれても安全
        public void Dispose()
        {
            if (mIsDisposed) return;
            mIsDisposed = true;

            if (mSkillGauge != null)
            {
                mSkillGauge.OnValueChanged -= HandleChanged;
            }
        }

        /* イベントハンドラ */

        // ゲージの変化を自身のイベントとして中継する
        private void HandleChanged(IReadableParameter _) => Changed?.Invoke();
    }
}
