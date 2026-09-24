using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class AIMob : MonoBehaviour
{
    public enum BehaviorMode
    {
        SEEK_ONLY,
        WANDER_ONLY,
        COMBINED
    }

    public enum State
    {
        WANDER,
        SEEK
    }

    [Header("Behavior Settings")]
    public BehaviorMode mode = BehaviorMode.COMBINED;

    [Header("Mob Properties")]
    public float maxSpeed = 5f;
    public float maxForce = 15f;
    public float detectionRadius = 15f;
    public float slowRadius = 5f; // Radio para frenar (Arrive)
    
    [Header("Wander Properties")]
    public float wanderDistance = 4f;
    public float wanderRadius = 2f;
    public float wanderJitter = 360f; // Grados por segundo
    
    [Header("Physics")]
    public float gravity = 20f;
    
    [Header("Debug Info")]
    public State currentState = State.WANDER;
    
    private Vector3 velocity = Vector3.zero;
    private Vector3 acceleration = Vector3.zero;
    private float currentWanderAngle;
    
    // References
    private CharacterController controller;
    private Transform player;

    void Start()
    {
        controller = GetComponent<CharacterController>();
        currentWanderAngle = Random.Range(0f, 360f);
        
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
        {
            player = playerObj.transform;
        }
    }

    void Update()
    {
        if (player == null)
        {
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null) player = playerObj.transform;
        }
        
        float distanceToPlayer = float.MaxValue;
        if (player != null)
        {
            distanceToPlayer = Vector3.Distance(transform.position, player.position);
        }

        // Selección de comportamiento según el modo
        if (mode == BehaviorMode.SEEK_ONLY)
        {
            currentState = State.SEEK;
            acceleration = (player != null) ? SeekArrive(player.position) : Vector3.zero;
        }
        else if (mode == BehaviorMode.WANDER_ONLY)
        {
            currentState = State.WANDER;
            acceleration = Wander(Time.deltaTime);
        }
        else if (mode == BehaviorMode.COMBINED)
        {
            if (distanceToPlayer <= detectionRadius)
            {
                currentState = State.SEEK;
                acceleration = SeekArrive(player.position);
            }
            else
            {
                currentState = State.WANDER;
                acceleration = Wander(Time.deltaTime);
            }
        }

        // Integración de Euler
        acceleration.y = 0;
        velocity += acceleration * Time.deltaTime;
        
        // Limitar velocidad horizontal
        Vector3 horizontalVelocity = new Vector3(velocity.x, 0, velocity.z);
        if (horizontalVelocity.sqrMagnitude > maxSpeed * maxSpeed)
        {
            horizontalVelocity = horizontalVelocity.normalized * maxSpeed;
            velocity.x = horizontalVelocity.x;
            velocity.z = horizontalVelocity.z;
        }

        // Terreno / Gravedad
        if (controller.isGrounded)
        {
            if (velocity.y < 0) 
            {
                velocity.y = -2f; 
            }
        }
        else
        {
            velocity.y -= gravity * Time.deltaTime;
        }

        // Rotación
        if (currentState == State.SEEK && player != null)
        {
            // Usar LookAt directo hacia el jugador cuando lo persigue
            Vector3 lookPos = player.position;
            lookPos.y = transform.position.y;
            transform.LookAt(lookPos);
            // Corrección de 180 grados porque el modelo 3D del duende está de espaldas
            transform.Rotate(0, 180, 0);
        }
        else if (horizontalVelocity.sqrMagnitude > 0.1f)
        {
            // Rotación suave estándar cuando está deambulando
            Quaternion baseRotation = Quaternion.LookRotation(horizontalVelocity);
            // Añadimos 180 grados al final para corregir el modelo
            Quaternion lookRotation = baseRotation * Quaternion.Euler(0, 180, 0);
            transform.rotation = Quaternion.Slerp(transform.rotation, lookRotation, Time.deltaTime * 5f);
        }

        controller.Move(velocity * Time.deltaTime);
    }

    private Vector3 Seek(Vector3 target)
    {
        Vector3 desired = (target - transform.position);
        desired.y = 0; 
        
        if (desired.sqrMagnitude > 0)
        {
            desired = desired.normalized * maxSpeed;
        }

        Vector3 steer = desired - new Vector3(velocity.x, 0, velocity.z);
        steer = Vector3.ClampMagnitude(steer, maxForce);
        
        return steer;
    }

    private Vector3 SeekArrive(Vector3 target)
    {
        Vector3 desired = (target - transform.position);
        desired.y = 0; 
        
        float distance = desired.magnitude;
        
        if (distance < 0.1f)
        {
            Vector3 stopSteer = -new Vector3(velocity.x, 0, velocity.z);
            return Vector3.ClampMagnitude(stopSteer, maxForce);
        }
        
        float speed = maxSpeed;
        if (distance < slowRadius)
        {
            speed = maxSpeed * (distance / slowRadius);
        }
        
        desired = desired.normalized * speed;
        
        Vector3 steer = desired - new Vector3(velocity.x, 0, velocity.z);
        steer = Vector3.ClampMagnitude(steer, maxForce);
        
        return steer;
    }

    private Vector3 Wander(float dt)
    {
        currentWanderAngle += Random.Range(-wanderJitter, wanderJitter) * dt;

        Vector3 forward = new Vector3(velocity.x, 0, velocity.z).normalized;
        if (forward.sqrMagnitude < 0.1f) forward = transform.forward;

        Vector3 circleCenter = transform.position + forward * wanderDistance;
        circleCenter.y = transform.position.y; 

        float angleRad = currentWanderAngle * Mathf.Deg2Rad;
        Vector3 displacement = new Vector3(Mathf.Cos(angleRad), 0, Mathf.Sin(angleRad)) * wanderRadius;

        Vector3 target = circleCenter + displacement;

        return Seek(target);
    }

    void OnControllerColliderHit(ControllerColliderHit hit)
    {
        // Si tocamos al jugador mientras lo estamos buscando/persiguiendo
        if (hit.gameObject.CompareTag("Player") && currentState == State.SEEK)
        {
            if (GameManager.instancia != null)
            {
                GameManager.instancia.GameOver();
            }
            else
            {
                Debug.Log("GOBLIN atrapó al jugador. Faltó el GameManager para lanzar GameOver.");
            }
        }
    }

    // Por si en el futuro se añade un trigger (ej: Collider isTrigger = true) para un hitbox más grande
    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player") && currentState == State.SEEK)
        {
            if (GameManager.instancia != null)
            {
                GameManager.instancia.GameOver();
            }
        }
    }

    private void OnDrawGizmos()
    {
        // Solo dibujar detección y frenado si pueden ocurrir
        if (mode == BehaviorMode.COMBINED || mode == BehaviorMode.SEEK_ONLY)
        {
            Gizmos.color = Color.white;
            if (mode == BehaviorMode.COMBINED) Gizmos.DrawWireSphere(transform.position, detectionRadius);
            
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, slowRadius);
        }
        
        if (Application.isPlaying)
        {
            if (currentState == State.WANDER)
            {
                Gizmos.color = Color.cyan;
                
                Vector3 forward = new Vector3(velocity.x, 0, velocity.z).normalized;
                if (forward.sqrMagnitude < 0.1f) forward = transform.forward;
                
                Vector3 circleCenter = transform.position + forward * wanderDistance;
                Gizmos.DrawWireSphere(circleCenter, wanderRadius);
                
                float angleRad = currentWanderAngle * Mathf.Deg2Rad;
                Vector3 displacement = new Vector3(Mathf.Cos(angleRad), 0, Mathf.Sin(angleRad)) * wanderRadius;
                Gizmos.DrawSphere(circleCenter + displacement, 0.5f);
            }
            else
            {
                if (player != null)
                {
                    Gizmos.color = Color.red;
                    Gizmos.DrawLine(transform.position, player.position);
                }
            }
        }
    }
}
