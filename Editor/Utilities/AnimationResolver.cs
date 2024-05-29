#if NDMF
using GoobieTools.Editor.Extensions;
using GoobieTools.Editor.Models;
using nadena.dev.ndmf;
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using Object = UnityEngine.Object;
using Stopwatch = System.Diagnostics.Stopwatch;

namespace GoobieTools.Editor.Utilities
{
    public class AnimationResolver
    {
        public bool FixTransformCurves { get; set; } = true;
        public bool DebugLogging { get; set; } = false;

        private readonly Transform _avatarRoot;
        private readonly Object _assetContainer;
        private readonly Dictionary<string, Transform> _transformsDict = new();
        private readonly Dictionary<Transform, TransformData> _originalTransforms = new();

        public AnimationResolver(Transform avatarRoot, Object assetContainer)
        {
            _avatarRoot = avatarRoot;
            _assetContainer = assetContainer;
        }

        public void Reset()
        {
            _transformsDict.Clear();
            _originalTransforms.Clear();
        }

        public void Add(RuntimeAnimatorController controller, GameObject? rootOverride = null, IEnumerable<RewriteBinding>? rewriteBindings = null, IEnumerable<string>? ignoreBindings = null)
        {
            foreach (var animationClip in controller.animationClips)
            {
                var floatBindings = AnimationUtility.GetCurveBindings(animationClip);
                var objRefBindings = AnimationUtility.GetObjectReferenceCurveBindings(animationClip);

                foreach (var fBinding in floatBindings)
                    HandleEditorCurveBinding(fBinding);
                foreach (var orBinding in objRefBindings)
                    HandleEditorCurveBinding(orBinding);

                void HandleEditorCurveBinding(EditorCurveBinding binding)
                {
                    var path = binding.path;
                    if (_transformsDict.TryGetValue(path, out _))
                        return; // already in dictionary, skip

                    // ignore bindings
                    if (ignoreBindings is not null)
                    {
                        foreach (var ignoreBinding in ignoreBindings)
                        {
                            if (string.CompareOrdinal(path[0..ignoreBinding.Length], ignoreBinding) != 0)
                                continue;
                            return; // skip binding
                        }
                    }

                    // rewrite bindings
                    var newPath = path;
                    if (rewriteBindings is not null)
                    {
                        foreach (var rewriteBinding in rewriteBindings)
                        {
                            // this replaces the prefix
                            if (rewriteBinding.From.Length > newPath.Length)
                                continue;
                            if (string.CompareOrdinal(newPath[0..rewriteBinding.From.Length], rewriteBinding.From) != 0)
                                continue;
                            newPath = rewriteBinding.To + newPath[rewriteBinding.From.Length..];
                            if (DebugLogging)
                                Debug.Log($"Rewrite path: '{path}' > '{newPath}'.");
                        }
                    }

                    binding.path = newPath;

                    var obj = AnimationUtility.GetAnimatedObject(_avatarRoot.gameObject, binding);
                    if (obj is null && rootOverride != null)
                        obj = AnimationUtility.GetAnimatedObject(rootOverride, binding);
                    if (obj is null)
                    {
                        //if (DebugLogging) Debug.Log($"Path '{newPath}' is not valid, skipping.");
                        return; // binding not valid 
                    }

                    Transform transform = obj switch
                    {
                        Transform t => t,
                        GameObject go => go.transform,
                        Component c => c.transform,
                        _ => null
                    };

                    if (transform is null)
                        return;

                    _transformsDict.Add(path, transform);
                    _originalTransforms.Add(transform, new TransformData(transform.localPosition, transform.localRotation, transform.localScale));
                }
            }
        }

        // FIX ANIMATOR CONTROLLER SECTION
        private Dictionary<AnimationClip, AnimationClip> _modifiedClips = new(); // reuse for speed

