using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class SpaceshipController : MonoBehaviour
{
    [Header("Движение")]
    public float thrustForce = 50f;            // Ускорение (м/с^2)
    public float maxSpeed = 500f;              // Максимальная скорость
    public float boostMultiplier = 2f;         // Множитель ускорения
    public float boostDuration = 3f;           // Длительность ускорения
    public float boostCooldown = 5f;           // Перезарядка ускорения

    [Header("Маневрирование")]
    public float rotationAccel = 1.5f;         // Угловое ускорение (рад/с^2)
    public float strafeAccel = 30f;            // Боковое/вертикальное ускорение (м/с^2)
    public float maxAngularVelocity = 2.5f;    // Максимальная угловая скорость (рад/с)

    [Header("Торможение")]
    public float brakingAccel = 60f;           // Замедление при тормозе (м/с^2)
    [Range(0f, 5f)]
    public float linearDamping = 0.2f;         // Коэф. линейного затухания (1/с)
    [Range(0f, 5f)]
    public float angularDamping = 1.0f;        // Коэф. углового затухания (1/с)

    [Header("Торможение (расширено)")]
    [Tooltip("Целевое время полной остановки при зажатом тормозе, с любой текущей скорости")]
    public float brakeTimeTarget = 1.2f;       // сек
    [Tooltip("Скорость, ниже которой считаем, что стоим")]
    public float stopSpeedEpsilon = 0.5f;      // м/с
    [Tooltip("Во сколько раз сильнее линейное затухание при торможении")]
    public float brakeDampingMultiplier = 4f;
    [Tooltip("Доп. ускорение против продольной компоненты скорости при тормозе")]
    public float retroBoostAccel = 80f;        // м/с^2

    [Header("Стабилизация")]
    public bool useStabilization = true;
    public float stabilizationTorque = 2.0f;   // Сила стабилизации вращения (рад/с^2)
    public float stabilizationThreshold = 0.1f;

    [Header("Гравитационная защита")]
    public bool hasGravityShield = true;
    public float shieldDuration = 5f;
    public float shieldCooldown = 10f;
    public float shieldEnergyConsumption = 20f;

    [Header("Энергия")]
    public float maxEnergy = 100f;
    public float energyRegenRate = 5f;

    [Header("Визуальные эффекты")]
    // Список объектов частиц, которые визуализируют работу двигателей
    public GameObject[] thrusterEffects;
    // Визуальный эффект активированного щита
    public GameObject shieldEffect;
    // Ссылка на модель корабля для визуального крена
    public Transform shipModel;
    // Максимальный угол наклона модели при повороте
    public float bankingAmount = 30f;

    [Header("Звуковые эффекты")]
    // Звук основного двигателя
    public AudioSource thrusterSound;
    // Звук активации ускорителя
    public AudioSource boostSound;
    // Звук включения щита
    public AudioSource shieldSound;

    [Header("Управление")]
    [Tooltip("Если выключить, управление берётся из ApplyControl().")]
    // Флаг: true — игрок управляет напрямую, false — контроль внешним кодом/ИИ
    public bool isPlayerControlled = true;

    // Приватные переменные
    // Кешированный Rigidbody корабля
    private Rigidbody rb;
    // Текущее количество энергии (для щита)
    private float currentEnergy;
    // Флаг активности ускорителя
    private bool isBoostActive;
    // Флаг, активирован ли щит сейчас
    private bool isShieldActive;
    // Таймер оставшегося времени работы ускорителя
    private float boostTimer;
    // Таймер оставшегося времени работы щита
    private float shieldTimer;
    // Время последнего использования ускорителя
    private float lastBoostTime;
    // Время последнего отключения щита
    private float lastShieldTime;

    // Входные данные
    // Текущий ввод тяги (0..1)
    private float thrustInput;       // 0 или 1 — только вперёд
    // Текущий ввод тангажа
    private float pitchInput;        // W/S
    // Текущий ввод рысканья
    private float yawInput;          // A/D
    // Текущий ввод крена
    private float rollInput;         // Q/E
    // Боковой стрейф
    private float strafeHorizontal;  // ←/→
    // Вертикальный стрейф
    private float strafeVertical;    // R/F
    // Запрос на ускорение
    private bool boostInput;         // LeftShift
    // Запрос на торможение
    private bool brakeInput;         // LeftCtrl
    // Запрос на включение/выключение щита
    private bool shieldInput;        // X

    // Состояние управления, которое можно задавать извне (ИИ/скрипты)
    public struct ControlState
    {
        // Нормализованный ввод тяги
        public float thrust;
        // Нормализованный ввод тангажа
        public float pitch;
        // Нормализованный ввод рысканья
        public float yaw;
        // Нормализованный ввод крена
        public float roll;
        // Горизонтальный стрейф
        public float strafeHorizontal;
        // Вертикальный стрейф
        public float strafeVertical;
        // Флаг попытки включить ускоритель
        public bool boost;
        // Флаг зажатого тормоза
        public bool brake;
        // Флаг переключения щита (нажатие)
        public bool shield;
    }

    // Последнее применённое состояние управления
    private ControlState controlState;

    // Состояние
    // Скорость корабля в предыдущем кадре (для анализа/отладки)
    private Vector3 lastVelocity;
    // Флаг, включены ли основные двигатели в текущем кадре
    private bool isUsingThrusters;

    // Инициализируем физику и визуальные эффекты
    private void Start()
    {
        rb = GetComponent<Rigidbody>();
        currentEnergy = maxEnergy;

        // Физика
        rb.useGravity = false;
        rb.linearDamping = 0f;                // штатное сопротивление выключаем
        rb.angularDamping = 0f;         // угловое тоже — затаиваем сами
        rb.maxAngularVelocity = maxAngularVelocity;

        if (shieldEffect != null)
        {
            shieldEffect.SetActive(false);
        }

        SetThrusterEffects(false);
    }

    // Обрабатываем ввод, состояние энергии и визуалы каждый кадр
    private void Update()
    {
        if (isPlayerControlled)
        {
            ApplyControl(ReadPlayerInput());
        }

        UpdateTimers();
        UpdateEnergy();
        UpdateVisualEffects();
        UpdateAudio();

        ToggleGravityShield();
    }

    // Применяем силы и моменты каждый физический кадр
    private void FixedUpdate()
    {
        ApplyMovement();
        ApplyRotation();
        ApplyStrafing();
        ApplyBraking();
        ApplyStabilization();
        ApplySpeedLimit();
        ApplyDamping();

        lastVelocity = rb.linearVelocity;
    }

    // Считываем текущее состояние входов игрока и формируем ControlState
    private ControlState ReadPlayerInput()
    {
        ControlState state = default;

        // Движение только вперёд на пробел (нет обратной тяги)
        state.thrust = Input.GetKey(KeyCode.Space) ? 1f : 0f;

        // Повороты: WS — pitch (вверх/вниз), AD — yaw (влево/вправо)
        float pitch = 0f;
        if (Input.GetKey(KeyCode.W)) pitch -= 1f; // было += 1f
        if (Input.GetKey(KeyCode.S)) pitch += 1f; // было -= 1f
        state.pitch = pitch;

        float yaw = 0f;
        if (Input.GetKey(KeyCode.A)) yaw -= 1f;
        if (Input.GetKey(KeyCode.D)) yaw += 1f;
        state.yaw = yaw;

        // Ролл на Q/E
        float roll = 0f;
        if (Input.GetKey(KeyCode.Q)) roll -= 1f;
        if (Input.GetKey(KeyCode.E)) roll += 1f;
        state.roll = roll;

        // Стрейф: горизонтальный — стрелки, вертикальный — R/F
        float strafeH = 0f;
        if (Input.GetKey(KeyCode.LeftArrow)) strafeH -= 1f;
        if (Input.GetKey(KeyCode.RightArrow)) strafeH += 1f;
        state.strafeHorizontal = strafeH;

        float strafeV = 0f;
        if (Input.GetKey(KeyCode.R)) strafeV += 1f;
        if (Input.GetKey(KeyCode.F)) strafeV -= 1f;
        state.strafeVertical = strafeV;

        // Спец. функции
        state.boost = Input.GetKey(KeyCode.LeftShift);
        state.brake = Input.GetKey(KeyCode.LeftControl); // перенесён с Space
        state.shield = Input.GetKeyDown(KeyCode.X);

        return state;
    }

    // Применяем полученное состояние управления к внутренним переменным контроллера
    public void ApplyControl(ControlState state)
    {
        controlState = state;

        thrustInput = Mathf.Clamp01(Mathf.Max(0f, state.thrust));
        pitchInput = Mathf.Clamp(state.pitch, -1f, 1f);
        yawInput = Mathf.Clamp(state.yaw, -1f, 1f);
        rollInput = Mathf.Clamp(state.roll, -1f, 1f);
        strafeHorizontal = Mathf.Clamp(state.strafeHorizontal, -1f, 1f);
        strafeVertical = Mathf.Clamp(state.strafeVertical, -1f, 1f);
        boostInput = state.boost;
        brakeInput = state.brake;
        shieldInput = state.shield;

        if (brakeInput) thrustInput = 0f;
    }

    // Возвращаем последнее применённое состояние управления (для ИИ/HUD)
    public ControlState CurrentControl => controlState;

    // Обновляем таймеры ускорителя и щита
    private void UpdateTimers()
    {
        if (isBoostActive)
        {
            boostTimer -= Time.deltaTime;
            if (boostTimer <= 0f) isBoostActive = false;
        }

        if (isShieldActive)
        {
            shieldTimer -= Time.deltaTime;
            if (shieldTimer <= 0f || currentEnergy <= 0f)
                DeactivateShield();
        }
    }

    // Поддерживаем регенерацию/расход энергии в зависимости от состояния щита
    private void UpdateEnergy()
    {
        if (!isShieldActive)
        {
            currentEnergy = Mathf.Min(maxEnergy, currentEnergy + energyRegenRate * Time.deltaTime);
        }
        else
        {
            currentEnergy = Mathf.Max(0f, currentEnergy - shieldEnergyConsumption * Time.deltaTime);
        }
    }

    // Применяем тягу и ускорение корабля вперёд, включая буст
    private void ApplyMovement()
    {
        if (thrustInput > 0.01f)
        {
            float accel = thrustForce * (isBoostActive ? boostMultiplier : 1f);
            Vector3 thrust = transform.forward * accel; // только вперёд

            rb.AddForce(thrust, ForceMode.Acceleration); // НЕ умножаем на deltaTime
            isUsingThrusters = true;

            if (boostInput && CanUseBoost() && !isBoostActive)
                ActivateBoost();
        }
        else
        {
            isUsingThrusters = false;
        }
    }

    // Поворачиваем корабль на основе ввода по тангажу/рысканью/крену
    private void ApplyRotation()
    {
        Vector3 localTorque = new Vector3(
            pitchInput * rotationAccel,
            yawInput * rotationAccel,
            rollInput * rotationAccel
        );

        if (localTorque.sqrMagnitude > 0.0001f)
        {
            Vector3 worldTorque =
                transform.right * localTorque.x +
                transform.up * localTorque.y +
                transform.forward * localTorque.z;

            rb.AddTorque(worldTorque, ForceMode.Acceleration);
        }

        if (shipModel != null)
        {
            float bankAngle = -yawInput * bankingAmount;
            Quaternion target = Quaternion.Euler(0f, 0f, bankAngle);
            shipModel.localRotation = Quaternion.Slerp(
                shipModel.localRotation, target, Time.fixedDeltaTime * 5f
            );
        }

        if (rb.angularVelocity.magnitude > maxAngularVelocity)
            rb.angularVelocity = rb.angularVelocity.normalized * maxAngularVelocity;
    }

    // Реализуем боковой и вертикальный стрейф при соответствующем вводе
    private void ApplyStrafing()
    {
        Vector3 accel = Vector3.zero;

        if (Mathf.Abs(strafeHorizontal) > 0.01f)
            accel += transform.right * (strafeHorizontal * strafeAccel);

        if (Mathf.Abs(strafeVertical) > 0.01f)
            accel += transform.up * (strafeVertical * strafeAccel);

        if (accel.sqrMagnitude > 0.0001f)
            rb.AddForce(accel, ForceMode.Acceleration);
    }

    // Обрабатываем активное торможение, чтобы быстро остановить корабль
    private void ApplyBraking()
    {
        if (!brakeInput) return;

        Vector3 v = rb.linearVelocity;
        float speed = v.magnitude;
        if (speed < stopSpeedEpsilon)
        {
            rb.linearVelocity = Vector3.zero; // не ползём
            return;
        }

        float t = Mathf.Max(0.1f, brakeTimeTarget);
        float aDesired = speed / t;
        float a = Mathf.Min(aDesired, brakingAccel);

        rb.AddForce(-v.normalized * a, ForceMode.Acceleration);

        // Доп. подавление продольной компоненты
        Vector3 fwd = transform.forward;
        float vForward = Vector3.Dot(rb.linearVelocity, fwd);
        if (Mathf.Abs(vForward) > 0.01f && retroBoostAccel > 0f)
        {
            Vector3 opposeForward = -Mathf.Sign(vForward) * fwd * retroBoostAccel;
            rb.AddForce(opposeForward, ForceMode.Acceleration);
        }

        isUsingThrusters = false;
        isBoostActive = false;
    }

    // Автоматически стабилизируем корабль, если ввод по осям отсутствует
    private void ApplyStabilization()
    {
        if (!useStabilization) return;

        bool noInput =
            Mathf.Abs(yawInput) < 0.05f &&
            Mathf.Abs(pitchInput) < 0.05f &&
            Mathf.Abs(rollInput) < 0.05f;

        if (noInput && rb.angularVelocity.magnitude > stabilizationThreshold)
        {
            Vector3 counter = -rb.angularVelocity.normalized * stabilizationTorque;
            rb.AddTorque(counter, ForceMode.Acceleration);
        }
    }

    // Ограничиваем максимальную скорость корабля
    private void ApplySpeedLimit()
    {
        if (rb.linearVelocity.magnitude > maxSpeed)
            rb.linearVelocity = rb.linearVelocity.normalized * maxSpeed;
    }

    // Применяем экспоненциальное затухание линейной и угловой скорости
    private void ApplyDamping()
    {
        float dt = Time.fixedDeltaTime;

        if (linearDamping > 0f)
        {
            float damping = linearDamping * (brakeInput ? brakeDampingMultiplier : 1f);
            float lin = Mathf.Exp(-damping * dt);
            rb.linearVelocity *= lin;
        }

        if (angularDamping > 0f)
        {
            float ang = Mathf.Exp(-angularDamping * dt);
            rb.angularVelocity *= ang;
        }
    }

    // Проверяем, готов ли ускоритель к повторному использованию
    private bool CanUseBoost()
    {
        return Time.time - lastBoostTime > boostCooldown && !isBoostActive;
    }

    // Активируем ускоритель и запускаем визуал/звук
    private void ActivateBoost()
    {
        isBoostActive = true;
        boostTimer = boostDuration;
        lastBoostTime = Time.time;

        if (boostSound != null) boostSound.Play();
    }

    // Обрабатываем ввод переключения гравитационного щита
    public void ToggleGravityShield()
    {
        if (shieldInput && hasGravityShield)
        {
            if (!isShieldActive && CanUseShield())
                ActivateShield();
            else if (isShieldActive)
                DeactivateShield();
        }

        shieldInput = false;
    }

    // Проверяем, достаточно ли энергии и прошёл ли кулдаун щита
    private bool CanUseShield()
    {
        return currentEnergy > shieldEnergyConsumption &&
               Time.time - lastShieldTime > shieldCooldown;
    }

    // Включаем щит и визуальные/звуковые эффекты
    private void ActivateShield()
    {
        isShieldActive = true;
        shieldTimer = shieldDuration;

        if (shieldEffect != null) shieldEffect.SetActive(true);
        if (shieldSound != null) shieldSound.Play();
    }

    // Выключаем щит и фиксируем время отключения
    private void DeactivateShield()
    {
        isShieldActive = false;
        lastShieldTime = Time.time;

        if (shieldEffect != null) shieldEffect.SetActive(false);
    }

    // Управляем визуальными эффектами двигателей в зависимости от тяги
    private void UpdateVisualEffects()
    {
        bool shouldShowThrusters = isUsingThrusters || brakeInput;
        SetThrusterEffects(shouldShowThrusters);

        if (thrusterEffects == null || thrusterEffects.Length == 0) return;

        float intensity = thrustInput;               // 0..1
        if (isBoostActive) intensity *= boostMultiplier;

        foreach (var effect in thrusterEffects)
        {
            if (effect == null) continue;
            var ps = effect.GetComponent<ParticleSystem>();
            if (ps == null) continue;

            var emission = ps.emission;
            emission.rateOverTime = intensity * 50f;
        }
    }

    // Включаем/выключаем объекты эффектов двигателей
    private void SetThrusterEffects(bool active)
    {
        if (thrusterEffects == null) return;

        foreach (var effect in thrusterEffects)
            if (effect != null) effect.SetActive(active);
    }

    // Синхронизируем звук двигателя с текущей тягой
    private void UpdateAudio()
    {
        if (thrusterSound == null) return;

        if (isUsingThrusters)
        {
            if (!thrusterSound.isPlaying) thrusterSound.Play();
            float a = thrustInput;
            thrusterSound.pitch = 0.5f + a * 0.5f;
            thrusterSound.volume = 0.3f + a * 0.7f;
        }
        else
        {
            thrusterSound.volume = Mathf.Lerp(thrusterSound.volume, 0f, Time.deltaTime * 5f);
            if (thrusterSound.volume < 0.01f) thrusterSound.Stop();
        }
    }

    // Публичные методы
    // Флаг активного щита для HUD/логики
    public bool IsShieldActive() => isShieldActive;
    // Флаг активного ускорителя
    public bool IsBoostActive() => isBoostActive;
    // Текущий процент энергии
    public float GetEnergyPercent() => currentEnergy / maxEnergy;
    // Текущий процент скорости от максимальной
    public float GetSpeedPercent() => rb.linearVelocity.magnitude / maxSpeed;
    // Текущая скорость корабля в мировых координатах
    public Vector3 GetVelocity() => rb.linearVelocity;
    // Нормализованный прогресс кулдауна ускорителя (0..1)
    public float GetBoostCooldownPercent() => Mathf.Clamp01((Time.time - lastBoostTime) / boostCooldown);
    // Нормализованный прогресс кулдауна щита (0..1)
    public float GetShieldCooldownPercent() => Mathf.Clamp01((Time.time - lastShieldTime) / shieldCooldown);

    // Визуализация направления носа, скорости и щита в редакторе
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.blue;
        Gizmos.DrawRay(transform.position, transform.forward * 5f);

        if (Application.isPlaying && rb != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawRay(transform.position, rb.linearVelocity);
        }

        if (isShieldActive)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(transform.position, 2f);
        }
    }
}
