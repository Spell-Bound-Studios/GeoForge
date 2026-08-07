// Copyright 2026 Spellbound Studio Inc.

using System.Collections.Generic;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Spellbound.GeoForge {
    /// <summary>
    /// Very basic controller for demonstrating some package functionality without downloading any samples. 
    /// Not recommended as a real controller.
    /// </summary>
    public class BasicWasdController : MonoBehaviour {
        // Movement fields
        [SerializeField] private float moveSpeed = 5f;
        [SerializeField] private float lookSpeed = 2f;
        private float _pitch;

        // Marching Cubes fields
        [SerializeField] private List<byte> conditionalDigList = new() { 0, 1, 2 };
        [SerializeField] private byte addableMaterial = 2;

        private void Start() => Cursor.lockState = CursorLockMode.Locked;

        private void Update() {
            HandleMovement();
            HandleTerraforming();
        }

        /// <summary>
        /// Reads inputs for Terraforming. Uses legacy input system if the regular input system is not installed.
        /// </summary>
        private void HandleTerraforming() {
#if ENABLE_INPUT_SYSTEM
            var keyboard = Keyboard.current;

            if (keyboard != null) {
                if (keyboard.digit1Key.wasPressedThisFrame)
                    RaycastTerraformRemove();
                else if (keyboard.digit2Key.wasPressedThisFrame)
                    RaycastTerraformAdd();
            }
#else
            if (Input.GetKeyDown(KeyCode.Alpha1))
                RaycastTerraformRemove();
            else if (Input.GetKeyDown(KeyCode.Alpha2))
                RaycastTerraformAdd();
#endif
        }

        /// <summary>
        /// Reads inputs for Movement. Uses legacy input system if the regular input system is not installed.
        /// </summary>
        private void HandleMovement() {
#if ENABLE_INPUT_SYSTEM
            var horizontal = 0f;
            var vertical = 0f;
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

#if ENABLE_INPUT_SYSTEM
            var mouseX = 0f;
            var mouseY = 0f;
            var mouse = Mouse.current;

            if (mouse != null) {
                mouseX = mouse.delta.x.ReadValue() * lookSpeed * 0.1f;
                mouseY = mouse.delta.y.ReadValue() * lookSpeed * 0.1f;
            }
#else
            var mouseX = Input.GetAxis("Mouse X") * lookSpeed;
            var mouseY = Input.GetAxis("Mouse Y") * lookSpeed;
#endif

            _pitch -= mouseY;
            _pitch = Mathf.Clamp(_pitch, -80f, 80f);

            transform.localRotation = Quaternion.Euler(_pitch, transform.localEulerAngles.y + mouseX, 0f);
        }

        /// <summary>
        /// Raycasts and Terraforms Remove at the hit location.
        /// </summary>
        private void RaycastTerraformRemove() {
            if (Physics.Raycast(
                    transform.position,
                    transform.forward,
                    out var hit,
                    float.MaxValue,
                    ~0)) {
                var iVolume = hit.collider.transform.GetComponentInParent<IGeoVolume>();

                if (iVolume != null) {
                    var success = GeoForgeCommands.RemoveSphere(
                        iVolume,
                        hit.point,   // worldPosition - sphere centers here
                        radius: 2.5f, // world units, same convention as GeoForgeStatic.RemoveSphere's radius
                        delta: 255     // magnitude to subtract - RemoveSphere negates this internally
                    );
                }
            }
        }

        /// <summary>
        /// Raycasts and Terraforms Add at the hit location.
        /// </summary>
        private void RaycastTerraformAdd() {
            /*if (Physics.Raycast(
                    transform.position,
                    transform.forward,
                    out var hit,
                    float.MaxValue,
                    ~0)) {
                var iVolume = hit.collider.transform.GetComponentInParent<IGeoVolume>();

                if (iVolume != null) {
                    var success = GeoForgeCommands.AddSphere(
                        iVolume,
                        hit.point,   // worldPosition - sphere centers here
                        radius: 2.5f, // world units, same convention as GeoForgeStatic.RemoveSphere's radius
                        delta: 50     // magnitude to subtract - RemoveSphere negates this internally
                    );

                    if (!success) {
                        // Rejected - check console for the warning (pool capacity or, for a non-finite
                        // volume, a missing chunk in range). Nothing was applied.
                    }
                }
            }
            */
        }
    }
}