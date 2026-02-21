// Copyright 2025 Spellbound Studio Inc.

using UnityEngine;

namespace Spellbound.GeoForge {
    /// <summary>
    /// Simple Script to rotate a transform. For use in Orbiting Planets Sample Scene.
    /// </summary>
    public class SimpleRotation : MonoBehaviour {
        [SerializeField] private Vector3 rotationSpeed;

        private void FixedUpdate() {
            transform.Rotate(rotationSpeed * Time.deltaTime);
        }
    }
}
