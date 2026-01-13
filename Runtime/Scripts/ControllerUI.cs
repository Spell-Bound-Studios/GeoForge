// Copyright 2025 Spellbound Studio Inc.

using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Spellbound.MarchingCubes {
    /// <summary>
    /// UI for Demo'ing MarchingCubes package.
    /// Not recommended as a real controller.
    /// </summary>
    public class ControllerUI : MonoBehaviour {
        [SerializeField] private TMP_Dropdown terraformingShapeDropdown;
        
        [SerializeField] private Slider terraformingRangeSlider;
        [SerializeField] private TextMeshProUGUI terraformingRangeValue;
        
        [SerializeField] private Slider terraformingSizeSlider;
        [SerializeField] private TextMeshProUGUI terraformingSizeValue;
        
        [SerializeField] private Slider terraformingStrengthSlider;
        [SerializeField] private TextMeshProUGUI terraformingStrengthValue;
        
        [SerializeField] private Toggle useCollisionToggle;
        [SerializeField] private TMP_Dropdown addableMaterialDropdown;
        
        [SerializeField] private Toggle[] diggableMaterialToggles;
        
        [SerializeField] private GameObject tabOverlayObj;
        private TerraformController _controller;

        public void SetController(TerraformController controller) {
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

        private void Update() {
            PollTabKey();
        }

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
    }
}