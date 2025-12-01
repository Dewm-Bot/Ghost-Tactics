using UnityEngine;

namespace Controllers
{
    public class Projectile : MonoBehaviour
    {
        [SerializeField] private float destroyDelay = 0.25f;
        [SerializeField] private int damage = 5;
        [SerializeField] private Vector3 force = new Vector3(0, 0, 10);

        private Rigidbody _rb;

        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            if (_rb)
            {
                _rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            }
        }

        private void Start()
        {
            Destroy(gameObject, destroyDelay);
        }

        public void Launch(Vector3 velocity)
        {
            if (_rb)
            {
                _rb.linearVelocity = velocity;
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            HandleHit(other.gameObject);
        }

        private void OnCollisionEnter(Collision collision)
        {
            HandleHit(collision.gameObject);
        }

        private void HandleHit(GameObject other)
        {
            Target target = other.GetComponent<Target>();
            if (target != null)
            {
                target.DestroyTarget();
            }

            Rigidbody rb = other.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.AddForce(force, ForceMode.Impulse);
            }

            if (other.CompareTag("Enemy"))
            {
                EnemyHealthController healthController = other.GetComponent<EnemyHealthController>();
                if (healthController)
                {
                    healthController.TakeDamage(damage);
                }
            }

            Destroy(gameObject);
        }
    }
}