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

        void Start()
        {
            controller = GetComponent<CharacterController>();
            anim = gameObject.GetComponentInChildren<Animator>();
        }

        void Update()
        {
            // --- TU LÓGICA DE ANIMACIÓN INTACTA ---
            if (Input.GetKey("w"))
            {
                anim.SetInteger("AnimationPar", 1);
            }
            else
            {
                anim.SetInteger("AnimationPar", 0);
            }

            // --- AQUÍ ES DONDE SE AGREGA EL SALTO ---
            if (controller.isGrounded)
            {
                // Tu cálculo de movimiento original
                moveDirection = transform.forward * Input.GetAxis("Vertical") * speed;

                // AGREGADO: Si presiona espacio, cambiamos la dirección Y hacia arriba
                if (Input.GetKeyDown(KeyCode.Space))
                {
                    moveDirection.y = fuerzaSalto;
                }
            }

            // --- TU LÓGICA DE ROTACIÓN Y GRAVEDAD INTACTA ---
            float turn = Input.GetAxis("Horizontal");
            transform.Rotate(0, turn * turnSpeed * Time.deltaTime, 0);

            // La gravedad baja el valor Y poco a poco después del salto
            moveDirection.y -= gravity * Time.deltaTime;

            controller.Move(moveDirection * Time.deltaTime);
        }
    }
}