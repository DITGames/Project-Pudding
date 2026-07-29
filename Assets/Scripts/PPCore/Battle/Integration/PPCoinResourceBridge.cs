/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file PPCoinResourceBridge.cs
 * @author hqrse
 * @date 2026/06/21
 * @brief プッシャーからバトルリソースへと変換するブリッジ層
 * =====================================*/
using CommandBattleCore;
using UnityEngine;

namespace PPCore
{
    /// <summary>
    /// プッシャー（物理）側とバトル側を繋ぐブリッジ。
    /// <para>
    /// <see cref="IPPCoinGainNotifier"/> のコイン獲得通知を購読し、
    /// パーティの変換係数と <see cref="IPPCoinResourceConverter"/> を通して
    /// <see cref="PPBattleResourcePool"/> へリソースとして加算する。
    /// </para>
    /// <para>
    /// このクラスを挟むことで、物理側はバトルの存在を知らず、バトル側はコインの存在を知らずに済む。
    /// 両者を直接結合させず、必ずここを経由させること。
    /// </para>
    /// </summary>
    public class PPCoinResourceBridge : MonoBehaviour
    {
        /// <summary>コイン取得通知元。<see cref="IPPCoinGainNotifier"/> 実装コンポーネントを差す。</summary>
        [Label("コイン取得通知コンポーネント")]
        [SerializeField] private MonoBehaviour mCoinNotifierSource;

        /// <summary>インスペクタで差された通知元をインターフェースとして解決したもの。</summary>
        private IPPCoinGainNotifier mCoinNotifier;
        /// <summary>コイン枚数からリソース量への変換ロジック。</summary>
        private IPPCoinResourceConverter mConverter = new PPLinearCoinResourceConverter();
        /// <summary>加算先のパーティ。<see cref="Bind"/> されるまでは null。</summary>
        private PPBattleParty mTargetParty;

        /// <summary>
        /// インスペクタで指定された通知元をインターフェースへ解決する。
        /// </summary>
        private void Awake()
        {
            mCoinNotifier = mCoinNotifierSource as IPPCoinGainNotifier;
            if (mConverter == null)
            {
                Debug.Log($"{nameof(mCoinNotifierSource)}はIPPCoinGainNotifierを実装している必要があります");
            }
        }

        /// <summary>
        /// 指定陣営のパーティを加算先として設定し、コイン獲得通知の購読を開始する。
        /// </summary>
        /// <param name="aBattleManager">対象パーティを引くためのバトルマネージャ。</param>
        /// <param name="aTargetSide">リソースを加算する陣営。</param>
        public void Bind(BattleManager aBattleManager, BattleSide aTargetSide)
        {
            if (aBattleManager.Context.GetParty(aTargetSide) is not PPBattleParty party)
            {
                Debug.Log("対象パーティが PPBattleParty ではありません。");
                return;
            }

            mTargetParty = party;

            if (mCoinNotifier != null)
            {
                mCoinNotifier.OnCoinGained += HandleCoinGained;
            }
        }

        /// <summary>
        /// 購読を解除し、加算先パーティの参照を切る。バトル終了時や破棄時に呼ぶ。
        /// </summary>
        public void Unbind()
        {
            if (mCoinNotifier != null)
            {
                mCoinNotifier.OnCoinGained -= HandleCoinGained;
            }
            mTargetParty = null;
        }

        /// <summary>
        /// コイン獲得通知を受けて、枚数をリソース量へ変換しプールへ加算する。
        /// </summary>
        /// <param name="a">獲得したコインの属性。加算先のリソース種別になる。</param>
        /// <param name="aCoinCount">獲得枚数。</param>
        private void HandleCoinGained(PPTypeAttribute a, int aCoinCount)
        {
            if (mTargetParty == null) return;

            float rate = mTargetParty.CoinConversionRate.CurrentValue;
            float amount = mConverter.Convert(aCoinCount, rate);
            mTargetParty.ResourcePool.Add(a, amount);
        }

        /// <summary>破棄時にイベント購読が残らないよう解除する。</summary>
        private void OnDestroy() => Unbind();
    }
}
