// MIT License
// 
// Copyright (c) 2022 bd_
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.

#if NDMF
using GoobieTools.Editor.Extensions;
using nadena.dev.ndmf;
using nadena.dev.ndmf.util;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using VRC.SDKBase;
using UnityObject = UnityEngine.Object;

namespace GoobieTools.Editor.Utilities
{
    // https://github.com/bdunderscore/modular-avatar/blob/6dcea7fa5eaacc66a3500ab36ddcdbbb52abd836/Editor/Animation/AnimationUtil.cs
    public static class AnimationUtilities
    {
        private const string _samplePathPackage = "Packages/com.vrchat.avatars/Samples/AV3 Demo Assets/Animation/Controllers";

        private const string _samplePathLegacy = "Assets/VRCSDK/Examples3/Animation/Controllers";

        private const string _guidGestureHandsonlyMask = "b2b8bad9583e56a46a3e21795e96ad92";

        public static AnimatorController? DeepCloneAnimator(UnityObject assetContainer, RuntimeAnimatorController controller)
        {
            if (controller == null)
                return null;

            var merger = new AnimatorCombiner(assetContainer, controller.name + " (cloned)");
            switch (controller)
            {
                case AnimatorController ac:
                    merger.AddController("", ac, null);
                    break;
                case AnimatorOverrideController oac:
                    merger.AddOverrideController("", oac, null);
                    break;
                default:
                    throw new Exception("Unknown RuntimeAnimatorContoller type " + controller.GetType());
            }

            return merger.Finish();
        }

        public static VRCAvatarDescriptor.CustomAnimLayer[]? CloneLayers(UnityObject assetContainer, VRCAvatarDescriptor.CustomAnimLayer[]? layers)
        {
            if (layers == null)
                return null;

            for (int i = 0; i < layers.Length; i++)
            {
                var layer = layers[i];

                if (layer.animatorController != null && assetContainer.IsTemporaryAsset(layer.animatorController))
                {
                    layer.animatorController = DeepCloneAnimator(assetContainer, layer.animatorController);
                }
                layers[i] = layer;
            }

            return layers;
        }

        public static AnimatorController? GetOrInitializeController(this BuildContext context, VRCAvatarDescriptor.AnimLayerType type)
        {
            var baseAnimationLayers = FindLayer(context.AvatarDescriptor.baseAnimationLayers);
            return baseAnimationLayers ? baseAnimationLayers : null ?? FindLayer(context.AvatarDescriptor.specialAnimationLayers);

            AnimatorController? FindLayer(IList<VRCAvatarDescriptor.CustomAnimLayer> layers)
            {
                for (int i = 0; i < layers.Count; i++)
                {
                    var layer = layers[i];
                    if (layer.type != type)
                        continue;

                    if (layer.animatorController != null && !layer.isDefault)
                        return (layer.animatorController as AnimatorController)!;

                    layer.animatorController = ResolveLayerController(layer);
                    if (type == VRCAvatarDescriptor.AnimLayerType.Gesture)
                    {
                        layer.mask = AssetDatabase.LoadAssetAtPath<AvatarMask>(
                            AssetDatabase.GUIDToAssetPath(_guidGestureHandsonlyMask)
                        );
                    }

                    layers[i] = layer;

                    return (layer.animatorController as AnimatorController)!;
                }

                return null;
            }
        }


        private static AnimatorController? ResolveLayerController(VRCAvatarDescriptor.CustomAnimLayer layer)
        {
            AnimatorController? controller = null;
            if (!layer.isDefault && layer.animatorController != null && layer.animatorController is AnimatorController c)
            {
                controller = c;
            }
            else
            {
                string? name = layer.type switch
                {
                    VRCAvatarDescriptor.AnimLayerType.Action => "Action",
                    VRCAvatarDescriptor.AnimLayerType.Additive => "Idle",
                    VRCAvatarDescriptor.AnimLayerType.Base => "Locomotion",
                    VRCAvatarDescriptor.AnimLayerType.Gesture => "Hands",
                    VRCAvatarDescriptor.AnimLayerType.Sitting => "Sitting",
                    VRCAvatarDescriptor.AnimLayerType.FX => "Face",
                    VRCAvatarDescriptor.AnimLayerType.TPose => "UtilityTPose",
                    VRCAvatarDescriptor.AnimLayerType.IKPose => "UtilityIKPose",
                    _ => null
                };

                if (name == null)
                    return controller;

                name = "/vrc_AvatarV3" + name + "Layer.controller";

                controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(_samplePathPackage + name);
                if (controller == null)
                    controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(_samplePathLegacy + name);
            }

            return controller;
        }