        public RuntimeAnimatorController Fix(RuntimeAnimatorController controller, Transform? rootOverride = null)
        {
            _modifiedClips.Clear();

            if (DebugLogging) Debug.Log($"'{controller.name}' has {controller.animationClips.Length} animation clips.");
            foreach (var animationClip in controller.animationClips)
            {
                if (_modifiedClips.TryGetValue(animationClip, out _))
                    continue; // idk why this is possible

                var newAnimationClip = FixAnimationClip(animationClip, rootOverride);
                if (newAnimationClip is null)
                    continue;

                _modifiedClips.Add(animationClip, newAnimationClip);
            }

            if (_modifiedClips.Count == 0)
            {
                return controller;
            }

            // changes made so we need to commit them now
            var newController = (AnimatorController)controller;
            if (!_assetContainer.IsTemporaryAsset(controller))
            {
                var sw = new Stopwatch();
                sw.Start();
                newController = AnimationUtilities.DeepCloneAnimator(_assetContainer, controller);
                sw.Stop();
                Debug.Log($"Cloned controller '{controller.name}' in {sw.ElapsedMilliseconds}ms.");
            }
            else
                Debug.Log($"Modified controller '{controller.name}'.");

            foreach (var layer in newController.layers)
                HandleStateMachine(layer.stateMachine);

            void HandleStateMachine(AnimatorStateMachine stateMachine)
            {
                foreach (var state in stateMachine.states)
                {
                    if (state.state.motion is not AnimationClip ac)
                        continue;
                    if (!_modifiedClips.TryGetValue(ac, out var newAc))
                        continue;
                    newController.SetStateEffectiveMotion(state.state, newAc);
                }

                foreach (var subStateMachine in stateMachine.stateMachines)
                    HandleStateMachine(subStateMachine.stateMachine);
            }

            return newController;
        }


        // ANIMATION CLIP FIX SECTION
        private Dictionary<EditorCurveBinding, EditorCurveBinding> _acCurveChanges = new();
        private Dictionary<EditorCurveBinding, EditorCurveBinding> _acObjRefChanges = new();

