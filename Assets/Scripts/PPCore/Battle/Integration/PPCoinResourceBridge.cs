/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file PPCoinResourceBridge.cs
 * @author hqrse
 * @date 2026/06/21
 * @brief プッシャーからバトルリソースへと変換するブリッジ層
 * =====================================*/

using CommandBattleCore;
using CustomConsole;
using UnityEngine;
using AttributeUtility;

namespace PPCore
{
    // プッシャー（物理）側とバトル側を繋ぐブリッジ
    // IPPCoinGainNotifier のコイン獲得通知を購読し、
    // パーティの変換係数と IPPCoinResourceConverter を通して PPBattleResourcePool へリソースとして加算する
    // このクラスを挟むことで、物理側はバトルの存在を知らず、バトル側はコインの存在を知らずに済む
    // 両者を直接結合させず、必ずここを経由させること
    public class PPCoinResourceBridge : MonoBehaviour
    {
        // コイン取得通知元。IPPCoinGainNotifier 実装コンポーネントを差す
        [Label("コイン取得通知コンポーネント")]
        [SerializeField] private MonoBehaviour mCoinNotifierSource;

        // インスペクタで差された通知元をインターフェースとして解決したもの
        private IPPCoinGainNotifier mCoinNotifier;
        // コイン枚数からリソース量への変換ロジック
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
        // aBattleManager : 対象パーティを引くためのバトルマネージャ
        // aTargetSide : リソースを加算する陣営
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

        // コイン獲得通知を受けて、枚数をリソース量へ変換しプールへ加算する
        // a : 獲得したコインの属性。加算先のリソース種別になる
        // aCoinCount : 獲得枚数
        private void HandleCoinGained(PPTypeAttribute a, int aCoinCount)
        {
            if (mTargetParty == null) return;

            float rate = mTargetParty.CoinConversionRate.CurrentValue;
            float amount = mConverter.Convert(aCoinCount, rate);
            // 実質変化のない変換(0枚など)はログを出さない
            if (amount > 0f)
            {
                CustomConsoleLog.Verbose("Resource", $"コイン{aCoinCount}枚を{a}リソース{amount}に変換します（レートx{rate}）。", this);
            }
            mTargetParty.ResourcePool.Add(a, amount);
        }

        // 破棄時にイベント購読が残らないよう解除する
        private void OnDestroy() => Unbind();
    }
}
