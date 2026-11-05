using UnityEngine;
using System.Collections;

namespace AstronautPlayer
{
    public class AstronautPlayer : MonoBehaviour
    {

        private Animator anim;
        private CharacterController controller;

        public float speed = 600.0f;
        public float turnSpeed = 400.0f;
        private Vector3 moveDirection = Vector3.zero;
        public float gravity = 20.0f;

        // Tus variables
        public float fuerzaSalto = 8f; // (Recomendado subirlo un poco para CharacterController)
        private bool puedeSaltar = true;

        [Header("Control estilo Roblox")]
        [Tooltip("W/S/A/D se mueven respecto a hacia donde mira la cámara y el astronauta gira hacia donde camina. Desactívalo para el control antiguo (A/D giran el cuerpo y W avanza).")]
        public bool movimientoRelativoACamara = true;
        [Tooltip("Rapidez con la que el astronauta gira hacia su dirección de marcha (más alto = más rápido).")]
        public float suavizadoGiro = 14f;

        private AstronautThirdPersonCamera.AstronautThirdPersonCamera camara;

        void Start()
        {
            controller = GetComponent<CharacterController>();
            anim = gameObject.GetComponentInChildren<Animator>();

            camara = GetComponentInChildren<AstronautThirdPersonCamera.AstronautThirdPersonCamera>(true);
            if (camara == null) camara = FindFirstObjectByType<AstronautThirdPersonCamera.AstronautThirdPersonCamera>();
        }

        void Update()
        {
            float avance = Input.GetAxis("Vertical");
            float lateral = Input.GetAxis("Horizontal");

            // Con cámara, el movimiento es relativo a hacia donde mira; sin cámara, se usa el control antiguo.
            bool relativo = movimientoRelativoACamara && camara != null;
            Vector3 deseada = Vector3.zero;

            if (relativo)
            {
                Quaternion giroCamara = Quaternion.Euler(0f, camara.Yaw, 0f);
                deseada = giroCamara * Vector3.forward * avance + giroCamara * Vector3.right * lateral;
                if (deseada.sqrMagnitude > 1f) deseada.Normalize(); // la diagonal no es más rápida
            }

            // --- Animación de caminar ---
            if (anim != null)
            {
                bool caminando = relativo ? deseada.sqrMagnitude > 0.01f : Input.GetKey("w");
                anim.SetInteger("AnimationPar", caminando ? 1 : 0);
            }

            // --- Movimiento y salto (solo con los pies en el suelo, como antes) ---
            if (controller.isGrounded)
            {
                moveDirection = relativo ? deseada * speed : transform.forward * avance * speed;

                if (Input.GetKeyDown(KeyCode.Space))
                {
                    moveDirection.y = fuerzaSalto;
                }
            }

            // --- Rotación ---
            if (relativo)
            {
                // El astronauta se gira hacia donde camina (como en Roblox).
                if (deseada.sqrMagnitude > 0.01f)
                {
                    Quaternion objetivo = Quaternion.LookRotation(deseada, Vector3.up);
                    float t = 1f - Mathf.Exp(-suavizadoGiro * Time.deltaTime);
                    transform.rotation = Quaternion.Slerp(transform.rotation, objetivo, t);
                }
            }
            else
            {
                transform.Rotate(0, lateral * turnSpeed * Time.deltaTime, 0);
            }

            // La gravedad baja el valor Y poco a poco después del salto
            moveDirection.y -= gravity * Time.deltaTime;

            controller.Move(moveDirection * Time.deltaTime);
        }
    }
}
