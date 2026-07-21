// Copyright 2026 Spellbound Studio Inc.

using UnityEngine;

namespace Spellbound.GeoForge.Sample3 {
    /// <summary>
    /// Simple Script to rotate a transform. For use in Orbiting Planets Sample Scene Three.
    /// </summary>
    public class RotationScript : MonoBehaviour {
        [SerializeField] private Vector3 rotationSpeed;

        private void FixedUpdate() => transform.Rotate(rotationSpeed * Time.deltaTime);
    }
}