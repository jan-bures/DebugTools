using UnityEngine;

// ReSharper disable once CheckNamespace
namespace DebugTools.Utils
{
    public class DebugShapesSphereMarker : MonoBehaviour
    {
        private DebugShapesDraw.Sphere? _sphere;

        private void Start()
        {
            _sphere = gameObject.AddComponent<DebugShapesDraw.Sphere>();
            _sphere.enabled = true;
            _sphere.color = Color.yellow;
            _sphere.sphereRadius = 0.5f;
            _sphere.sphereCenter = Vector3.zero;
        }

        public void SetRadius(float radius)
        {
            if (_sphere == null) return;
            _sphere.sphereRadius = radius;
        }


        public void SetCenter(Vector3 center)
        {
            if (_sphere == null) return;
            _sphere.UpdatePosition(center);
        }

        public void SetEnabled(bool value)
        {
            if (_sphere == null) return;
            _sphere.enabled = value;
        }
    }
}