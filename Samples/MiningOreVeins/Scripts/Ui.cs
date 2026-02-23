// Copyright 2025 Spellbound Studio Inc.

using UnityEngine;
using UnityEngine.UI;
using TMPro;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Spellbound.GeoForge.Sample2 {
    /// <summary>
    /// Controller for Sample Two, Mining Ore Veins.
    /// Not recommended as a real controller/UI, because it is hard-couled to the Controller.
    /// </summary>
    public class Ui : MonoBehaviour {

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
        
        // Semi-Transparent overlay to indicate when tab is pressed.
        [SerializeField] private GameObject tabOverlayObj;
        
        // Controller, this is what the UI controls.
        private Controller _controller;

        /// <summary>
        /// Sets the controller.
        /// Subscribes to events.
        /// Initializes values.
        /// </summary>
        public void SetController(Controller controller) {
            _controller = controller;
            
            terraformingRangeSlider.onValueChanged.AddListener(HandleRangeSliderChanged);
            HandleRangeSliderChanged(terraformingRangeSlider.value);
            
            terraformingSizeSlider.onValueChanged.AddListener(HandleSizeSliderChanged);
            HandleSizeSliderChanged(terraformingSizeSlider.value);
            
            terraformingStrengthSlider.onValueChanged.AddListener(HandleStrengthSliderChanged);
            HandleStrengthSliderChanged(terraformingStrengthSlider.value);
            
            useCollisionToggle.onValueChanged.AddListener(HandleCollisionToggle);
            HandleCollisionToggle(useCollisionToggle.isOn);
            
            
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
        
        private void HandleRangeSliderChanged(float value) {
            terraformingRangeValue.text = value.ToString("F2");
            _controller.terraformRange = value;
        }
        
        private void HandleSizeSliderChanged(float value) {
            terraformingSizeValue.text = value.ToString("F2");
            _controller.terraformSize = value;
        }
        
        private void HandleStrengthSliderChanged(float value) {
            terraformingStrengthValue.text = value.ToString("F2");
            _controller.terraformStrength = (int)value;
            
        }
        
        private void HandleCollisionToggle(bool value) {
            _controller.GetComponent<Collider>().enabled = value;
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