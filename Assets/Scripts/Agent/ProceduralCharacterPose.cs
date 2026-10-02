using System.Collections.Generic;
using UnityEngine;

namespace ControlRoom
{
    /// <summary>Applies every pose relative to the authored rest transforms, without accumulating offsets.</summary>
    internal sealed class ProceduralCharacterPose
    {
        private sealed class Joint
        {
            public Transform Transform;
            public Vector3 Position, Scale;
            public Quaternion Rotation;
        }

        private readonly Transform root;
        private readonly Vector3 rootPosition, rootScale;
        private readonly Quaternion rootRotation;
        private readonly Dictionary<string, Joint> joints = new Dictionary<string, Joint>();

        public ProceduralCharacterPose(Transform visual)
        {
            root = visual;
            rootPosition = root.localPosition;
            rootScale = root.localScale;
            rootRotation = root.localRotation;
            foreach (string name in new[] { "Left arm", "Right arm", "Left leg", "Right leg" })
            {
                Transform limb = visual.Find(name);
                if (limb == null) continue;
                joints.Add(name, new Joint { Transform = limb, Position = limb.localPosition,
                    Rotation = limb.localRotation, Scale = limb.localScale });
            }
        }

        public void Apply(Vector3 bodyOffset, Vector3 bodyAngles, float bodyHeight,
            Vector3 leftArm, Vector3 rightArm, Vector3 leftLeg, Vector3 rightLeg, float blend)
        {
            if (root == null) return;
            root.localPosition = Vector3.Lerp(root.localPosition, rootPosition + bodyOffset, blend);
            root.localRotation = Quaternion.Slerp(root.localRotation, rootRotation * Quaternion.Euler(bodyAngles), blend);
            root.localScale = Vector3.Lerp(root.localScale, Vector3.Scale(rootScale, new Vector3(1, bodyHeight, 1)), blend);
            SetJoint("Left arm", leftArm, blend);
            SetJoint("Right arm", rightArm, blend);
            SetJoint("Left leg", leftLeg, blend);
            SetJoint("Right leg", rightLeg, blend);
        }

        private void SetJoint(string name, Vector3 angles, float blend)
        {
            if (!joints.TryGetValue(name, out Joint joint) || joint.Transform == null) return;
            Quaternion relative = Quaternion.Euler(angles);
            // Cube limb origins are at their centers. Preserve the top pivot to keep shoulders and hips attached.
            Vector3 top = Vector3.up * joint.Scale.y * .5f;
            Vector3 position = joint.Position + joint.Rotation * (top - relative * top);
            joint.Transform.localPosition = Vector3.Lerp(joint.Transform.localPosition, position, blend);
            joint.Transform.localRotation = Quaternion.Slerp(joint.Transform.localRotation, joint.Rotation * relative, blend);
            joint.Transform.localScale = joint.Scale;
        }
    }
}
