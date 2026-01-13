// Copyright 2025 Spellbound Studio Inc.

using System;
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
    public class TerraformController : MonoBehaviour {
        [SerializeField] float moveSpeed = 5f;
        [SerializeField] float lookSpeed = 2f;
        [SerializeField] public float terraformRange = 5f;
        [SerializeField] public float terraformSize = 1f;
        [SerializeField, Range(1, byte.MaxValue)] public int terraformStrength = byte.MaxValue;
        [SerializeField] private Color lowStrengthColor;
        [SerializeField] private Color highStrengthColor;
        [SerializeField] private Material projectionMaterial;

        private Action<RaycastHit, Vector3, float, int, List<byte>> _terraformRemove;
        private Action<RaycastHit, Vector3, float, int, byte> _terraformAdd;
        
        private GameObject _projectionObj;
        public enum TerraformShape {
            Sphere,
            Cube
        }
        
        private Rigidbody _rb;
        [HideInInspector] public Collider collider;
        [HideInInspector] public bool freezeUpdate = false;


        private float pitch = 0f;

        [SerializeField] public List<byte> diggableMaterialList = new();
        [SerializeField] public byte addableMaterial = 0;
        
        [SerializeField] private ControllerUI uiPrefab;
        
        private void Start() {
            _rb = GetComponent<Rigidbody>();
            collider = GetComponent<Collider>();
            

            if (_rb == null) {
                Debug.LogError("No Rigidbody component found!");
                enabled = false;

                return;
            }
            
            if (collider == null) {
                Debug.LogError("No Collider component found!");
                enabled = false;

                return;
            }
            
            _rb.freezeRotation = true;
            var ui = Instantiate(uiPrefab).GetComponent<ControllerUI>();
            ui.SetController(this);
        }

        private void Update() {
            HandleProjection();
            if (freezeUpdate)
                return;

            HandleMovement();

#if ENABLE_INPUT_SYSTEM
            var keyboard = Keyboard.current;

            if (keyboard != null) {
                if (keyboard.digit1Key.wasPressedThisFrame
                        && Physics.Raycast(
                        transform.position,
                        transform.forward,
                        out var hit,
                        terraformRange,
                        ~0))
                    _terraformRemove(hit, transform.forward, terraformSize, terraformStrength, diggableMaterialList);
                else if (keyboard.digit2Key.wasPressedThisFrame
                         && Physics.Raycast(
                        transform.position,
                        transform.forward,
                        out hit,
                        terraformRange,
                        ~0))
                    _terraformAdd(hit, transform.forward, terraformSize, terraformStrength, addableMaterial);
               
            }
#else
            if (Input.GetKeyDown(KeyCode.Alpha1)
                        && Physics.Raycast(
                        transform.position,
                        transform.forward,
                        out var hit,
                        terraformRange,
                        ~0))
                    _terraformRemove(hit, transform.forward, terraformSize, terraformStrength, diggableMaterialList);
                else if (Input.GetKeyDown(KeyCode.Alpha2
                         && Physics.Raycast(
                        transform.position,
                        transform.forward,
                        out hit,
                        terraformRange,
                        ~0))
                    _terraformAdd(hit, transform.forward, terraformSize, terraformStrength, addableMaterial);
#endif
        }

        public void SetProjectionShape(TerraformShape shape) {
            if (_projectionObj != null)
                Destroy(_projectionObj);
            switch (shape) {
                case TerraformShape.Sphere:
                    _projectionObj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    _terraformRemove = SbVoxel.RemoveSphere;
                    _terraformAdd = SbVoxel.AddSphere;
                    break;
                case TerraformShape.Cube:
                    _projectionObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    _terraformRemove = SbVoxel.RemoveCube;
                    _terraformAdd = SbVoxel.AddCube;
                    break;
            }

            Destroy(_projectionObj.transform.GetComponent<Collider>());
            _projectionObj.GetComponent<Renderer>().material = projectionMaterial;
        }

        public void SetProjectionShape(int index) => SetProjectionShape((TerraformShape)index);
   

        private void HandleProjection() {
            if (_projectionObj != null && Physics.Raycast(
                    transform.position,
                    transform.forward,
                    out var hit,
                    terraformRange,
                    ~0)) {
                _projectionObj.transform.position = hit.point;
                _projectionObj.transform.rotation = Quaternion.LookRotation(transform.forward, Vector3.up);
                _projectionObj.transform.localScale =  terraformSize * Vector3.one;
                _projectionObj.GetComponent<MeshRenderer>().material.color = 
                        Color.Lerp(lowStrengthColor, highStrengthColor, terraformStrength/255f);
                _projectionObj.SetActive(true);

                return;
            }
            _projectionObj.SetActive(false);
        }


        private void HandleMovement() {
            // --- Movement (WASD) ---
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
            _rb.MovePosition(_rb.position + move * (moveSpeed * Time.deltaTime));

            // --- Mouse look ---
#if ENABLE_INPUT_SYSTEM
            var mouseX = 0f;
            var mouseY = 0f;

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
    }
}