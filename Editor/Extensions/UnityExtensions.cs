using UnityEditor;
using UnityEngine;

namespace GoobieTools.Editor.Extensions
{
    internal static class UnityExtensions
    {
        public static Vector4 GetVector(this Quaternion q) 
        {
            return new Vector4(q.x, q.y, q.z, q.w);
        }

        public static bool IsTemporaryAsset(this Object assetContainer, Object obj)
        {
            return !EditorUtility.IsPersistent(obj) || AssetDatabase.GetAssetPath(obj) == AssetDatabase.GetAssetPath(assetContainer);
        }
    }
}
