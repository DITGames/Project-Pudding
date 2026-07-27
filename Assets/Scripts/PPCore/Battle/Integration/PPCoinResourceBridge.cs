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
    public class PPCoinResourceBridge : MonoBehaviour
    {
        [Label("コイン取得通知コンポーネント")]
        [SerializeField] private MonoBehaviour mCoinNotifierSource;

        private IPPCoinGainNotifier mCoinNotifier;
        private IPPCoinResourceConverter mConverter = new PPLinearCoinResourceConverter();
        private PPBattleParty mTargetParty;

        private void Awake()
        {
            mCoinNotifier = mCoinNotifierSource as IPPCoinGainNotifier;
            if (mConverter == null)
            {
                Debug.Log($"{nameof(mCoinNotifierSource)}はIPPCoinGainNotifierを実装している必要があります");
            }
        }

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

        public void Unbind()
        {
            if (mCoinNotifier != null)
            {
                mCoinNotifier.OnCoinGained -= HandleCoinGained;
            }
            mTargetParty = null;
        }

        private void HandleCoinGained(PPTypeAttribute a, int aCoinCount)
        {
            if (mTargetParty == null) return;

            float rate = mTargetParty.CoinConversionRate.CurrentValue;
            float amount = mConverter.Convert(aCoinCount, rate);
            mTargetParty.ResourcePool.Add(a, amount);
        }

        private void OnDestroy() => Unbind();
    }
}
