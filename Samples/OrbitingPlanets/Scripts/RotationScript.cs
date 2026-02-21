// Copyright 2025 Spellbound Studio Inc.

using UnityEngine;

namespace Spellbound.GeoForge.Sample3 {
    /// <summary>
    /// Simple Script to rotate a transform. For use in Orbiting Planets Sample Scene.
    /// </summary>
    public class RotationScript : MonoBehaviour {
        [SerializeField] private Vector3 rotationSpeed;

        private void FixedUpdate() {
            transform.Rotate(rotationSpeed * Time.deltaTime);
        }
    }
}
