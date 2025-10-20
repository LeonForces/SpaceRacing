using UnityEngine;

public class GravityCameraController : MonoBehaviour
{
    [Header("Цель следования")]
    public Transform target;                      // Объект, за которым следует камера

    [Header("Настройки позиции")]
    public Vector3 offset = new Vector3(0f, 8f, -25f); // Базовое смещение относительно цели
    public float followSpeed = 3f;                     // Скорость перемещения камеры
    public float zoomSpeed = 15f;                      // Скорость изменения дистанции колесом мыши

    [Header("Динамическое смещение")]
    public bool useDynamicOffset = true;               // Подстраивать позицию под скорость цели
    public float velocityInfluence = 1.2f;             // Насколько сильно учитываем скорость цели
    public float maxVelocityOffset = 8f;               // Максимальное динамическое смещение

    [Header("Границы камеры")]
    public float minDistance = 150f;                   // Нижний предел дистанции до цели
    public float maxDistance = 500f;                   // Верхний предел дистанции до цели

    [Header("Сглаживание")]
    public float positionSmoothTime = 0.8f;            // Время сглаживания движения камеры

    [Header("Настройки камеры от 3го лица")]
    public float heightOffset = 300f;                  // Высота камеры над целью
    public float behindDistance = 300f;                // Расстояние позади цели

    [Tooltip("Камера позиционируется в локальных осях цели (за ней и сверху)")]
    public bool followTargetAxes = true;               // Использовать ли локальные оси цели

    [Tooltip("Камера повторяет вращение цели (yaw/pitch/roll)")]
    public bool matchTargetRotation = true;            // Выравнивать ли вращение с целью

    [Tooltip("Скорость поворота камеры при ручном управлении (град/сек)")]
    public float manualTurnSpeed = 240f;               // Скорость поворота в ручном режиме

    [Tooltip("Скорость поворота камеры при автоследовании (град/сек)")]
    public float followTurnSpeed = 200f;               // Скорость поворота в авто-режиме

    [Tooltip("Скорость сведения при совпадении поворота с целью (град/сек)")]
    public float matchRotationSpeed = 240f;            // Скорость выравнивания при matchTargetRotation

    [Header("Эффекты от гравитации")]
    public bool gravityCameraShake = true;             // Включить эффект дрожания
    public float shakeIntensity = 0.1f;                // Интенсивность дрожания
    public float shakeDamping = 5f;                    // Скорость затухания дрожания

    private Vector3 velocity = Vector3.zero;           // Текущая скорость сглаживания
    private Vector3 currentOffset;                     // Текущее смещение камеры
    private Rigidbody targetRigidbody;                 // Rigidbody цели для чтения скорости
    private Vector3 shakeOffset = Vector3.zero;        // Смещение от эффекта дрожания
    private float currentDistance;                     // Текущая дистанция камеры

    private float mouseX;                              // Суммарное вращение по горизонтали
    private float mouseY;                              // Суммарное вращение по вертикали
    private bool isManualControl;                      // Активен ли ручной режим камеры

    private void Start()                               // Ищем цель и выставляем стартовую позицию
    {
        if (target == null)
        {
            GameObject playerShip = GameObject.FindGameObjectWithTag("Player");
            if (playerShip != null)
            {
                target = playerShip.transform;
            }
        }

        if (target != null)
        {
            targetRigidbody = target.GetComponent<Rigidbody>();
            currentOffset = offset;
            currentDistance = offset.magnitude;

            Vector3 initialPos = target.position - target.forward * behindDistance + Vector3.up * heightOffset;
            transform.position = initialPos;
            transform.LookAt(target.position, Vector3.up);
        }
    }

    private void LateUpdate()                          // Обновляем камеру после движения цели
    {
        if (target == null)
        {
            return;
        }

        HandleInput();
        UpdateCameraPosition();
        UpdateCameraRotation();
        ApplyGravityEffects();
    }

    private void HandleInput()                         // Обрабатываем зум и ручное вращение
    {
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (Mathf.Abs(scroll) > Mathf.Epsilon)
        {
            currentDistance = Mathf.Clamp(currentDistance - scroll * zoomSpeed, minDistance, maxDistance);
        }

        if (Input.GetMouseButton(1))
        {
            isManualControl = true;
            mouseX += Input.GetAxis("Mouse X");
            mouseY -= Input.GetAxis("Mouse Y");
            mouseY = Mathf.Clamp(mouseY, -60f, 60f);
        }
        else
        {
            isManualControl = false;
        }

        if (Input.GetKeyDown(KeyCode.R))
        {
            mouseX = 0f;
            mouseY = 0f;
            currentDistance = offset.magnitude;
        }
    }

