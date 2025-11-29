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
        }

        private void Start()
        {
            Destroy(gameObject, destroyDelay);
        }

        public void Launch(Vector3 velocity)
        {
            if (_rb)
            {
#if UNITY_6000_0_OR_NEWER
                _rb.linearVelocity = velocity;
#else
                _rb.velocity = velocity;
#endif
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            Target target = other.gameObject.GetComponent<Target>();
            if (target != null)
            {
                target.DestroyTarget();
            }

            Rigidbody rb = other.gameObject.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.AddForce(force, ForceMode.Impulse);
            }

            if (other.gameObject.CompareTag("Enemy"))
            {
                EnemyHealthController healthController = other.gameObject.GetComponent<EnemyHealthController>();
                if (healthController != null)
                {
                    healthController.TakeDamage(damage);
                }
            }

            Destroy(gameObject);
        }
    }
}