/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file PPGaugeUtility.cs
 * @author hqrse
 * @date 2026/08/24
 * @brief ユニットゲージの支払い判定ヘルパー
 * =====================================*/

using System.Collections.Generic;
using CommandBattleCore;
using CustomConsole;
using UnityEngine;

namespace PPCore
{
    // ユニット単位ゲージの残量判定・支払い・分配をまとめたヘルパー
    // 「足りているか」の比較誤差の扱いを 1 箇所へ寄せ、
    // バリデータ・コマンド・AI が同じ基準で判定できるようにする
    public static class PPGaugeUtility
    {
        // 残量比較に使う許容誤差
        // 加算・減算を繰り返した結果 1 の位で誤差が出ても「ちょうど足りている」を不足扱いにしないための値
        public const float CompareEpsilon = 0.0001f;

        // ゲージが指定量を支払えるかを、実際には消費せずに判定する
        // aGauge : 判定するゲージ。null なら支払えないものとして扱う
        // aAmount : 必要量。0 以下なら常に支払える
        // return : 支払える場合 true
        public static bool CanPay(ResourceParameter aGauge, float aAmount)
        {
            if (aAmount <= 0f) return true;
            return aGauge != null && aGauge.Current + CompareEpsilon >= aAmount;
        }

        // ゲージから指定量を消費する。足りない場合は何も消費せず失敗を返す
        // 比較の基準を CanPay と揃えるため、ResourceParameter.TryConsume を直接呼ばずここを通す
        // aGauge : 消費するゲージ
        // aAmount : 消費量。0 以下なら何もせず成功
        // return : 消費できた場合 true
        public static bool TryPay(ResourceParameter aGauge, float aAmount)
        {
            if (aAmount <= 0f) return true;
            if (!CanPay(aGauge, aAmount)) return false;

            aGauge.Damage(aAmount);
            return true;
        }

        // 指定量を生存ユニット全員のゲージへ均等分配する
        // 撃破済みのユニットは分配対象に含めない
        // 分配先が居ない場合・加算量が 0 以下の場合は何もしない（0 加算の通知やログを出さないため）
        // aUnits : 分配先の候補ユニット
        // aKind : 加算するゲージの種別
        // aTotalAmount : 分配する総量
        // return : 実際に分配されたユニット数。分配しなかった場合は 0
        public static int DistributeToAliveUnits(IEnumerable<BattleUnit> aUnits, PPGaugeKind aKind, float aTotalAmount)
        {
            if (aUnits == null || aTotalAmount <= 0f) return 0;

            var targets = new List<PPBattleUnit>();
            foreach (var unit in aUnits)
            {
                if (unit is PPBattleUnit ppUnit && ppUnit.IsAlive)
                {
                    targets.Add(ppUnit);
                }
            }
            if (targets.Count == 0) return 0;

            float share = aTotalAmount / targets.Count;
            foreach (var unit in targets)
            {
                unit.ExtraParameters.Gauge(aKind).Recover(share);
            }

            CustomConsoleLog.Verbose("Resource",
                $"{ToDisplayString(aKind)}を{aTotalAmount:0.##}、生存{targets.Count}体へ均等分配しました（1体あたり{share:0.##}）。");
            return targets.Count;
        }

        // ゲージ種別を日本語表記へ変換する
        // aKind : 変換するゲージ種別
        // return : 日本語の表記
        public static string ToDisplayString(PPGaugeKind aKind)
            => aKind == PPGaugeKind.Skill ? "スキルゲージ" : "コインゲージ";

        // 不足量を 1 ティックあたりの増加量で割り、埋まるまでのティック数を見積もる
        // 増加が見込めない場合は無限大を返し、呼び出し側で「待っても撃てない」と判断させる
        // aShortfall : 不足量
        // aGainPerTick : 1 ティックあたりの増加量
        // return : 見積もりティック数
        public static float EstimateWaitTicks(float aShortfall, float aGainPerTick)
        {
            if (aShortfall <= 0f) return 0f;
            return aGainPerTick > 0f ? aShortfall / aGainPerTick : Mathf.Infinity;
        }
    }
}