        public static bool IsProxyAnimation(this Motion m)
        {
            var path = AssetDatabase.GetAssetPath(m);

            // This is a fairly wide condition in order to deal with:
            // 1. Future additions of proxy animations (so GUIDs are out)
            // 2. Unitypackage based installations of the VRCSDK
            // 3. VCC based installations of the VRCSDK
            // 4. Very old VCC based installations of the VRCSDK where proxy animations were copied into Assets
            return path.Contains("/AV3 Demo Assets/Animation/ProxyAnim/proxy")
                   || path.Contains("/VRCSDK/Examples3/Animation/ProxyAnim/proxy")
                   || path.StartsWith("Packages/com.vrchat.");
        }

        /// <summary>
        /// Enumerates all states in an animator controller
        /// </summary>
        /// <param name="ac"></param>
        /// <returns></returns>
        internal static IEnumerable<AnimatorState> States(AnimatorController ac)
        {
            HashSet<AnimatorStateMachine> visitedStateMachines = new();
            Queue<AnimatorStateMachine> pending = new();

            foreach (var layer in ac.layers)
                if (layer.stateMachine != null)
                    pending.Enqueue(layer.stateMachine);

            while (pending.Count > 0)
            {
                var next = pending.Dequeue();
                if (!visitedStateMachines.Add(next))
                    continue;

                foreach (var child in next.stateMachines)
                    if (child.stateMachine != null)
                        pending.Enqueue(child.stateMachine);

                foreach (var state in next.states)
                    yield return state.state;
            }
        }
    }

    internal class AnimatorCombiner
    {
        private int _controllerBaseLayer;
        private Dictionary<UnityObject, UnityObject>? _cloneMap;

        private readonly DeepClone _deepClone;
        private readonly AnimatorController _combined;

        private List<AnimatorControllerLayer> _layers = new();
        private readonly Dictionary<string, AnimatorControllerParameter> _parameters = new();
        private readonly Dictionary<KeyValuePair<string, AnimatorStateMachine>, AnimatorStateMachine> _stateMachines = new();

        public VRC_AnimatorLayerControl.BlendableLayer? BlendableLayer;

        public AnimatorCombiner(UnityObject assetContainer, string assetName)
        {
            _combined = new AnimatorController();
            if (assetContainer != null && EditorUtility.IsPersistent(assetContainer))
            {
                AssetDatabase.AddObjectToAsset(_combined, assetContainer);
            }

            _combined.name = assetName;

            _deepClone = new DeepClone(assetContainer);
        }

        public AnimatorController Finish()
        {
            PruneEmptyLayers();

            _combined.parameters = _parameters.Values.ToArray();
            _combined.layers = _layers.ToArray();
            return _combined;
        }

        private void PruneEmptyLayers()
        {
            var originalLayers = _layers;
            int[] layerIndexMappings = new int[originalLayers.Count];

            List<AnimatorControllerLayer> newLayers = new();

            for (int i = 0; i < originalLayers.Count; i++)
            {
                if (i > 0 && IsEmptyLayer(originalLayers[i]))
                {
                    layerIndexMappings[i] = -1;
                }
                else
                {
                    layerIndexMappings[i] = newLayers.Count;
                    newLayers.Add(originalLayers[i]);
                }
            }

            foreach (var layer in newLayers)
            {
                if (layer.stateMachine == null) continue;

                foreach (var asset in layer.stateMachine.ReferencedAssets(includeScene: false))
                {
                    if (asset is AnimatorState alc)
                    {
                        alc.behaviours = AdjustStateBehaviors(alc.behaviours);
                    }
                    else if (asset is AnimatorStateMachine asm)
                    {
                        asm.behaviours = AdjustStateBehaviors(asm.behaviours);
                    }
                }
            }

            _layers = newLayers;

            StateMachineBehaviour[] AdjustStateBehaviors(StateMachineBehaviour[] behaviours)
            {
                if (behaviours.Length == 0) return behaviours;

                var newBehaviors = new List<StateMachineBehaviour>();
                foreach (var b in behaviours)
                {
                    if (b is VRCAnimatorLayerControl alc && alc.playable == BlendableLayer)
                    {
                        int newLayer = -1;
                        if (alc.layer >= 0 && alc.layer < layerIndexMappings.Length)
                        {
                            newLayer = layerIndexMappings[alc.layer];
                        }

                        if (newLayer != -1)
                        {
                            alc.layer = newLayer;
                            newBehaviors.Add(alc);
                        }
                    }
                    else
                    {
                        newBehaviors.Add(b);
                    }
                }

                return newBehaviors.ToArray();
            }
        }

