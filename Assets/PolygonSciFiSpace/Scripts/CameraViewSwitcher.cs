using UnityEngine;

/// <summary>
/// Переключение между видом от первого и третьего лица
/// Управляет активацией двух камер и тегом MainCamera
/// </summary>
public class CameraViewSwitcher : MonoBehaviour
{
    [Header("Камеры")]
    [Tooltip("Камера от третьего лица (Main Camera с GravityCameraController)")]
    public Camera thirdPersonCamera;
    
    [Tooltip("Камера от первого лица (в кабине корабля)")]
    public Camera firstPersonCamera;

    [Header("Настройки")]
    [Tooltip("Клавиша для переключения вида")]
    public KeyCode toggleKey = KeyCode.V;
    
    [Tooltip("Начинать с вида от первого лица?")]
    public bool startInFirstPerson = false;

    [Header("UI (опционально)")]
    [Tooltip("Canvas UI для переключения рендер-камеры (если используется ScreenSpace-Camera)")]
    public Canvas uiCanvas;

    // Текущая активная камера
    private bool isFirstPerson;

    void Start()
    {
        // Проверка на наличие обеих камер
        if (thirdPersonCamera == null || firstPersonCamera == null)
        {
            Debug.LogError("CameraViewSwitcher: Не назначены обе камеры! Отключаю скрипт.");
            enabled = false;
            return;
        }

        // Проверка AudioListener - должен быть только у одной камеры
        CheckAudioListeners();

        // Установка начального вида
        isFirstPerson = startInFirstPerson;
        ApplyCameraView(isFirstPerson);
    }

    void Update()
    {
        // Переключение по клавише
        if (Input.GetKeyDown(toggleKey))
        {
            ToggleView();
        }
    }

    /// <summary>
    /// Переключить вид между первым и третьим лицом
    /// </summary>
    public void ToggleView()
    {
        isFirstPerson = !isFirstPerson;
        ApplyCameraView(isFirstPerson);
    }

    /// <summary>
    /// Применить вид камеры
    /// </summary>
    /// <param name="firstPerson">True = первое лицо, False = третье лицо</param>
    private void ApplyCameraView(bool firstPerson)
    {
        if (firstPerson)
        {
            // Переключаемся на вид от первого лица
            thirdPersonCamera.gameObject.SetActive(false);
            firstPersonCamera.gameObject.SetActive(true);

            // Переключаем тег MainCamera
            thirdPersonCamera.tag = "Untagged";
            firstPersonCamera.tag = "MainCamera";

            Debug.Log("Переключено на вид от первого лица");
        }
        else
        {
            // Переключаемся на вид от третьего лица
            firstPersonCamera.gameObject.SetActive(false);
            thirdPersonCamera.gameObject.SetActive(true);

            // Переключаем тег MainCamera
            firstPersonCamera.tag = "Untagged";
            thirdPersonCamera.tag = "MainCamera";

            Debug.Log("Переключено на вид от третьего лица");
        }

        // Обновляем UI Canvas, если он использует Camera режим
        UpdateUICanvas();
    }

    /// <summary>
    /// Обновить ссылку на камеру в UI Canvas
    /// </summary>
    private void UpdateUICanvas()
    {
        if (uiCanvas != null && uiCanvas.renderMode == RenderMode.ScreenSpaceCamera)
        {
            Camera activeCamera = isFirstPerson ? firstPersonCamera : thirdPersonCamera;
            uiCanvas.worldCamera = activeCamera;
        }
    }

    /// <summary>
    /// Проверка и предупреждение о дублирующихся AudioListener
    /// </summary>
    private void CheckAudioListeners()
    {
        AudioListener listener1 = thirdPersonCamera.GetComponent<AudioListener>();
        AudioListener listener2 = firstPersonCamera.GetComponent<AudioListener>();

        if (listener1 != null && listener2 != null)
        {
            Debug.LogWarning("CameraViewSwitcher: Обе камеры имеют AudioListener! " +
                           "Удалите AudioListener с FirstPersonCamera, чтобы избежать конфликтов.");
        }
    }

    /// <summary>
    /// Получить текущий режим камеры
    /// </summary>
    public bool IsFirstPersonView()
    {
        return isFirstPerson;
    }

    /// <summary>
    /// Принудительно установить вид от первого лица
    /// </summary>
    public void SetFirstPersonView()
    {
        if (!isFirstPerson)
        {
            ToggleView();
        }
    }

    /// <summary>
    /// Принудительно установить вид от третьего лица
    /// </summary>
    public void SetThirdPersonView()
    {
        if (isFirstPerson)
        {
            ToggleView();
        }
    }
}