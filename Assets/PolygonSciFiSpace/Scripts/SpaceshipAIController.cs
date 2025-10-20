using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Простенький автопилот: ведёт корабль по точкам, заполняя ControlState.
/// </summary>
[RequireComponent(typeof(SpaceshipController))]
public class SpaceshipAIController : MonoBehaviour
{
    [Header("Маршрут")]
    [Tooltip("Чекпоинты, которые бот проходит по порядку.")]
    // Массив трансформов, определяющий траекторию полёта AI
    [SerializeField] private Transform[] waypoints = System.Array.Empty<Transform>(); // Список чекпоинтов, формирует трассу
    [Tooltip("Радиус, в котором точка считается достигнутой.")]
    // Радиус вокруг waypoint, попадание внутрь которого засчитывает прохождение точки
    [SerializeField] private float waypointRadius = 40f; // Считаем точку достигнутой, когда корабль входит в эту сферу
    [Tooltip("Зацикливать обход точек.")]
    // Разрешить ли вечное патрулирование по кругу
    [SerializeField] private bool loopWaypoints = true; // true — после последнего чекпоинта возвращаемся к первому

    [Header("Поведение")]
    [Tooltip("Коэффициент поворота: больше значение — агрессивнее рули.")]
    // Насколько агрессивно AI реагирует рулём на отклонение от направления на цель
    [SerializeField] private float steerGain = 3f; // Множитель поворота: больше => быстрее крутит yaw/pitch
    [Tooltip("Наклон корпуса в повороте (ролл).")]
    // Дополнительный коэффициент для крена корпуса, чтобы корабль красиво ложился в вираж
    [SerializeField] private float rollAlignGain = 2f; // Насколько сильно AI наклоняет корпус по ходу поворота
    [Tooltip("На каком расстоянии выкручивать газ на максимум.")]
    // Дистанция, с которой thrust достигает 100%; ближе — тяга плавно снижается до minThrottle
    [SerializeField] private float throttleDistance = 250f; // На этой дистанции и дальше газ выкручивается на 100%
    [Tooltip("Минимальный газ, чтобы бот не глох.")]
    [Range(0f, 1f)]
    [SerializeField] private float minThrottle = 0.35f;

    [Header("Завершение трассы")]
    [Tooltip("Считать, что бот завершил маршрут при достижении последней точки.")]
    // При true завершение маршрута отправляет событие в GameOverManager (например, игрок проиграл гонку)
    [SerializeField] private bool loseOnFinish = true; // Если true — по завершении маршрута показываем экран поражения
    // Ссылка на UI-менеджер, отвечающий за экран поражения
    [SerializeField] private GameOverManager loseManager;
    // Текст, который выведется на экране поражения после финиша
    [SerializeField] private string loseMessage;

    [Tooltip("Событие, вызываемое при завершении маршрута (независимо от loseOnFinish).")]
    // UnityEvent предоставляет возможность подписывать произвольные реакции из инспектора
    [SerializeField] private UnityEngine.Events.UnityEvent onRouteFinished;

    private SpaceshipController ship; // Ссылка на физический контроллер корабля
    private int currentWaypointIndex; // Индекс текущего чекпоинта в массиве
    private bool finishTriggered; // Нужно, чтобы не вызывать событие завершения несколько раз в нецикличном режиме

    private void Awake()
    {
        ship = GetComponent<SpaceshipController>(); // Кешируем контроллер для минимизации GetComponent в рантайме
        ship.isPlayerControlled = false; // Переключаем SpaceshipController в режим внешнего управления
    }

    private void OnValidate()
    {
        if (waypoints == null) return; // Нечего валидировать, если массив не задан
        for (int i = 0; i < waypoints.Length; i++)
        {
            if (waypoints[i] == null) continue; // Здесь можно подсвечивать ошибки; пока оставлено без логики
        }
    }

