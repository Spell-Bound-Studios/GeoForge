// Copyright 2025 Spellbound Studio Inc.

using UnityEngine;
using UnityEngine.UI;
using TMPro;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Spellbound.MarchingCubes {
    /// <summary>
    /// Controller for Sample One, Digging a Hole.
    /// Not recommended as a real controller/UI, because it is hard-couled to the Controller.
    /// </summary>
    public class SampleOneUi : MonoBehaviour {
        // Shape, what shape the terraforming command should take.
        [SerializeField] private TMP_Dropdown terraformingShapeDropdown;
        
        // Range, how far away terraforming may be commanded at.
        [SerializeField] private Slider terraformingRangeSlider;
        [SerializeField] private TextMeshProUGUI terraformingRangeValue;
        
        // Size, how large a region the terraforming command should affect.
        [SerializeField] private Slider terraformingSizeSlider;
        [SerializeField] private TextMeshProUGUI terraformingSizeValue;
        
        // Strength, how significant of a change a terraforming command should have on the underlying voxels.
        // Strength = 1 is a tiny change, barely perceptible, Strength = 255 is a complete change
        [SerializeField] private Slider terraformingStrengthSlider;
        [SerializeField] private TextMeshProUGUI terraformingStrengthValue;
        
        // Collisions, whether the controller should collide with the mesh or pass right through.
        [SerializeField] private Toggle useCollisionToggle;
        
        // Material that will be added when additive terraforming occurs.
        [SerializeField] private TMP_Dropdown addableMaterialDropdown;
        
        // Materials that will be removed when negative terraforming occurs.
        [SerializeField] private Toggle[] diggableMaterialToggles;
        
        // Semi-Transparent overlay to indicate when tab is pressed.
        [SerializeField] private GameObject tabOverlayObj;
        
        // Controller, this is what the UI controls.
        private SampleOneController _controller;

        /// <summary>
        /// Sets the controller.
        /// Subscribes to events.
        /// Initializes values.
        /// </summary>
        public void SetController(SampleOneController controller) {
            _controller = controller;

            terraformingShapeDropdown.onValueChanged.AddListener(HandleShapeDropdownChanged);
            HandleShapeDropdownChanged(terraformingShapeDropdown.value);
            
            terraformingRangeSlider.onValueChanged.AddListener(HandleRangeSliderChanged);
            HandleRangeSliderChanged(terraformingRangeSlider.value);
            
            terraformingSizeSlider.onValueChanged.AddListener(HandleSizeSliderChanged);
            HandleSizeSliderChanged(terraformingSizeSlider.value);
            
            terraformingStrengthSlider.onValueChanged.AddListener(HandleStrengthSliderChanged);
            HandleStrengthSliderChanged(terraformingStrengthSlider.value);
            
            useCollisionToggle.onValueChanged.AddListener(HandleCollisionToggle);
            HandleCollisionToggle(useCollisionToggle.isOn);
            
            addableMaterialDropdown.onValueChanged.AddListener(HandleAddableMaterialChanged);
            HandleAddableMaterialChanged(addableMaterialDropdown.value);

            for(var i = 0; i < diggableMaterialToggles.Length; i++) {
                var index = i;

                diggableMaterialToggles[i].onValueChanged.AddListener((value)
                        => HandleDiggableMateralsChanged(index, value));
                HandleDiggableMateralsChanged(index,  diggableMaterialToggles[i].isOn);
            }
            
            Cursor.lockState = CursorLockMode.Locked;
            tabOverlayObj.SetActive(false);
            
        }

        /// <summary>
        /// Other changes are event based, but listening for the Tab key being pressed must be polled on Update.
        /// </summary>
        private void Update() {
            PollTabKey();
        }

        /// <summary>
        /// Reads tab key input. Uses legacy input system if the regular input system is not installed.
        /// </summary>
        private void PollTabKey() {
#if ENABLE_INPUT_SYSTEM
            var keyboard = Keyboard.current;

            if (keyboard != null) {
                if (keyboard.tabKey.wasPressedThisFrame) {
                    HandleTabPressed();
                }
            }
#else
            if(Input.GetKeyDown(KeyCode.Tab)) {
                HandleTabPressed();
            }
#endif
        }

        // Subscribed methods to handle value changes from the UI.
        #region HandlerMethods

        private void HandleShapeDropdownChanged(int index) {
            _controller.SetProjectionShape(index);
        }

        private void HandleRangeSliderChanged(float value) {
            terraformingRangeValue.text = value.ToString();
            _controller.terraformRange = value;
        }
        
        private void HandleSizeSliderChanged(float value) {
            terraformingSizeValue.text = value.ToString();
            _controller.terraformSize = value;
        }
        
        private void HandleStrengthSliderChanged(float value) {
            terraformingStrengthValue.text = value.ToString();
            _controller.terraformStrength = (int)value;
            
        }
        
        private void HandleCollisionToggle(bool value) {
            _controller.collider.enabled = value;
        }

        private void HandleAddableMaterialChanged(int index) {
            _controller.addableMaterial = (byte)index;
        }

        private void HandleDiggableMateralsChanged(int index, bool isDiggable) {
            if (!isDiggable) {
                _controller.diggableMaterialList.Remove((byte)index);

                return;
            }

            if (!_controller.diggableMaterialList.Contains((byte)index)) {
                _controller.diggableMaterialList.Add((byte)index);
            }
        }

        private void HandleTabPressed() {
            if (_controller == null)
                return;

            if (_controller.freezeUpdate) {
                Cursor.lockState = CursorLockMode.Locked;
                tabOverlayObj.SetActive(false);
                _controller.freezeUpdate = false;

                return;
            }
            
            _controller.freezeUpdate = true;
            tabOverlayObj.SetActive(true);
            Cursor.lockState = CursorLockMode.None;
        }

        #endregion
        
    }
}