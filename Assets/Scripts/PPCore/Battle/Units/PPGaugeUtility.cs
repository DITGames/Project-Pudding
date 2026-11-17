/* =====================================
 * Copyright WabisabiAndons. All rights reserved.
 * @file PPGaugeUtility.cs
 * @author hqrse
 * @date 2026/08/24
 * @brief ユニットゲージの支払い判定ヘルパー
 * =====================================*/

using System.Collections.Generic;
using CustomConsole;
using UnityEngine;

namespace PPCore
{
    // ユニット単位ゲージの残量判定・支払い・分配をまとめたヘルパー
    // バリデータ・コマンド・AI が同じ基準で判定できるよう、比較誤差の扱いをここへ寄せる
    public static class PPGaugeUtility
    {
        /* 定数 */

        // 残量比較に使う許容誤差
        // 加算・減算を繰り返した結果 1 の位で誤差が出ても「ちょうど足りている」を不足扱いにしないための値
        public const float CompareEpsilon = 0.0001f;

        /* 操作 */

        // ゲージから指定量を消費する。足りない場合は何も消費せず失敗を返す
        // 比較の基準を CanPay と揃えるため、ResourceParameter.TryConsume を直接呼ばずここを通す
        public static bool TryPay(ResourceParameter aGauge, float aAmount)
        {
            if (aAmount <= 0f) return true;
            if (!CanPay(aGauge, aAmount)) return false;

            aGauge.Damage(aAmount);
            return true;
        }

        // 指定量を生存ユニット全員のゲージへ均等分配する
        // 分配先が居ない場合・加算量が 0 以下の場合は何もしない
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

        /* 問い合わせ */

        // ゲージが指定量を支払えるかを、実際には消費せずに判定する
        public static bool CanPay(ResourceParameter aGauge, float aAmount)
        {
            if (aAmount <= 0f) return true;
            return aGauge != null && aGauge.Current + CompareEpsilon >= aAmount;
        }

        /* 説明文 */

        // ゲージ種別を日本語表記へ変換する
        public static string ToDisplayString(PPGaugeKind aKind)
            => aKind == PPGaugeKind.Skill ? "スキルゲージ" : "コインゲージ";
    }
}