        AnimationClip? FixAnimationClip(AnimationClip animationClip, Transform? rootOverride = null)
        {
            _acCurveChanges.Clear();
            _acObjRefChanges.Clear();

            var floatBindings = AnimationUtility.GetCurveBindings(animationClip);
            var objRefBindings = AnimationUtility.GetObjectReferenceCurveBindings(animationClip);

            bool clipModified = false;

            foreach (var binding in floatBindings)
            {
                var newBinding = ModifyEditorCurveBinding(binding);
                if (newBinding is null)
                    continue;
                _acCurveChanges.Add(binding, (EditorCurveBinding)newBinding);
                clipModified = true;
            }

            foreach (var binding in objRefBindings)
            {
                var newBinding = ModifyEditorCurveBinding(binding);
                if (newBinding is null)
                    continue;
                _acObjRefChanges.Add(binding, (EditorCurveBinding)newBinding);
                clipModified = true;
            }

            //if (DebugLogging) Debug.Log($"Binding check finished on anim '{animationClip.name}' with result '{clipModified}'.");

            EditorCurveBinding? ModifyEditorCurveBinding(EditorCurveBinding binding, bool objRef = false)
            {
                if (string.IsNullOrEmpty(binding.path))
                    return null;
                if (!_transformsDict.TryGetValue(binding.path, out var transform))
                {
                    //Debug.LogWarning($"Fix Vrcf Animations > Transform for '{binding.path}' was not found. Animator has been modified.");
                    return null; // something has gone horribly wrong
                }

                string newPath = rootOverride != null && transform.IsChildOf(rootOverride) ?
                    AnimationUtility.CalculateTransformPath(transform, rootOverride) :
                    AnimationUtility.CalculateTransformPath(transform, _avatarRoot);

                //Debug.Log($"Fix Vrcf Animations > Processing binding with path '{binding.path}' > '{newPath}'");
                if (string.CompareOrdinal(newPath, binding.path) == 0)
                    return null; // dont replace binding, it's fine.

                if (DebugLogging)
                    Debug.Log($"Processing binding with path '{binding.path}' > '{newPath}'.");

                /*var newBinding = (binding.isPPtrCurve, binding.isDiscreteCurve) switch
                {
                    (false, false) => EditorCurveBinding.FloatCurve(newPath, binding.type, binding.propertyName),
                    (true, true) => EditorCurveBinding.PPtrCurve(newPath, binding.type, binding.propertyName),
                    (false, true) => EditorCurveBinding.DiscreteCurve(newPath, binding.type, binding.propertyName),
                    _ => throw new NotImplementedException() // shouldnt be possible :clueless:
                };*/
                binding.path = newPath;
                return binding;
            }

            if (FixTransformCurves && !clipModified)
            {
                foreach (var binding in floatBindings)
                {
                    if (binding.type != typeof(Transform))
                        continue;
                    if (!_transformsDict.TryGetValue(binding.path, out var transform))
                    {
                        if (AnimationUtility.GetAnimatedObject(_avatarRoot.gameObject, binding) is not Transform t)
                            continue;
                        _transformsDict.Add(binding.path, t);
                        transform = t;
                    }
                    if (!_originalTransforms.TryGetValue(transform, out var originalTransform))
                        continue;

                    Vector4 offset = GetOffsetFromPropertyName(binding.propertyName[..^2], transform, originalTransform);
                    if (DebugLogging) Debug.Log($"Transform at '{binding.path} {binding.propertyName}' has offset of '({offset.x}, {offset.y}, {offset.z}, {offset.w})'");
                    if (offset == Vector4.zero)
                        continue;
                    clipModified = true;
                    if (DebugLogging) Debug.Log($"Non-zero offset found! Skipping to clip creation.");
                    break;
                }
            }

            if (clipModified)
            {
                var isTemporaryClip = _assetContainer.IsTemporaryAsset(animationClip);
                var newAnimationClip = isTemporaryClip ? animationClip : new AnimationClip();
                if (!isTemporaryClip)
                {
                    newAnimationClip.name = animationClip.name + " (ndmf)";
                    newAnimationClip.frameRate = animationClip.frameRate;
                    newAnimationClip.wrapMode = animationClip.wrapMode;
                }

                AnimationUtility.SetAnimationEvents(newAnimationClip, animationClip.events);

                foreach (var fBinding in floatBindings)
                {
                    EditorCurveBinding binding = fBinding;
                    if (_acCurveChanges.TryGetValue(fBinding, out var newBinding))
                        binding = newBinding;
                    var curve = AnimationUtility.GetEditorCurve(animationClip, fBinding);

                    if (FixTransformCurves)
                        FixTransformCurve();

                    void FixTransformCurve()
                    {
                        if (binding.type != typeof(Transform))
                            return;
                        if (!_transformsDict.TryGetValue(binding.path, out var transform))
                        {
                            if (AnimationUtility.GetAnimatedObject(_avatarRoot.gameObject, binding) is not Transform t)
                                return;
                            _transformsDict.Add(binding.path, t);
                            transform = t;
                        }
                        if (!_originalTransforms.TryGetValue(transform, out var originalTransform))
                            return;

                        Vector4 vectorOffset = GetOffsetFromPropertyName(binding.propertyName[..^2], transform, originalTransform);
                        if (vectorOffset == Vector4.zero)
                            return;

                        // get x y z or w value
                        float offset = binding.propertyName[^1] switch
                        {
                            'x' => vectorOffset.x,
                            'y' => vectorOffset.y,
                            'z' => vectorOffset.z,
                            'w' => vectorOffset.w,
                            _ => throw new NotImplementedException() // this shouldnt be possible but fuck me i might get rolled
                        };

                        if (DebugLogging) Debug.Log($"Applying offset to '{animationClip.name}:{binding.path}:{binding.propertyName}' offset: '{offset}'.");

                        // create new curve and duplicate keys to it :)
                        var newCurve = new AnimationCurve();
                        foreach (var key in curve.keys)
                        {
                            var newKey = new Keyframe(key.time, key.value + offset, key.inTangent, key.outTangent, key.inWeight, key.outWeight);
                            newKey.weightedMode = key.weightedMode;
                            newCurve.AddKey(newKey);
                        }

                        curve = newCurve;
                    }

                    AnimationUtility.SetEditorCurve(newAnimationClip, binding, curve);
                }

                foreach (var orBinding in objRefBindings)
                {
                    EditorCurveBinding binding = orBinding;
                    if (_acObjRefChanges.TryGetValue(orBinding, out var newBinding))
                        binding = newBinding;
                    var curve = AnimationUtility.GetObjectReferenceCurve(animationClip, orBinding);
                    AnimationUtility.SetObjectReferenceCurve(newAnimationClip, binding, curve);
                }

                if (!isTemporaryClip)
                    AssetDatabase.AddObjectToAsset(newAnimationClip, _assetContainer);
                animationClip = newAnimationClip;
            }

            return clipModified ? animationClip : null;
        }


        private const string _localPositionPropName = "m_LocalPosition";
        private const string _localRotationPropName = "m_LocalRotation";
        private const string _localEulerPropName = "localEulerAnglesRaw";
        private const string _localScalePropName = "m_LocalScale";

        Vector4 GetOffsetFromPropertyName(string propertyName, Transform t, TransformData ot)
        {
            return propertyName switch
            {
                _localPositionPropName => t.localPosition - ot.LocalPosition,
                _localRotationPropName => (t.localRotation * ot.InverseLocalRotation).GetVector(),
                _localEulerPropName => t.localEulerAngles - ot.LocalEulerAngles,
                _ => Vector4.zero
            };
        }
    }
}
#endif