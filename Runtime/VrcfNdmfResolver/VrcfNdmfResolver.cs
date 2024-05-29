using UnityEngine;
using VRC.SDKBase;

namespace GoobieTools.VrcfNdmfResolver
{
    public class VrcfNdmfResolver : MonoBehaviour, IEditorOnly
    {
        [Tooltip("This will fix any animation bindings that vrcfury references, so that they point to the correct objects after ndmf runs.")]
        public bool FixAnimationBindings = true;
        [Tooltip("This will fix any transform animations so that if positions of parents have been moved, the animations will still animate to the same positions in avatar space.\nThis only works if 'FixAnimationBindings' is also enabled.")]
        public bool FixTransformAnimations = true;
        //[Tooltip("This will lossy fix scale animations on transforms if the scales of parents have been modified.\nThis only works if 'FixTransformAnimations' is also enabled.")]
        //public bool LossyFixTransformScale = false;
    }
}
