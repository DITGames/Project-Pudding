/* =====================================
 * Copyright WabisabiAndons. All rights reserved.
 * @file PPBattleStatusSources.cs
 * @author hqrse
 * @date 2026/06/25
 * @brief View へ表示データを供給するステータスソース（契約と実装）
 * =====================================*/

using System;

namespace PPCore
{
    // UI がユニットの表示情報を読み取るためのインターフェース
    // View 側に BattleUnit を直接触らせず、
    // 表示に必要な項目だけを露出させるための境界
    // UI は Changed を購読して再描画すればよく、バトル側のどのイベントで値が変わるかを知る必要がない
    public interface IPPUnitStatusSource
    {
        // UI 表示名
        string DisplayName { get; }
        // 現在 HP
        float CurrentHP { get; }
        // 最大 HP
        float MaxHP { get; }

        // 表示内容が変化したときに発火する
        event Action Changed;
    }

    // UI がスキルの表示情報を読み取るためのインターフェース
    // スキル名とコストに加えて、ボタンを押せるかどうか（IsCastable）と
    // 残クールダウンを露出させ、メニュー側が発動可否を自前で判定せずに済むようにする
    public interface IPPSkillStatusSource
    {
        // UI 表示名
        string DisplayName { get; }
        // 発動に必要なスキルゲージ量
        float SkillGaugeCost { get; }
        // 今このスキルを発動できるか。ボタンの有効・無効に使う
        bool IsCastable { get; }
        // 残りクールダウンターン数
        int CooldownRemaining { get; }
        // 表示内容が変化したときに発火する
        event Action Changed;
    }

    // BattleUnit を UI 向けの表示情報として見せるアダプタ
    // 値を保持せず参照のたびに読み直し、HP と最大 HP の変化を購読して Changed へ中継する
    // ユニットのパラメータを購読するため、UI を破棄する際は必ず Dispose を呼ぶこと
    // 呼ばないとユニットが生きている限りこのアダプタが解放されない
    public class PPBattleUnitStatusSource : IPPUnitStatusSource, IDisposable
    {
        // 表示対象のユニット
        private readonly BattleUnit mBattleUnit;
        // 購読解除済みかどうか。Dispose の多重呼び出しを無害にする
        private bool mIsDisposed;
        // 表示内容が変化したときに発火する
        public event Action Changed;

        // UI 表示名
        public string DisplayName => mBattleUnit.DisplayName;
        // 現在 HP
        public float CurrentHP => mBattleUnit.Parameters.Hp.CurrentValue;
        // 最大 HP。バフで変動しうるため現在値を返す
        public float MaxHP => mBattleUnit.Parameters.Hp.Max.CurrentValue;

        // aBattleUnit : 表示対象のユニット
        public PPBattleUnitStatusSource(BattleUnit aBattleUnit)
        {
            mBattleUnit = aBattleUnit;
            // 最大HPもバフで変わるため両方購読する
            mBattleUnit.Parameters.Hp.OnValueChanged += HandleChanged;
            mBattleUnit.Parameters.Hp.Max.OnValueChanged += HandleChanged;
        }

        // パラメータの変化を自身のイベントとして中継する
        // 解除できるようラムダではなく名前付きメソッドにしてある
        private void HandleChanged(IReadableParameter _) => Changed?.Invoke();

        // ユニットのパラメータへの購読を解除する。UI を破棄する際に呼ぶ
        // 二度呼ばれても安全
        public void Dispose()
        {
            if (mIsDisposed) return;
            mIsDisposed = true;

            mBattleUnit.Parameters.Hp.OnValueChanged -= HandleChanged;
            mBattleUnit.Parameters.Hp.Max.OnValueChanged -= HandleChanged;
        }
    }

    // BattleSkill を UI 向けの表示情報として見せるアダプタ
    // 発動可否は自前で判定せず ICastValidator へ委ねるため、
    // UI の表示とコマンド実行時の判定が食い違わない
    // 購読するのは保持ユニット自身のスキルゲージだけ
    // 他ユニットのゲージ増減で UI が再描画されるのを避けている
    public class PPBattleSkillStatusSource : IPPSkillStatusSource, IDisposable
    {
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
        // 表示内容が変化したときに発火する
        public event Action Changed;

        // UI 表示名
        public string DisplayName => mSkill.DisplayName;
        // 発動に必要なスキルゲージ量。定義を引けない場合は無コスト扱い
        public float SkillGaugeCost => (mSkill.SourceDefinition as PPSkillDefinition)?.SkillGaugeCost ?? 0f;
        // 残りクールダウンターン数
        public int CooldownRemaining => mSkill.RemainingCooldown;
        // 今このスキルを発動できるか
        // クールダウン・使用回数・ゲージ残量の判定はバリデータへ委譲し、
        // そのうえで「同じティックに既に予約した行動」で使う予定の分も差し引いて判定する
        // 2 回目以降の予約で、払えないスキルがボタンとして押せてしまうのを防ぐ
        public bool IsCastable
        {
            get
            {
                if (!mContext.Rules.CastValidator.Validate(mOwner, mSkill, mContext).CanCast) return false;
                if (mLedger == null || mOwner is not PPBattleUnit ppOwner) return true;

                return mLedger.CanReserveSkill(ppOwner, SkillGaugeCost);
            }
        }

        // aSkill : 表示対象のスキル
        // aOwner : このスキルを持つユニット
        // aContext : バトルコンテキスト
        // aLedger : このティックの予約を仮押さえしている台帳。未指定なら予約を考慮しない
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

        // ゲージの変化を自身のイベントとして中継する
        private void HandleChanged(IReadableParameter _) => Changed?.Invoke();

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

    }
}
