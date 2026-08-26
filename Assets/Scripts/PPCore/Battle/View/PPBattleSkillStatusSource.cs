/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file PPBattleSkillStatusSource.cs
 * @author hqrse
 * @date 2026/06/30
 * @brief バトルスキル情報アダプタ
 * =====================================*/

using System;
using CommandBattleCore;

namespace PPCore
{
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
