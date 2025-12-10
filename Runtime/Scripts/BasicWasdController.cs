// Copyright 2025 Spellbound Studio Inc.

using System.Collections.Generic;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Spellbound.MarchingCubes {
    /// <summary>
    /// Controller for Demo'ing MarchingCubes package.
    /// Not recommended as a real controller.
    /// </summary>
    public class BasicWasdController : MonoBehaviour {
        public float moveSpeed = 5f;
        public float lookSpeed = 2f;

        private float pitch = 0f;

        [SerializeField] private List<byte> conditionalDigList = new();
        [SerializeField] private byte addableMaterial = 3;

        private void Start() => Cursor.lockState = CursorLockMode.Locked;

        private void Update() {
            HandleMovement();

#if ENABLE_INPUT_SYSTEM
            var keyboard = Keyboard.current;
            if (keyboard != null) {
                if (keyboard.digit1Key.isPressed)
                    RaycastTerraformRemove();
                else if (keyboard.digit2Key.wasPressedThisFrame)
                    RaycastTerraformAdd();
                else if (keyboard.digit3Key.wasPressedThisFrame)
                    RaycastTerraformRemoveAll();
            }
#else
            if (Input.GetKey(KeyCode.Alpha1))
                RaycastTerraformRemove();
            else if (Input.GetKeyDown(KeyCode.Alpha2))
                RaycastTerraformAdd();
            else if (Input.GetKeyDown(KeyCode.Alpha3))
                RaycastTerraformRemoveAll();
#endif
        }

        private void HandleMovement() {
            // --- Movement (WASD) ---
#if ENABLE_INPUT_SYSTEM
            
            float horizontal = 0f;
            float vertical = 0f;
    
            var keyboard = Keyboard.current;
            if (keyboard != null) {
                if (keyboard.dKey.isPressed) horizontal += 1f;
                if (keyboard.aKey.isPressed) horizontal -= 1f;
                if (keyboard.wKey.isPressed) vertical += 1f;
                if (keyboard.sKey.isPressed) vertical -= 1f;
            }
#else
            float horizontal = Input.GetAxis("Horizontal"); // A/D
            float vertical = Input.GetAxis("Vertical");     // W/S
#endif
            
            var move = transform.right * horizontal + transform.forward * vertical;
            transform.position += move * (moveSpeed * Time.deltaTime);

            // --- Mouse look ---
#if ENABLE_INPUT_SYSTEM
            float mouseX = 0f;
            float mouseY = 0f;
            
            var mouse = Mouse.current;
            if (mouse != null) {
                mouseX = mouse.delta.x.ReadValue() * lookSpeed * 0.1f; // Scale down delta
                mouseY = mouse.delta.y.ReadValue() * lookSpeed * 0.1f;
            }
#else
            var mouseX = Input.GetAxis("Mouse X") * lookSpeed;
            var mouseY = Input.GetAxis("Mouse Y") * lookSpeed;
#endif

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
                    ~0))
                SbVoxel.RemoveSphere(hit.point);
        }

        private void RaycastTerraformAdd() {
            if (Physics.Raycast(
                    transform.position,
                    transform.forward,
                    out var hit,
                    float.MaxValue,
                    ~0))
                SbVoxel.AddSphere(hit, 3, byte.MaxValue, addableMaterial);
        }

        private void RaycastTerraformRemoveAll() {
            if (Physics.Raycast(
                    transform.position,
                    transform.forward,
                    out var hit,
                    float.MaxValue,
                    ~0))
                SbVoxel.RemoveSphere(hit.point, 3, byte.MaxValue, conditionalDigList);
        }
    }
}