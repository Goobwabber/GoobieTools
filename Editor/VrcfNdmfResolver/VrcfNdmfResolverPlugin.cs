#if NDMF && VRCF
using GoobieTools.Editor.Models;
using GoobieTools.VrcfNdmfResolver.Editor;
using nadena.dev.ndmf;
using System.Collections.Generic;
using UnityEngine;
using VF.Model;
using VF.Model.Feature;

[assembly: ExportsPlugin(typeof(VrcfNdmfResolverPlugin))]

namespace GoobieTools.VrcfNdmfResolver.Editor
{
    public class VrcfNdmfResolverPlugin : Plugin<VrcfNdmfResolverPlugin>
    {
        /// <summary>
        /// This name is used to identify the plugin internally, and can be used to declare BeforePlugin/AfterPlugin
        /// dependencies. If not set, the full type name will be used.
        /// </summary>
        public override string QualifiedName => "goobietools.vrcfndmfresolver";

        /// <summary>
        /// The plugin name shown in debug UIs. If not set, the qualified name will be shown.
        /// </summary>
        public override string DisplayName => "Vrcf Ndmf Resolver";

        protected override void Configure()
        {
            InPhase(BuildPhase.Resolving).Run("Resolve Vrcf Animations", ctx =>
            {
                var mainComponent = ctx.AvatarRootTransform.GetComponentInChildren<VrcfNdmfResolver>();
                if (mainComponent is null || !mainComponent.FixAnimationBindings)
                {
                    Debug.Log("Resolve Vrcf Animations > Not fixing animation bindings, exiting.");
                    return;
                }

                if (VrcfNdmfResolverState.Resolver is not null)
                    Debug.LogWarning("Resolve Vrcf Animations > Previous resolver was not cleaned up! Something went wrong, but we can continue for now.");
                VrcfNdmfResolverState.Resolver = new(ctx.AvatarRootTransform, ctx.AssetContainer);

                var components = ctx.AvatarRootTransform.GetComponentsInChildren<VRCFury>();
                bool fcComponentFound = false;

                foreach (var component in components)
                {
                    if (component.content is not FullController fcComponent)
                        continue;
                    fcComponentFound = true;

                    // binding rewrites and stuff from full controller
                    List<RewriteBinding> rewriteBindings = new();
                    List<string> ignoreBindings = new();
                    foreach (var rewriteBinding in fcComponent.rewriteBindings)
                    {
                        if (rewriteBinding.delete)
                        {
                            ignoreBindings.Add(rewriteBinding.from);
                            continue;
                        }
                        rewriteBindings.Add(new RewriteBinding(rewriteBinding.from, rewriteBinding.to));
                    }

                    // add controllers
                    foreach (var controllerEntry in fcComponent.controllers)
                    {
                        var controller = controllerEntry.controller.objRef as RuntimeAnimatorController;
                        VrcfNdmfResolverState.Resolver.Add(controller, fcComponent.rootObjOverride ?? component.gameObject, rewriteBindings, ignoreBindings);
                    }
                }

                if (!fcComponentFound)
                    Debug.Log("Resolve Vrcf Animations > No VRCF Full Controller components found, exiting.");

                // we are finished, so tick flag
                VrcfNdmfResolverState.Resolved = true;
            });

            // this just exists so that if user does manual test build, it still works.
            // execute after aao bc it will change paths and stuff.
            InPhase(BuildPhase.Optimizing).AfterPlugin("com.anatawa12.avatar-optimizer").Run("Fix Vrcf Animations", ctx =>
            {
                if (VrcfNdmfResolverState.Resolver is null || !VrcfNdmfResolverState.Resolved)
                    return; // just skip ig.

                // fix vrcf animations
                VrcfNdmfResolverHook.FixVrcfAnimations(ctx.AvatarRootObject);

                // cleanup
                foreach (var component in ctx.AvatarRootTransform.GetComponentsInChildren<VrcfNdmfResolver>())
                    GameObject.DestroyImmediate(component);

                // reset stuff :)
                VrcfNdmfResolverState.Resolved = false;
                VrcfNdmfResolverState.Resolver = null;
            });
        }
    }
}
#endif