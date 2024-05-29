#if NDMF && VRCF
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using VF.Model.Feature;
using VF.Model;
using VRC.SDKBase.Editor.BuildPipeline;
using static VF.Model.Feature.FullController;
using System;

namespace GoobieTools.VrcfNdmfResolver.Editor
{
    internal class VrcfNdmfResolverHook : IVRCSDKPreprocessAvatarCallback
    {
        // -11000 is ndmf
        // -10000 is vrcfury
        // we need to run somewhere inbetween these.
        public int callbackOrder => -10100;

        public bool OnPreprocessAvatar(GameObject avatarGameObject)
        {
            if (VrcfNdmfResolverState.Resolver is null || !VrcfNdmfResolverState.Resolved)
                return true; // just skip ig.

            try
            {
                // fix vrcf animations
                FixVrcfAnimations(avatarGameObject);

                // cleanup
                foreach (var component in avatarGameObject.transform.GetComponentsInChildren<VrcfNdmfResolver>())
                    GameObject.DestroyImmediate(component);

                // reset stuff :)
                VrcfNdmfResolverState.Resolved = false;
                VrcfNdmfResolverState.Resolver = null;

                return true;
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                return false;
            }
        }

        internal static void FixVrcfAnimations(GameObject avatarGameObject)
        {
            var mainComponent = avatarGameObject.GetComponentInChildren<VrcfNdmfResolver>();
            if (mainComponent is null || !mainComponent.FixAnimationBindings)
            {
                Debug.Log("Fix Vrcf Animations > Not fixing animation bindings, exiting.");
                return;
            }

            var components = avatarGameObject.GetComponentsInChildren<VRCFury>();

            Dictionary<AnimationClip, AnimationClip> modifiedAnimationClips = new(); // reuse for speed
            bool fcComponentFound = false;

            foreach (var component in components)
            {
                if (component.content is not FullController fcComponent)
                    continue;

                fcComponentFound = true;

                var cTransform = component.transform;
                List<ControllerEntry> newControllers = new List<ControllerEntry>();

                foreach (var controllerEntry in fcComponent.controllers)
                {
                    var controller = controllerEntry.controller.objRef as RuntimeAnimatorController;
                    newControllers.Add(new ControllerEntry
                    {
                        controller = VrcfNdmfResolverState.Resolver.Fix(controller, 
                            fcComponent.rootObjOverride != null ? fcComponent.rootObjOverride.transform : component.transform),
                        type = controllerEntry.type
                    });
                }

                fcComponent.controllers = newControllers;
                // just so happens that we fix any of the bindings while we are here, so clear the rewrite bindings list.
                fcComponent.rewriteBindings.Clear();

                EditorUtility.SetDirty(component);
                Debug.Log($"Fix Vrcf Animations > Full Controller at path '{AnimationUtility.CalculateTransformPath(component.transform, avatarGameObject.transform)}' fixed.");
            }

            if (!fcComponentFound)
                Debug.Log("Fix Vrcf Animations > No VRCF Full Controller components found, exiting.");
        }
    }
}
#endif