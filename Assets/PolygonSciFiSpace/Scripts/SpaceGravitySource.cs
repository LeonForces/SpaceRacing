using System.Collections.Generic;
using UnityEngine;

namespace Game.SpacePhysics
{
    public class SpaceGravitySource : MonoBehaviour
    {
        public static readonly List<SpaceGravitySource> Instances = new();

        [Header("Физика")]
        [Tooltip("Гравитационная «постоянная». Масштабируй под размеры сцены.")]
        public float gravitationalConstant = 0.01f; // Масштаб силы притяжения

        [Tooltip("Масса источника (M)")]
        public float sourceMass = 1e6f; // Масса, участвующая в формуле Ньютона

        [Tooltip("Радиус влияния. За пределами — не считаем (оптимизация).")]
        public float radiusOfInfluence = 5000f; // Максимальная дистанция действия

        [Tooltip("Минимальная эффективная дистанция до центра (анти-сингулярность).")]
        public float minDistance = 25f; // Нижняя граница при расчёте ускорения

        [Tooltip("Plummer-softening (eps). 0 — выключено.")]
        public float softening = 0f; // Дополнительное сглаживание потенциала

        [Tooltip("Лимит ускорения (м/с²), чтобы не «стреляло». 0 — без лимита.")]
        public float maxAcceleration = 150f; // Верхний предел ускорения

        [Tooltip("Кривая спада внутри ROI (t = r / ROI).")]
        public AnimationCurve falloff = AnimationCurve.Linear(0, 1, 1, 1); // Настройка ослабления по расстоянию

        [Header("Чёрная дыра (опционально)")]
        public bool isBlackHole = false; // Флаг особого поведения как чёрной дыры

        [Tooltip("Радиус горизонта событий (радиус поглощения).")]
        public float eventHorizonRadius = 100f; // Радиус уничтожения объектов

        [Tooltip("Уничтожать объект при входе в горизонт.")]
        public bool destroyOnHorizon = false; // Уничтожать ли тела при пересечении

        [Header("Визуализация (Gizmos)")]
        public Color influenceColor = new(1f, 0.85f, 0.2f, 0.6f); // Цвет сферы влияния
        public Color horizonColor = new(0f, 0f, 0f, 0.9f); // Цвет горизонта событий
        public bool drawGizmos = true; // Рисовать ли Gizmos

        private void OnEnable()
        {
            // Подписываемся в глобальном реестре, чтобы тела могли учитывать этот источник
            if (!Instances.Contains(this)) Instances.Add(this);
        }

        private void OnDisable()
        {
            // При выключении/уничтожении удаляемся из списка, чтобы не оставлять висячих ссылок
            Instances.Remove(this);
        }

        /// Возвращает вектор УСКОРЕНИЯ в точке worldPos.
        public Vector3 GetAccelerationAtPoint(Vector3 worldPos)
        {
            Vector3 toCenter = transform.position - worldPos; // Направление силы — к центру источника
            float r = toCenter.magnitude; // Текущее расстояние до центра
            if (r <= 0f || r > radiusOfInfluence) return Vector3.zero; // Ноль внутри точки и вне радиуса влияния

            // Анти-сингулярность: не позволяем r стремиться к нулю
            float rEff = Mathf.Max(minDistance, r);
            if (softening > 0f)
            {
                // Plummer-softening: увеличиваем эффективную дистанцию по формуле sqrt(r^2 + eps^2)
                rEff = Mathf.Sqrt((rEff * rEff) + (softening * softening));
            }

            // Базовая формула ускорения: a = G * M / r^2
            float aMag = gravitationalConstant * sourceMass / (rEff * rEff);

            // Плавный спад внутри ROI позволяет моделировать ослабление — берём t = r / ROI
            float t = Mathf.Clamp01(r / Mathf.Max(1e-3f, radiusOfInfluence));
            aMag *= Mathf.Max(0f, falloff.Evaluate(t));

            // Кэп ускорения нужен для стабильности: не допускаем слишком резких скачков
            if (maxAcceleration > 0f) aMag = Mathf.Min(aMag, maxAcceleration);

            // Возвращаем вектор ускорения по нормализованному направлению к центру
            return toCenter.normalized * aMag;
        }

        public bool IsInsideEventHorizon(Vector3 worldPos)
        {
            if (!isBlackHole) return false; // Для обычных источников горизонт не проверяем
            float r = Vector3.Distance(worldPos, transform.position);
            return r <= Mathf.Max(0f, eventHorizonRadius); // true, если объект пересёк границу поглощения
        }

        private void OnDrawGizmos()
        {
            if (!drawGizmos) return; // Позволяет отключить визуализацию в сцене

            Gizmos.color = influenceColor;
            Gizmos.DrawWireSphere(transform.position, Mathf.Max(0f, radiusOfInfluence)); // Показываем радиус влияния

            if (isBlackHole)
            {
                Gizmos.color = horizonColor;
                Gizmos.DrawWireSphere(transform.position, Mathf.Max(0f, eventHorizonRadius)); // Доп. сфера горизонта
            }
        }
    }
}
