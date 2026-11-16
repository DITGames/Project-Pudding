/* =====================================
 * Copyright WabisabiAndons. All rights reserved.
 * @file PPCoinResourceBridge.cs
 * @author hqrse
 * @date 2026/06/21
 * @brief コイン獲得をバトルリソースへ変換する橋渡し（通知元・変換器・ブリッジ）
 * =====================================*/

using System;
using UnityEngine;
using AttributeUtility;
using CustomConsole;

namespace PPCore
{
    // プッシャー（物理）側がコイン獲得を知らせるためのインターフェース
    // 実装例は CoinDropCounter、購読側は PPCoinResourceBridge
    public interface IPPCoinGainNotifier
    {
        // コイン獲得時(コインの属性, 枚数)
        event Action<PPTypeAttribute, int> OnCoinGained;
    }

    // コインの枚数をバトルのリソース量へ変換する規則
    // 「まとめて落とすほど効率が上がる」といった非線形な変換へ差し替えられるよう分離してある
    public interface IPPCoinResourceConverter
    {
        // コイン枚数をリソース量へ変換する
        float Convert(int aCoinCount, float aRate);
    }

    // 枚数に係数を掛けるだけの線形変換。既定の実装
    public class PPLinearCoinResourceConverter : IPPCoinResourceConverter
    {
        // 枚数 × 係数をそのまま返す
        public float Convert(int aCoinCount, float aRate) => aCoinCount * aRate;
    }

    // プッシャー（物理）側とバトル側を繋ぐブリッジ。コイン獲得通知を購読し、生存ユニット全員のコインゲージへ均等分配する
    // 物理側とバトル側は直接結合させず、必ずここを経由させること
    public class PPCoinResourceBridge : MonoBehaviour
    {
        // コイン取得通知元。IPPCoinGainNotifier 実装コンポーネントを差す
        [Label("コイン取得通知コンポーネント")]
        [SerializeField] private MonoBehaviour mCoinNotifierSource;

        // インスペクタで差された通知元をインターフェースとして解決したもの
        private IPPCoinGainNotifier mCoinNotifier;
        // コイン枚数からゲージ量への変換ロジック
        private IPPCoinResourceConverter mConverter = new PPLinearCoinResourceConverter();
        // 加算先のパーティ。Bind されるまでは null
        private PPBattleParty mTargetParty;

        // インスペクタで指定された通知元をインターフェースへ解決する
        private void Awake()
        {
            mCoinNotifier = mCoinNotifierSource as IPPCoinGainNotifier;
            if (mConverter == null)
            {
                CustomConsoleLog.Warning("Resource", $"{nameof(mCoinNotifierSource)}はIPPCoinGainNotifierを実装している必要があります", this);
            }
        }

        // 指定陣営のパーティを加算先として設定し、コイン獲得通知の購読を開始する
        public void Bind(BattleManager aBattleManager, BattleSide aTargetSide)
        {
            if (aBattleManager.Context.GetParty(aTargetSide) is not PPBattleParty party)
            {
                CustomConsoleLog.Warning("Resource", "対象パーティが PPBattleParty ではありません。", this);
                return;
            }

            mTargetParty = party;

            if (mCoinNotifier != null)
            {
                mCoinNotifier.OnCoinGained += HandleCoinGained;
            }
        }

        // 購読を解除し、加算先パーティの参照を切る。バトル終了時や破棄時に呼ぶ
        public void Unbind()
        {
            if (mCoinNotifier != null)
            {
                mCoinNotifier.OnCoinGained -= HandleCoinGained;
            }
            mTargetParty = null;
        }

        // コイン獲得通知を受けて、枚数をゲージ量へ変換し生存ユニットへ均等分配する
        // コインの属性は分配先の選択には使わない
        private void HandleCoinGained(PPTypeAttribute a, int aCoinCount)
        {
            if (mTargetParty == null) return;

            float rate = mTargetParty.CoinConversionRate.CurrentValue;
            float amount = mConverter.Convert(aCoinCount, rate);
            // 実質変化のない変換(0枚など)はログを出さない
            if (amount <= 0f) return;

            CustomConsoleLog.Verbose("Resource", $"コイン{aCoinCount}枚をコインゲージ{amount:0.##}に変換します（レートx{rate}）。", this);
            PPGaugeUtility.DistributeToAliveUnits(mTargetParty.ActiveMembers, PPGaugeKind.Coin, amount);
        }

        // 破棄時にイベント購読が残らないよう解除する
        private void OnDestroy() => Unbind();
    }
}
