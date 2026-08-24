using AttributeUtility;
using UnityEngine;

namespace AnimSequencer2D
{
    public class AnimSequencePlayerHelper : MonoBehaviour
    {
        [Label("初期ステート")]
        public string mInitialAnimationName = string.Empty;
        
        [Label("AnimSequencePlayer")]
        [SerializeField] private AnimSequencePlayer mAnimSequencePlayer;
        
        void Start()
        {
            if (mAnimSequencePlayer == null)
            {
                mAnimSequencePlayer = GetComponent<AnimSequencePlayer>();
            }

            if (mAnimSequencePlayer != null && mInitialAnimationName != string.Empty)
            {
                mAnimSequencePlayer.PlaySequence(mInitialAnimationName);
            }
        }
    }
}