        private bool IsEmptyLayer(AnimatorControllerLayer layer)
        {
            if (layer.syncedLayerIndex >= 0) return false;
            if (layer.avatarMask != null) return false;

            return layer.stateMachine == null
                   || (layer.stateMachine.states.Length == 0 && layer.stateMachine.stateMachines.Length == 0);
        }

        public void AddController(string basePath, AnimatorController controller, bool? writeDefaults,
            bool forceFirstLayerWeight = false)
        {
            _controllerBaseLayer = _layers.Count;
            _cloneMap = new Dictionary<UnityObject, UnityObject>();

            foreach (var param in controller.parameters)
            {
                if (_parameters.TryGetValue(param.name, out var acp))
                {
                    if (acp.type != param.type)
                    {
                        Debug.LogError("Animator merge parameter type mismatch");
                    }
                    continue;
                }

                _parameters.Add(param.name, param);
            }

            bool first = true;
            var layers = controller.layers;
            foreach (var layer in layers)
            {
                InsertLayer(basePath, layer, first, writeDefaults, layers);
                if (first && forceFirstLayerWeight)
                {
                    _layers[^1].defaultWeight = 1;
                }

                first = false;
            }
        }

        public void AddOverrideController(string basePath, AnimatorOverrideController overrideController,
            bool? writeDefaults)
        {
            var controller = overrideController.runtimeAnimatorController as AnimatorController;
            if (controller == null)
                return;

            _deepClone.OverrideController = overrideController;
            try
            {
                AddController(basePath, controller, writeDefaults);
            }
            // ReSharper disable once RedundantEmptyFinallyBlock
            finally
            {
            }
        }

        private void InsertLayer(string basePath, AnimatorControllerLayer layer, bool first,
            bool? writeDefaults, IReadOnlyList<AnimatorControllerLayer> layers)
        {
            if (_cloneMap == null)
                return;

            var newLayer = new AnimatorControllerLayer
            {
                name = layer.name,
                avatarMask = layer.avatarMask,
                blendingMode = layer.blendingMode,
                defaultWeight = first ? 1 : layer.defaultWeight,
                syncedLayerIndex = layer.syncedLayerIndex,
                syncedLayerAffectsTiming = layer.syncedLayerAffectsTiming,
                iKPass = layer.iKPass,
                stateMachine = MapStateMachine(basePath, layer.stateMachine),
            };

            UpdateWriteDefaults(newLayer.stateMachine, writeDefaults);


            if (newLayer.syncedLayerIndex != -1 && newLayer.syncedLayerIndex >= 0 &&
                newLayer.syncedLayerIndex < layers.Count)
            {
                // Transfer any motion overrides onto the new synced layer
                var baseLayer = layers[newLayer.syncedLayerIndex];
                foreach (var state in WalkAllStates(baseLayer.stateMachine))
                {
                    var overrideMotion = layer.GetOverrideMotion(state);
                    if (overrideMotion != null)
                    {
                        newLayer.SetOverrideMotion((AnimatorState)_cloneMap[state], overrideMotion);
                    }

                    var overrideBehaviors = (StateMachineBehaviour[])layer.GetOverrideBehaviours(state)?.Clone()!;
                    for (int i = 0; i < overrideBehaviors.Length; i++)
                    {
                        overrideBehaviors[i] = _deepClone.DoClone(overrideBehaviors[i])!;
                        AdjustBehavior(overrideBehaviors[i]);
                    }

                    newLayer.SetOverrideBehaviours((AnimatorState)_cloneMap[state], overrideBehaviors);
                }

                newLayer.syncedLayerIndex += _controllerBaseLayer;
            }

            _layers.Add(newLayer);
        }