    private void FixedUpdate()
    {
        if (ship == null || waypoints == null || waypoints.Length == 0)
        {
            return; // Без корабля или waypoint управление не имеет смысла
        }

        Transform target = waypoints[currentWaypointIndex]; // Текущая целевая точка
        if (target == null)
        {
            AdvanceWaypoint(); // Пропускаем пустой чекпоинт, чтобы не зависнуть
            return; // Подождём следующий кадр, чтобы работать уже с новым индексом
        }

        Vector3 toTarget = target.position - transform.position; // Вектор до следующего чекпоинта
        float distance = toTarget.magnitude; // Дистанция до цели для контроля газа и фиксации достижения

        if (distance <= waypointRadius)
        {
            AdvanceWaypoint(); // Переходим к следующей точке при входе в радиус
            return; // Прерываем кадр, чтобы начать маневрировать уже к новой цели
        }

        Vector3 desiredDir = toTarget.normalized; // Желаемое направление полёта в мировых координатах
        Vector3 localDir = transform.InverseTransformDirection(desiredDir); // То же направление, но в локальных осях корабля

        var control = ship.CurrentControl; // Захватываем последнюю команду, чтобы только обновить необходимые поля

        // Чем дальше цель, тем ближе газ к 100%. Ближе порога — плавно снижаем тягу до minThrottle
        float throttleBlend = Mathf.Clamp01(distance / Mathf.Max(1f, throttleDistance));
        control.thrust = Mathf.Lerp(minThrottle, 1f, throttleBlend);

        // Повороты: localDir.y отвечает за наклон вверх/вниз, .x — влево/вправо. Знаки подобраны под SpaceshipController
        control.pitch = Mathf.Clamp(-localDir.y * steerGain, -1f, 1f);
        control.yaw = Mathf.Clamp(localDir.x * steerGain, -1f, 1f);
        control.roll = Mathf.Clamp(-localDir.x * rollAlignGain, -1f, 1f);

        control.strafeHorizontal = 0f; // Бот не использует стрейфы
        control.strafeVertical = 0f;
        control.boost = false; // И спец-способности тоже
        control.brake = false;
        control.shield = false;

        ship.ApplyControl(control); // Передаём собранное состояние в SpaceshipController
    }

    private void AdvanceWaypoint()
    {
        if (waypoints == null || waypoints.Length == 0) return; // Перестраховка, если массив внезапно обнулили

        currentWaypointIndex++; // Переходим к следующему индексу в массиве

        if (currentWaypointIndex >= waypoints.Length)
        {
            HandleRouteFinished(); // Обрабатываем завершение трассы (ивенты/UI)
            currentWaypointIndex = loopWaypoints ? 0 : Mathf.Max(waypoints.Length - 1, 0); // Замыкаем или удерживаем индекс
        }
    }

    private void HandleRouteFinished()
    {
        if (finishTriggered && !loopWaypoints) return; // Не повторяем событие в нецикличном режиме

        onRouteFinished?.Invoke(); // Даём инспекторным подписчикам возможность отреагировать

        if (loseOnFinish && loseManager != null)
        {
            if (!string.IsNullOrEmpty(loseMessage))
            {
                loseManager.SetMessage(loseMessage); // Передаём кастомное сообщение в UI
            }

            loseManager.HandleLose(); // Показываем панель поражения/останавливаем игру
        }

        finishTriggered = true; // Запоминаем, что маршрут завершён хотя бы раз
    }

    private void OnDrawGizmosSelected()
    {
        if (waypoints == null || waypoints.Length == 0) return; // Нечего визуализировать

        Gizmos.color = Color.cyan; // Единый цвет для всей трассы
        for (int i = 0; i < waypoints.Length; i++)
        {
            Transform wp = waypoints[i]; // Текущий checkpoint
            if (wp == null) continue; // Пропускаем пустые ссылки, чтобы не ловить исключения

            Gizmos.DrawWireSphere(wp.position, waypointRadius); // Наглядно показывает радиус, в котором точка засчитывается

            if (i + 1 < waypoints.Length && waypoints[i + 1] != null)
            {
                Gizmos.DrawLine(wp.position, waypoints[i + 1].position); // Соединяем соседние точки
            }
            else if (loopWaypoints && waypoints.Length > 0 && waypoints[0] != null)
            {
                Gizmos.DrawLine(wp.position, waypoints[0].position); // В цикле рисуем замыкающую линию к первой точке
            }
        }
    }
}
