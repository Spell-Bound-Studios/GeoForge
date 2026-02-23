// Copyright 2025 Spellbound Studio Inc.

using System;
using System.Collections.Generic;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Spellbound.GeoForge.Sample2 {
    /// <summary>
    /// Controller for Sample One, Digging a Hole.
    /// Not recommended as a real controller.
    /// Fields and settings are controlled from the UI, which is created on Start(), and why some fields are public.
    /// </summary>
    public class Controller : MonoBehaviour {
        
        // Movement fields
        [SerializeField] private float moveSpeed = 5f;
        [SerializeField] private float lookSpeed = 2f;
        private float _pitch;
        
        // Marching Cubes fields
        [SerializeField] public float terraformRange = 5f;
        [SerializeField] public float terraformSize = 1f;
        [SerializeField, Range(1, byte.MaxValue)] public int terraformStrength = byte.MaxValue;
        private readonly List<byte> _diggableMaterialList = new() { 0, 1, 2, 3 };
        
        // Config
        [SerializeField] private Color lowStrengthColor;
        [SerializeField] private Color highStrengthColor;
        [SerializeField] private Material projectionMaterial;
        private GameObject _projectionObj;
        private Rigidbody _rb;
        [HideInInspector] public Collider playerCollider;
        [HideInInspector] public bool freezeUpdate;
        [SerializeField] private Ui uiPrefab;
        
        // Effects
        [SerializeField] private AudioClip miningAudioClip;
        [SerializeField] private ParticleSystem miningParticle;
        
        // Local enum for the shape of the terraforming commands
        private enum TerraformShape {
            Sphere,
            Cube
        }
        
        /// <summary>
        /// Start method initializes the controller, and creates and initializes it's UI. 
        /// </summary>
        private void Start() {
            _rb = GetComponent<Rigidbody>();
            playerCollider = GetComponent<Collider>();
            

            if (_rb == null) {
                Debug.LogError("No Rigidbody component found!");
                enabled = false;

                return;
            }
            
            if (playerCollider == null) {
                Debug.LogError("No Collider component found!");
                enabled = false;

                return;
            }
            
            _rb.freezeRotation = true;
            var ui = Instantiate(uiPrefab).GetComponent<Ui>();
            ui.SetController(this);
        }

        /// <summary>
        /// freezeUpdate is true when utilizing the UI.
        /// Projection continues to be updated to reflect whats being tweaked in the UI.
        /// Movement and Terraforming are disabled while utilizing the UI.
        /// </summary>
        private void Update() {
            HandleProjection();
            
            if (freezeUpdate)
                return;
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
                if (keyboard.digit1Key.wasPressedThisFrame
                    && Physics.Raycast(
                        transform.position,
                        transform.forward,
                        out var hit,
                        terraformRange,
                        ~0)) {
                    GeoForgeStatic.RemoveSphereAll(hit, terraformSize, terraformStrength, _diggableMaterialList);
                    AudioSource.PlayClipAtPoint(miningAudioClip, hit.point);
                    var direction = Vector3.Slerp(-transform.forward, hit.normal, 0.5f);
                    var geoVolume = hit.collider.gameObject.GetComponentInParent<IVolume>();

                    if (geoVolume != null) {
                        var ps = Instantiate(miningParticle, hit.point, Quaternion.LookRotation(direction, Vector3.up));
                        Destroy(ps.gameObject, ps.main.duration);
                    }
                    
                }
            }
#else
            if (Input.GetKeyDown(KeyCode.Alpha1)
                        && Physics.Raycast(
                        transform.position,
                        transform.forward,
                        out var hit,
                        terraformRange,
                        ~0)){
                _terraformRemove(pos, rot.eulerAngles, terraformSize, terraformStrength, _diggableMaterialList, snapToGrid);
            }
#endif
        }

        /// <summary>
        /// Method for setting or changing the shape of the terraforming projection and commands.
        /// </summary>
        private void SetProjectionShape(TerraformShape shape) {
            if (_projectionObj != null)
                Destroy(_projectionObj);
            switch (shape) {
                case TerraformShape.Sphere:
                    _projectionObj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    break;
                case TerraformShape.Cube:
                    _projectionObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    break;
            }

            Destroy(_projectionObj.transform.GetComponent<Collider>());
            _projectionObj.GetComponent<Renderer>().material = projectionMaterial;
        }

        public void SetProjectionShape(int index) => SetProjectionShape((TerraformShape)index);
   
        /// <summary>
        /// Updates a semi-transparent projection of what terraforming fields are set to.
        /// </summary>
        private void HandleProjection() {
            if (_projectionObj != null && Physics.Raycast(
                    transform.position,
                    transform.forward,
                    out var hit,
                    terraformRange,
                    ~0)) {
                var volume = hit.transform.GetComponentInParent<IVolume>();

                if (volume == null) {
                    _projectionObj.SetActive(false);

                    return;
                }
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
            _rb.MovePosition(_rb.position + move * (moveSpeed * Time.deltaTime));
            
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
    }
}