        private static IEnumerable<AnimatorState> WalkAllStates(AnimatorStateMachine animatorStateMachine)
        {
            HashSet<UnityObject> visited = new();

            foreach (var state in VisitStateMachine(animatorStateMachine))
            {
                yield return state;
            }

            yield break;

            IEnumerable<AnimatorState> VisitStateMachine(AnimatorStateMachine layerStateMachine)
            {
                if (!visited.Add(layerStateMachine)) yield break;

                foreach (var state in layerStateMachine.states)
                {
                    if (state.state == null) continue;

                    yield return state.state;
                }

                foreach (var child in layerStateMachine.stateMachines)
                {
                    if (child.stateMachine == null) continue;

                    if (!visited.Add(child.stateMachine))
                        continue;

                    foreach (var state in VisitStateMachine(child.stateMachine))
                    {
                        yield return state;
                    }
                }
            }
        }

        private static void UpdateWriteDefaults(AnimatorStateMachine stateMachine, bool? writeDefaults)
        {
            if (!writeDefaults.HasValue)
                return;

            var queue = new Queue<AnimatorStateMachine>();
            queue.Enqueue(stateMachine);
            while (queue.Count > 0)
            {
                var sm = queue.Dequeue();
                foreach (var state in sm.states)
                {
                    state.state.writeDefaultValues = writeDefaults.Value;
                }

                foreach (var child in sm.stateMachines)
                {
                    queue.Enqueue(child.stateMachine);
                }
            }
        }

        private AnimatorStateMachine MapStateMachine(string basePath, AnimatorStateMachine layerStateMachine)
        {
            var cacheKey = new KeyValuePair<string, AnimatorStateMachine>(basePath, layerStateMachine);

            if (_stateMachines.TryGetValue(cacheKey, out var asm))
            {
                return asm;
            }

            asm = _deepClone.DoClone(layerStateMachine, basePath, _cloneMap);

            foreach (var state in WalkAllStates(asm!))
            {
                foreach (var behavior in state.behaviours)
                {
                    AdjustBehavior(behavior);
                }
            }

            _stateMachines[cacheKey] = asm!;
            return asm!;
        }

        private void AdjustBehavior(StateMachineBehaviour behavior)
        {
            switch (behavior)
            {
                case VRCAnimatorLayerControl layerControl:
                    {
                        // intra-animator cases.
                        layerControl.layer += _controllerBaseLayer;
                        break;
                    }
            }
        }
    }

    // https://github.com/bdunderscore/modular-avatar/blob/6dcea7fa5eaacc66a3500ab36ddcdbbb52abd836/Editor/Animation/DeepClone.cs
    internal class DeepClone
    {
        private readonly bool _isSaved;
        private readonly UnityObject _combined;

        public AnimatorOverrideController? OverrideController { get; set; }

        public DeepClone(UnityEngine.Object assetContainer)
        {
            _isSaved = assetContainer != null && EditorUtility.IsPersistent(assetContainer);
            _combined = assetContainer!;
        }

