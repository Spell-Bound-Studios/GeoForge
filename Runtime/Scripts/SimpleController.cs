using UnityEngine;

namespace Spellbound.MarchingCubes {
    public class SimpleController : MonoBehaviour {
        
        public float moveSpeed = 5f;
        public float lookSpeed = 2f;

        float pitch = 0f; // up/down rotation

        void Awake() {
            Application.targetFrameRate = -1;
        }

        void Update() {
            HandleMovement();
            if (Input.GetKey(KeyCode.Z))
                RaycastDig();
        }
        

        void HandleMovement() {
            // --- Movement (WASD) ---
            float x = Input.GetAxis("Horizontal"); // A/D
            float z = Input.GetAxis("Vertical");   // W/S
            Vector3 move = transform.right * x + transform.forward * z;
            transform.position += move * moveSpeed * Time.deltaTime;

            // --- Mouse look ---
            float mouseX = Input.GetAxis("Mouse X") * lookSpeed;
            float mouseY = Input.GetAxis("Mouse Y") * lookSpeed;

            pitch -= mouseY;
            pitch = Mathf.Clamp(pitch, -80f, 80f);

            transform.localRotation = Quaternion.Euler(pitch, transform.localEulerAngles.y + mouseX, 0f);
        }

        void RaycastDig() {
            if (Physics.Raycast(
                    transform.position,
                    transform.forward,
                    out var hit,
                    float.MaxValue,
                    LayerMask.GetMask("Terrain"))) {
                SbTerrain.RemoveSphere(hit.point);
            }
        }

    }

}

