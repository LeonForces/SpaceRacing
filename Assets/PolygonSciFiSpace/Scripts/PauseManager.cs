using UnityEngine;

public class PauseManager : MonoBehaviour
{
    [Header("UI")]
    public GameObject panel;                        // Панель паузы, которую нужно показать
    public CanvasGroup canvasGroup;                 // CanvasGroup для управления прозрачностью/интерактивностью

    [Header("Настройки")]
    public KeyCode toggleKey = KeyCode.Escape;      // Клавиша для переключения паузы
    public bool pauseAudio = true;                  // Останавливать ли глобальное аудио при паузе
    public bool lockCursorOnResume = true;          // Блокировать ли курсор после выхода из паузы

    private bool isPaused;                          // Текущее состояние паузы
    private bool previousAudioPause;                // Сохраняем прежнее состояние AudioListener.pause

    private void Awake()                            // Подготавливаем UI cостояние
    {
        if (panel != null) panel.SetActive(false);

        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }
    }

    private void Update()                           // Отслеживаем нажатие клавиши паузы
    {
        if (Input.GetKeyDown(toggleKey))
        {
            if (isPaused) Resume();
            else Pause();
        }
    }

    public void Pause()                             // Включаем паузу и показываем UI
    {
        if (isPaused) return;
        isPaused = true;

        previousAudioPause = AudioListener.pause;

        Time.timeScale = 0f;

        if (panel != null) panel.SetActive(true);
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 1f;
            canvasGroup.interactable = true;
            canvasGroup.blocksRaycasts = true;
        }

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        if (pauseAudio) AudioListener.pause = true;
    }

    public void Resume()                            // Выходим из паузы и скрываем UI
    {
        if (!isPaused) return;
        isPaused = false;

        Time.timeScale = 1f;

        if (canvasGroup != null)
        {
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
            canvasGroup.alpha = 0f;
        }

        if (panel != null) panel.SetActive(false);

        if (pauseAudio && !previousAudioPause) AudioListener.pause = false;

        if (lockCursorOnResume)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    private void OnDisable()                        // Гарантируем выход из паузы при выключении объекта
    {
        if (isPaused)
        {
            isPaused = false;
            Time.timeScale = 1f;

            if (pauseAudio && !previousAudioPause) AudioListener.pause = false;
        }

        if (canvasGroup != null)
        {
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
            canvasGroup.alpha = 0f;
        }

        if (panel != null) panel.SetActive(false);
    }
}
