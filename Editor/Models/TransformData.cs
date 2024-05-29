using UnityEngine;

namespace GoobieTools.Editor.Models
{
    internal class TransformData
    {
        public Vector3 LocalPosition { get; }
        public Quaternion LocalRotation { get; }
        public Vector3 LocalScale { get; }

        public Quaternion InverseLocalRotation { get; }
        public Vector3 LocalEulerAngles { get; }

        public TransformData(Vector3 localPosition, Quaternion localRotation, Vector3 localScale)
        {
            LocalPosition = localPosition;
            LocalRotation = localRotation;
            LocalScale = localScale;

            InverseLocalRotation = Quaternion.Inverse(localRotation);
            LocalEulerAngles = localRotation.eulerAngles;
        }
    }
}