    private void UpdateCameraPosition()                // Вычисляем новую позицию камеры
    {
        Vector3 targetPosition;

        if (isManualControl)
        {
            Quaternion rotation = Quaternion.Euler(mouseY, mouseX, 0f);
            targetPosition = target.position + rotation * Vector3.back * currentDistance;
        }
        else
        {
            if (followTargetAxes)
            {
                Vector3 localDesired = Vector3.back * behindDistance + Vector3.up * heightOffset;
                Vector3 desiredWorld = target.TransformPoint(localDesired.normalized * currentDistance);

                if (useDynamicOffset && targetRigidbody != null)
                {
                    Vector3 velocityOffset = targetRigidbody.linearVelocity * velocityInfluence;
                    velocityOffset = Vector3.ClampMagnitude(velocityOffset, maxVelocityOffset);

                    desiredWorld -= velocityOffset * 0.5f;
                }

                targetPosition = desiredWorld;
            }
            else
            {
                Vector3 behindTarget = target.position - target.forward * behindDistance;
                Vector3 aboveTarget = behindTarget + Vector3.up * heightOffset;
                Vector3 desiredOffset = (aboveTarget - target.position).normalized * currentDistance;

                if (useDynamicOffset && targetRigidbody != null)
                {
                    Vector3 velocityOffset = targetRigidbody.linearVelocity * velocityInfluence;
                    velocityOffset = Vector3.ClampMagnitude(velocityOffset, maxVelocityOffset);
                    desiredOffset -= velocityOffset * 0.5f;
                }

                targetPosition = target.position + desiredOffset;
            }
        }

        if (gravityCameraShake)
        {
            targetPosition += shakeOffset;
        }

        transform.position = Vector3.SmoothDamp(
            transform.position,
            targetPosition,
            ref velocity,
            positionSmoothTime
        );
    }

    private void UpdateCameraRotation()                // Поворачиваем камеру к цели
    {
        Vector3 toTarget = target.position - transform.position;
        if (toTarget.sqrMagnitude < 0.0001f)
        {
            return;
        }

        Quaternion lookAtTarget = Quaternion.LookRotation(toTarget.normalized, target.up);

        if (isManualControl)
        {
            transform.rotation = Quaternion.RotateTowards(
                transform.rotation,
                lookAtTarget,
                manualTurnSpeed * Time.deltaTime
            );
            return;
        }

        transform.rotation = Quaternion.RotateTowards(
            transform.rotation,
            lookAtTarget,
            followTurnSpeed * Time.deltaTime
        );
    }


    private void ApplyGravityEffects()                 // Рассчитываем смещение дрожания
    {
        if (!gravityCameraShake)
        {
            return;
        }

        float totalGravityForce = 0f;

        GravitySource[] gravitySources = UnityEngine.Object.FindObjectsByType<GravitySource>(FindObjectsSortMode.None);
        foreach (var source in gravitySources)
        {
            float distance = Vector3.Distance(transform.position, source.transform.position);
            if (distance < source.influenceRadius && distance > 0.001f)
            {
                float force = source.gravityStrength / (distance * distance);
                totalGravityForce += force;
            }
        }

        if (totalGravityForce > 0.01f)
        {
            Vector3 randomShake = Random.insideUnitSphere * shakeIntensity * totalGravityForce;
            shakeOffset = Vector3.Lerp(shakeOffset, randomShake, Time.deltaTime * shakeDamping);
        }
        else
        {
            shakeOffset = Vector3.Lerp(shakeOffset, Vector3.zero, Time.deltaTime * shakeDamping);
        }
    }

    public void SetTarget(Transform newTarget)         // Меняем цель слежения
    {
        target = newTarget;
        targetRigidbody = target != null ? target.GetComponent<Rigidbody>() : null;
    }

    public void FocusOnTarget(float duration = 1f)     // Плавно переносим камеру к offset
    {
        if (target != null)
        {
            StartCoroutine(FocusCoroutine(duration));
        }
    }

    private System.Collections.IEnumerator FocusCoroutine(float duration) // Анимация перемещения
    {
        Vector3 startPos = transform.position;
        Vector3 endPos = target.position + offset;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            transform.position = Vector3.Lerp(startPos, endPos, t);
            yield return null;
        }
    }

    private void OnDrawGizmosSelected()                // Рисуем вспомогательные Gizmos
    {
        if (target == null)
        {
            return;
        }

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(target.position, 0.5f);

        Gizmos.color = Color.blue;
        Gizmos.DrawWireSphere(transform.position, 0.3f);

        Gizmos.color = Color.white;
        Gizmos.DrawLine(transform.position, target.position);

        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(target.position, minDistance);

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(target.position, maxDistance);
    }
}

[System.Serializable]
public class GravitySource : MonoBehaviour
{
    public float gravityStrength = 10f;    // Сила гравитации источника
    public float influenceRadius = 50f;    // Радиус действия источника
}
