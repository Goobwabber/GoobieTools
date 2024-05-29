#if NDMF
using GoobieTools.AAOPatch.Editor;
using GoobieTools.Editor.Utilities;
using nadena.dev.ndmf;
using UnityEditor;
using UnityEngine;

[assembly: ExportsPlugin(typeof(AAOPatchPlugin))]

namespace GoobieTools.AAOPatch.Editor
{
    public class AAOPatchPlugin : Plugin<AAOPatchPlugin>
    {
        /// <summary>
        /// This name is used to identify the plugin internally, and can be used to declare BeforePlugin/AfterPlugin
        /// dependencies. If not set, the full type name will be used.
        /// </summary>
        public override string QualifiedName => "goobietools.aaopatch";

        /// <summary>
        /// The plugin name shown in debug UIs. If not set, the qualified name will be shown.
        /// </summary>
        public override string DisplayName => "AAO Resolve Patch";

        private static AnimationResolver? _resolver;

        protected override void Configure()
        {
            InPhase(BuildPhase.Optimizing).BeforePlugin("com.anatawa12.avatar-optimizer").Run("Resolve AAO Animations", ctx =>
            {
                if (_resolver != null)
                    Debug.LogWarning("Resolver loop not completed. Something may have gone wrong, but we will continue anyways.");

                // only do base animation layers bc i dont care.
                var animationLayers = ctx.AvatarDescriptor.baseAnimationLayers;
                _resolver = new(ctx.AvatarRootTransform, ctx.AssetContainer);
                _resolver.DebugLogging = true;

                foreach (var layer in animationLayers)
                    if (layer.animatorController != null)
                        _resolver.Add(layer.animatorController);
            });

            InPhase(BuildPhase.Optimizing).AfterPlugin("com.anatawa12.avatar-optimizer").Run("Fix AAO Animations", ctx =>
            {
                if (_resolver == null)
                    return; // nooooo

                // only do base animation layers bc i dont care.
                var animationLayers = ctx.AvatarDescriptor.baseAnimationLayers;
                
                for (int i = 0; i < animationLayers.Length; i++)
                {
                    if (animationLayers[i].animatorController == null)
                    {
                        Debug.Log($"{animationLayers[i].type} is null!");
                        continue;
                    }
                    Debug.Log($"Processing '{animationLayers[i].type}' layer.");
                    var newController = _resolver.Fix(animationLayers[i].animatorController);
                    animationLayers[i].animatorController = newController;
                }

                ctx.AvatarDescriptor.baseAnimationLayers = animationLayers;
                EditorUtility.SetDirty(ctx.AvatarDescriptor);

                _resolver = null;
            });
        }
    }
}
#endif