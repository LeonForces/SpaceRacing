using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace Game.SpacePhysics
{
    [RequireComponent(typeof(Rigidbody))]
    public class SpaceGravityBody : MonoBehaviour
    {
        [Header("Влияние гравитации")]
        public float gravityScale = 1f;                  // Общий множитель для гравитации
        public int maxSourcesPerStep = 0;                // Лимит источников, учитываемых за тик
        public bool respectGravityShield = true;         // Учитывать ли активный щит
        [Range(0f, 1f)] public float shieldMultiplier = 0.2f; // Сила гравитации при щите

        [Header("Проигрыш")]
        [Tooltip("Проигрывать при столкновении именно с доминирующим источником.")]
        public bool loseOnCollisionWithDominantSource = true; // Проигрывать ли при ударе об доминирующий источник

        [Tooltip("Проигрывать при входе в горизонт событий доминирующей ЧД (если включено у источника).")]
        public bool loseOnEnterEventHorizon = true; // Проигрывать ли при входе в горизонт

        [Tooltip("Событие поражения (подвесь сюда UI/перезапуск и т.д.).")]
        public UnityEvent onLose;                       // Событие, вызываемое при поражении

        public SpaceGravitySource DominantSource { get; private set; } // Активный доминирующий источник

        private Rigidbody _rb;                          // Кешированный Rigidbody
        private SpaceshipController _ship;              // Связанный контроллер корабля
        private bool _isDead;                           // Флаг, что объект уже проиграл

        private void Awake()                           // Настраиваем физику и ссылки
        {
            _rb = GetComponent<Rigidbody>();
            _rb.useGravity = false;
            _rb.interpolation = RigidbodyInterpolation.Interpolate;
            _rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

            _ship = GetComponent<SpaceshipController>();
        }

        private void FixedUpdate()                      // Применяем гравитацию каждый физический тик
        {
            if (_isDead) return;

            Vector3 acc = ComputeTotalGravityAcceleration(out SpaceGravitySource dominant);
            DominantSource = dominant;

            if (acc.sqrMagnitude > 0f)
            {
                _rb.AddForce(acc, ForceMode.Acceleration);
            }

            if (loseOnEnterEventHorizon && DominantSource != null &&
                DominantSource.isBlackHole &&
                DominantSource.IsInsideEventHorizon(transform.position))
            {
                TriggerLose();
            }
        }

        private Vector3 ComputeTotalGravityAcceleration(out SpaceGravitySource dominant) // Суммируем ускорения от источников
        {
            dominant = null;

            List<SpaceGravitySource> list = SpaceGravitySource.Instances;
            if (list == null || list.Count == 0) return Vector3.zero;

            Vector3 pos = transform.position;

            var candidates = new List<SpaceGravitySource>(list.Count);
            for (int i = 0; i < list.Count; i++)
            {
                var s = list[i];
                if (!s) continue;
                float roi = s.radiusOfInfluence;
                if ((pos - s.transform.position).sqrMagnitude <= roi * roi)
                {
                    candidates.Add(s);
                }
            }

            if (maxSourcesPerStep > 0 && candidates.Count > maxSourcesPerStep)
            {
                candidates.Sort((a, b) =>
                {
                    float da = (pos - a.transform.position).sqrMagnitude;
                    float db = (pos - b.transform.position).sqrMagnitude;
                    return da.CompareTo(db);
                });
                candidates.RemoveRange(maxSourcesPerStep, candidates.Count - maxSourcesPerStep);
            }

            Vector3 sum = Vector3.zero;
            float maxAcc = 0f;

            for (int i = 0; i < candidates.Count; i++)
            {
                Vector3 a = candidates[i].GetAccelerationAtPoint(pos);
                sum += a;

                float mag = a.magnitude;
                if (mag > maxAcc)
                {
                    maxAcc = mag;
                    dominant = candidates[i];
                }
            }

            float mult = gravityScale;
            if (respectGravityShield && _ship != null && _ship.IsShieldActive())
            {
                mult *= Mathf.Clamp01(shieldMultiplier);
            }

            return sum * mult;
        }

        private void OnCollisionEnter(Collision collision) // Проверяем столкновения с источниками
        {
            if (_isDead || !loseOnCollisionWithDominantSource) return;

            var src = collision.collider.GetComponentInParent<SpaceGravitySource>();
            if (src != null && src == DominantSource)
            {
                TriggerLose();
            }
        }

        private void OnTriggerEnter(Collider other) // Реагируем на триггеры источников
        {
            if (_isDead || !loseOnCollisionWithDominantSource) return;

            var src = other.GetComponentInParent<SpaceGravitySource>();
            if (src != null && src == DominantSource)
            {
                TriggerLose();
            }
        }

        private void TriggerLose()                     // Переводим объект в состояние поражения
        {
            if (_isDead) return;
            _isDead = true;

            if (_ship != null) _ship.enabled = false;

            if (onLose != null) onLose.Invoke();
            else Destroy(gameObject);
        }
    }
}
