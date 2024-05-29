using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

#if UNITY_EDITOR
namespace GoobieTools
{
    [ExecuteInEditMode]
    public class BoneFixer : MonoBehaviour
    {
        // this little handy thing allows you to fix bone arrays when you export from blender but the mesh disappears!!
        // author: goobwabber (github.com/goobwabber)

        public bool Run = false;
        public SkinnedMeshRenderer MeshRenderer;
        public Transform Armature;
        public Transform TargetArmature;
        public SkinnedMeshRenderer OptionalOverrideMeshRenderer;

        private Dictionary<Transform, Transform> _transferDict = new();

        // Start is called before the first frame update
        void Start()
        {

        }

        // Update is called once per frame
        void Update()
        {
            if (!Run)
                return;
            Run = false;

            _transferDict.Clear();

            Transform target = Armature;

            void BuildBoneDict(Transform current, Transform target)
            {
                _transferDict.Add(current, target);
                foreach (Transform child in current)
                {
                    Transform targetChild = target.Find(child.name);
                    if (targetChild != null)
                        BuildBoneDict(child, targetChild);
                }
            }

            BuildBoneDict(Armature, TargetArmature);

            Transform[] bones = (Transform[])MeshRenderer.bones.Clone();
            string nobones = "";
            string yesbones = "";
            for (int i = 0; i < bones.Count(); i++)
            {
                var bone = bones[i];
                if (bone is null)
                    continue;
                if (!_transferDict.TryGetValue(bone, out Transform targetBone))
                {
                    nobones += $"\nCould not find bone '{AnimationUtility.CalculateTransformPath(bone, Armature)}'";
                    continue;
                }
                yesbones += $"\nRebound bone '{AnimationUtility.CalculateTransformPath(bone, Armature)}' to '{AnimationUtility.CalculateTransformPath(targetBone, TargetArmature)}'";
                bones[i] = targetBone;
            }
            MeshRenderer.bones = bones;

            if (!_transferDict.TryGetValue(MeshRenderer.rootBone, out Transform targetRootBone))
            {
                Debug.LogError("Root bone was not found!");
                return;
            }

            MeshRenderer.rootBone = targetRootBone;

            if (!string.IsNullOrEmpty(nobones))
                Debug.LogWarning("Bones not found:" + nobones);

            if (!string.IsNullOrEmpty(yesbones))
                Debug.Log("Bones were rebound:" + yesbones);
            else
                Debug.LogWarning("No bones were rebound.");
            Debug.Log("First bone: " + MeshRenderer.bones[0].GetHierarchyPath());

            if (OptionalOverrideMeshRenderer != null)
            {
                if (OptionalOverrideMeshRenderer.sharedMesh != MeshRenderer.sharedMesh)
                {
                    Debug.LogWarning("Not overriding mesh renderer, target mesh renderer and override mesh renderer do not have same shared mesh.");
                    return;
                }

                var targetMeshRenderer = OptionalOverrideMeshRenderer;

                targetMeshRenderer.bones = MeshRenderer.bones;
                targetMeshRenderer.rootBone = MeshRenderer.rootBone;
                targetMeshRenderer.sharedMesh = MeshRenderer.sharedMesh;
                // meow obsolete code
                return;

                // copy blendshapes
                var blendshapeCount = MeshRenderer.sharedMesh.blendShapeCount;
                for (int i = 0; i < blendshapeCount; i++)
                {
                    var value = targetMeshRenderer.GetBlendShapeWeight(i);
                    MeshRenderer.SetBlendShapeWeight(i, value);
                }

                // copy materials
                List<Material> materials = new List<Material>();
                targetMeshRenderer.GetMaterials(materials);
                MeshRenderer.SetMaterials(materials);

                // copy probe anchor
                MeshRenderer.probeAnchor = targetMeshRenderer.probeAnchor;

                // fuck the old renderer
            }
        }
    }
}
#endif
