/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file VFXPoolManager.cs
 * @author hqrse
 * @date 2026/08/16
 * @brief VisualEffectインスタンスをVFXアセット単位でプールし、生成コストを抑える汎用マネージャー
 * =====================================*/

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.VFX;

namespace VFXUtility
{
    public class VFXPoolManager : MonoBehaviour
    {
        private static VFXPoolManager sInstance;

        private readonly Dictionary<VisualEffectAsset, Stack<VisualEffect>> mPool = new();

        public static VFXPoolManager Instance
        {
            get
            {
                if (sInstance == null)
                {
                    GameObject go = new GameObject(nameof(VFXPoolManager));
                    sInstance = go.AddComponent<VFXPoolManager>();
                    DontDestroyOnLoad(go);
                }
                return sInstance;
            }
        }

        // 指定アセットのVisualEffectインスタンスを1つ取得する(プールに空きがなければ新規生成)
        // 生成物はワールド座標で指定位置に配置され、本マネージャー配下に置かれる(呼び出し元のTransformには追従しない)
        // aAsset: 対象VFXアセット / aPosition: ワールド座標 / aRotation: ワールド回転
        public VisualEffect Rent(VisualEffectAsset aAsset, Vector3 aPosition, Quaternion aRotation)
        {
            Stack<VisualEffect> stack = GetOrCreateStack(aAsset);

            VisualEffect visualEffect;
            if (stack.Count > 0)
            {
                visualEffect = stack.Pop();
            }
            else
            {
                GameObject go = new GameObject($"VFX_{aAsset.name}");
                visualEffect = go.AddComponent<VisualEffect>();
                visualEffect.visualEffectAsset = aAsset;
            }

            Transform t = visualEffect.transform;
            t.SetParent(transform, false);
            t.SetPositionAndRotation(aPosition, aRotation);
            visualEffect.gameObject.SetActive(true);
            return visualEffect;
        }

        // インスタンスを停止してプールへ返却する
        // aVisualEffect: 返却対象
        public void Return(VisualEffect aVisualEffect)
        {
            if (aVisualEffect == null)
            {
                return;
            }

            aVisualEffect.Stop();
            aVisualEffect.gameObject.SetActive(false);
            aVisualEffect.transform.SetParent(transform, false);

            Stack<VisualEffect> stack = GetOrCreateStack(aVisualEffect.visualEffectAsset);
            stack.Push(aVisualEffect);
        }

        private Stack<VisualEffect> GetOrCreateStack(VisualEffectAsset aAsset)
        {
            if (!mPool.TryGetValue(aAsset, out Stack<VisualEffect> stack))
            {
                stack = new Stack<VisualEffect>();
                mPool[aAsset] = stack;
            }
            return stack;
        }
    }
}
