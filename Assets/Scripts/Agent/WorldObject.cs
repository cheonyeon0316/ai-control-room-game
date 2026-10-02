using System;
using UnityEngine;

namespace ControlRoom
{
    public sealed class WorldObject
    {
        public ObjectRecord Record { get; }
        public Transform Transform { get; }
        public Vector3 StandPoint { get; set; }
        public string RequiredCode { get; set; } = "";
        public bool IsOpen { get; private set; }
        public bool IsCollected { get; private set; }
        public Transform MovingPart { get; set; }
        public Action Opened { get; set; }
        private readonly Quaternion closedRotation;

        public WorldObject(ObjectRecord record, Transform transform, Vector3 standPoint)
        {
            Record = record;
            Transform = transform;
            StandPoint = standPoint;
            closedRotation = transform.localRotation;
        }

        public bool Unlock(string code)
        {
            if (IsOpen) return true;
            if (IsCollected || (!string.IsNullOrEmpty(RequiredCode) && code != RequiredCode)) return false;
            SetOpen();
            return true;
        }

        public void SetOpen()
        {
            if (IsOpen) return;
            IsOpen = true;
            Transform moving = MovingPart != null ? MovingPart : Transform;
            moving.localRotation = closedRotation * Quaternion.Euler(0, 82, 0);
            foreach (Collider collider in moving.GetComponentsInChildren<Collider>()) collider.enabled = false;
            Opened?.Invoke();
        }

        public void Collect()
        {
            IsCollected = true;
            Record.IsAvailable = false;
            Transform.gameObject.SetActive(false);
        }
    }
}
