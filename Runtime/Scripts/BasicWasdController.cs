// Copyright 2025 Spellbound Studio Inc.

using UnityEngine;

namespace Spellbound.MarchingCubes {
    /// <summary>
    /// Controller for Demo'ing MarchingCubes package.
    /// Not recommended as a real controller.
    /// </summary>
    public class BasicWasdController : MonoBehaviour {
        public float moveSpeed = 5f;
        public float lookSpeed = 2f;

        private float pitch = 0f;

        private void Update() {
            HandleMovement();

            if (Input.GetKey(KeyCode.Alpha1))
                RaycastTerraformRemove();
            else if (Input.GetKeyDown(KeyCode.Alpha2))
                RaycastTerraformAdd();
            else if (Input.GetKeyDown(KeyCode.Alpha3))
                RaycastTerraformRemoveAll();
        }

        private void HandleMovement() {
            // --- Movement (WASD) ---
            var x = Input.GetAxis("Horizontal"); // A/D
            var z = Input.GetAxis("Vertical");   // W/S
            var move = transform.right * x + transform.forward * z;
            transform.position += move * moveSpeed * Time.deltaTime;

            // --- Mouse look ---
            var mouseX = Input.GetAxis("Mouse X") * lookSpeed;
            var mouseY = Input.GetAxis("Mouse Y") * lookSpeed;

            pitch -= mouseY;
            pitch = Mathf.Clamp(pitch, -80f, 80f);

            transform.localRotation = Quaternion.Euler(pitch, transform.localEulerAngles.y + mouseX, 0f);
        }

        private void RaycastTerraformRemove() {
            if (Physics.Raycast(
                    transform.position,
                    transform.forward,
                    out var hit,
                    float.MaxValue,
                    LayerMask.GetMask("Terrain")))
                SbTerrain.RemoveSphere(hit.point);
        }

        private void RaycastTerraformAdd() {
            if (Physics.Raycast(
                    transform.position,
                    transform.forward,
                    out var hit,
                    float.MaxValue,
                    LayerMask.GetMask("Terrain")))
                SbTerrain.AddSphere(hit.point);
        }

        private void RaycastTerraformRemoveAll() {
            if (Physics.Raycast(
                    transform.position,
                    transform.forward,
                    out var hit,
                    float.MaxValue,
                    LayerMask.GetMask("Terrain")))
                SbTerrain.RemoveSphere(hit.point, 6f, byte.MaxValue);
        }
    }
}