        public T? DoClone<T>(T? original, string? basePath = null, Dictionary<UnityObject, UnityObject>? cloneMap = null) where T : UnityObject
        {
            if (original == null)
                return null;

            cloneMap ??= new Dictionary<UnityObject, UnityObject>();

            Func<UnityObject, UnityObject>? visitor = null;
            if (basePath != null)
            {
                visitor = o => _ = CloneWithPathMapping(o, basePath)!;
            }

            // We want to avoid trying to copy assets not part of the animation system (eg - textures, meshes,
            // MonoScripts...), so check for the types we care about here
            switch (original)
            {
                // Any object referenced by an animator that we intend to mutate needs to be listed here.
                case Motion _:
                case AnimatorController _:
                case AnimatorState _:
                case AnimatorStateMachine _:
                case AnimatorTransitionBase _:
                case StateMachineBehaviour _:
                    break; // We want to clone these types

                // Leave textures, materials, and script definitions alone
                case Texture2D _:
                case MonoScript _:
                case Material _:
                    return original;

                // Also avoid copying unknown scriptable objects.
                // This ensures compatibility with e.g. avatar remote, which stores state information in a state
                // behaviour referencing a custom ScriptableObject
                case ScriptableObject _:
                    return original;

                default:
                    throw new Exception($"Unknown type referenced from animator: {original.GetType()}");
            }

            // When using AnimatorOverrideController, replace the original AnimationClip based on AnimatorOverrideController.
            if (OverrideController != null && original is AnimationClip srcClip)
            {
                var overrideClip = OverrideController[srcClip] as T;
                if (overrideClip != null)
                {
                    original = overrideClip;
                }
            }

            if (cloneMap.TryGetValue(original, out var value))
            {
                return (T)value;
            }

            var obj = visitor?.Invoke(original);
            if (obj != null)
            {
                cloneMap[original] = obj;
                if (obj != original)
                {
                    ObjectRegistry.RegisterReplacedObject(original, obj);
                }

                return (T)obj;
            }

            var ctor = original.GetType().GetConstructor(Type.EmptyTypes);
            if (ctor == null || original is ScriptableObject)
            {
                obj = UnityObject.Instantiate(original);
            }
            else
            {
                obj = (T)ctor.Invoke(Array.Empty<object>());
                EditorUtility.CopySerialized(original, obj);
            }

            cloneMap[original] = obj;
            ObjectRegistry.RegisterReplacedObject(original, obj);

            if (_isSaved)
            {
                AssetDatabase.AddObjectToAsset(obj, _combined);
            }

            SerializedObject so = new(obj);
            var prop = so.GetIterator();

            bool enterChildren = true;
            while (prop.Next(enterChildren))
            {
                enterChildren = true;
                // ReSharper disable once SwitchStatementMissingSomeEnumCasesNoDefault
                switch (prop.propertyType)
                {
                    case SerializedPropertyType.ObjectReference:
                        {
                            var newObj = DoClone(prop.objectReferenceValue, basePath, cloneMap);
                            prop.objectReferenceValue = newObj;
                            break;
                        }
                    // Iterating strings can get super slow...
                    case SerializedPropertyType.String:
                        enterChildren = false;
                        break;
                }
            }

            so.ApplyModifiedPropertiesWithoutUndo();

            return (T)obj;
        }

        private UnityObject? CloneWithPathMapping(UnityObject o, string basePath)
        {
            switch (o)
            {
                // We'll always rebase if the asset is non-persistent, because we can't reference a nonpersistent asset
                // from a persistent asset. If the asset is persistent, skip cases where path editing isn't required,
                // or where this is one of the special VRC proxy animations.
                case AnimationClip clip when EditorUtility.IsPersistent(o) && (basePath == "" || clip.IsProxyAnimation()):
                    return clip;
                case AnimationClip clip:
                    {
                        AnimationClip newClip = new()
                        {
                            name = "rebased " + clip.name
                        };
                        if (_isSaved)
                        {
                            AssetDatabase.AddObjectToAsset(newClip, _combined);
                        }

                        foreach (var binding in AnimationUtility.GetCurveBindings(clip))
                        {
                            var newBinding = binding;
                            newBinding.path = MapPath(binding, basePath);
                            newClip.SetCurve(newBinding.path, newBinding.type, newBinding.propertyName,
                                AnimationUtility.GetEditorCurve(clip, binding));
                        }

                        foreach (var objBinding in AnimationUtility.GetObjectReferenceCurveBindings(clip))
                        {
                            var newBinding = objBinding;
                            newBinding.path = MapPath(objBinding, basePath);
                            AnimationUtility.SetObjectReferenceCurve(newClip, newBinding,
                                AnimationUtility.GetObjectReferenceCurve(clip, objBinding));
                        }

                        newClip.wrapMode = clip.wrapMode;
                        newClip.legacy = clip.legacy;
                        newClip.frameRate = clip.frameRate;
                        newClip.localBounds = clip.localBounds;
                        AnimationUtility.SetAnimationClipSettings(newClip, AnimationUtility.GetAnimationClipSettings(clip));

                        return newClip;
                    }
                case Texture:
                    return o;
                default:
                    return null;
            }
        }

        private static string MapPath(EditorCurveBinding binding, string basePath)
        {
            if (binding.type == typeof(Animator) && binding.path == "")
            {
                return "";
            }
            else
            {
                var newPath = binding.path == "" ? basePath : basePath + binding.path;
                if (newPath.EndsWith("/"))
                {
                    newPath = newPath.Substring(0, newPath.Length - 1);
                }

                return newPath;
            }
        }
    }
}
#endif