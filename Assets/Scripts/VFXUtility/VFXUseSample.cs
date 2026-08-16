using System.Collections;
using UnityEngine;

namespace VFXUtility
{
    public class VFXUseSample : MonoBehaviour
    {
        [SerializeField]
        private VFXParameterComponent mVfxParameterComponent;
        
        [SerializeField]
        private int mFirstCount = 128;
        
        [SerializeField]
        private int mIncreaseCount = 4;
        
        private int mCurrentValue;
        
        Coroutine mCoroutine;
        void Start()
        {
            if (mVfxParameterComponent == null)
            {
                mVfxParameterComponent = GetComponent<VFXParameterComponent>();
            }

            if (mVfxParameterComponent != null)
            {
                mVfxParameterComponent.ActivateVFX("VFX_Burst");
                mVfxParameterComponent.ApplyParameter("VFX_Burst", "Burst");
            }
            
            mCurrentValue = mFirstCount;
            
            mCoroutine = StartCoroutine(VFXFunc());
        }

        IEnumerator VFXFunc()
        {
            while (true)
            {
                yield return new WaitForSeconds(2);
            
                mCurrentValue += mIncreaseCount;

                if (mVfxParameterComponent != null)
                {
                    mVfxParameterComponent.ApplyParameter("VFX_Burst", "SpawnCount", mCurrentValue);
                    mVfxParameterComponent.ApplyParameter("VFX_Burst", "Burst");
                }
                else
                {
                    yield break;
                }
            }
        }
    }
}