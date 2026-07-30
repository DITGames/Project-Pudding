/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file PPBattleResourcePool.cs
 * @author hqrse
 * @date 2026/06/21
 * @brief バトルで使用されるリソース定義
 * =====================================*/
using CommandBattleCore;

namespace PPCore
{
    // パーティが保持する属性別の行動リソースプール
    // プッシャー台から落ちたコインが PPCoinResourceBridge を経由してここへ加算され、
    // スキル発動時に PPResourceCost の分だけ消費される
    // 属性ごとに ResourceParameter を 1 本ずつ持ち、添字は PPTypeAttribute の値をそのまま使う
    public class PPBattleResourcePool
    {
        // 属性ごとの行動用リソース。添字は PPTypeAttribute と対応する
        private readonly ResourceParameter[] mResourcePools;

        // 全属性分のリソースを生成する
        // ResourceParameter は最大値で初期化されるため、
        // 生成直後に上限分を Damage して現在値 0 から始まるようにしている
        // aMaxPerType : 属性 1 種あたりのリソース上限
        public PPBattleResourcePool(int aMaxPerType)
        {
            mResourcePools = new ResourceParameter[PPTypeAttributeDefinition.TypeCount];
            for (int i = 0; i < mResourcePools.Length; i++)
            {
                var p = new ResourceParameter(aMaxPerType);
                p.Damage(aMaxPerType);  // 初期値0に設定
                mResourcePools[i] = p;
            }
        }

        // 指定属性のリソースパラメータ本体を取得する
        // a : 対象の属性
        public ResourceParameter Pool(PPTypeAttribute a) => mResourcePools[(int)a];
        // 指定属性のリソース現在値を取得する
        // a : 対象の属性
        public float Current(PPTypeAttribute a) => mResourcePools[(int)a].Current;
        // 指定属性のリソース最大値を取得する
        // a : 対象の属性
        public float Max(PPTypeAttribute a) => mResourcePools[(int)a].Max.CurrentValue;
        // 指定属性のリソースを加算する。上限を超えた分は切り捨てられる
        // a : 対象の属性
        // aAmount : 加算量
        public void Add(PPTypeAttribute a, float aAmount) => mResourcePools[(int)a].Recover(aAmount);

        // コストを支払えるかを実際には消費せずに判定する
        // AI の行動候補の絞り込みや UI のグレーアウト判定など、事前チェックに使う
        // 浮動小数の誤差で「ちょうど足りている」が不足扱いにならないよう、微小値を足して比較している
        // aCost : 判定するコスト。null または無コストなら常に true
        // return : 全属性分のリソースが足りていれば true
        public bool CanPay(PPResourceCost aCost)
        {
            if(aCost == null || aCost.IsFree)
                return true;
            for (int i = 0; i < PPTypeAttributeDefinition.TypeCount; i++)
            {
                float need = aCost.Get(i);
                if(need > 0f && mResourcePools[i].Current + 0.0001f < need)
                    return false;
            }
            return true;
        }

        // コストを実際に消費する
        // 先に CanPay で全属性を確認してから減算するため、
        // 途中で不足して「一部だけ支払われた」状態にはならない
        // aCost : 支払うコスト。null または無コストなら何もせず true
        // return : 支払えた場合 true。不足していれば何も消費せず false
        public bool TryPay(PPResourceCost aCost)
        {
            if(aCost == null || aCost.IsFree)
                return true;
            if(!CanPay(aCost))
                return false;
            for (int i = 0; i < PPTypeAttributeDefinition.TypeCount; i++)
            {
                float need = aCost.Get(i);
                if (need > 0f)
                {
                    mResourcePools[i].Damage(need);
                }
            }
            return true;
        }
    }
}
