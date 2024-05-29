#if NDMF && VRCF
using GoobieTools.Editor.Utilities;

namespace GoobieTools.VrcfNdmfResolver.Editor
{
    internal class VrcfNdmfResolverState
    {
        public static bool Resolved { get; set; } = false;
        public static AnimationResolver? Resolver { get; set; }
    }
}
#endif