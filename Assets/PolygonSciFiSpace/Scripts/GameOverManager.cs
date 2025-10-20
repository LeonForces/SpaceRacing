using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class GameOverManager : MonoBehaviour
{
    [Header("UI")]
    [Tooltip("Панель экрана поражения (корневой объект).")]
    public GameObject panel;              // Корневой объект UI

    [Tooltip("CanvasGroup панели (для фейда).")]
    public CanvasGroup canvasGroup;       // Контроль прозрачности и кликов

    [Tooltip("Текстовое поле с сообщением (опционально).")]
    public Text messageText;              // Элемент для вывода сообщения

    [Header("Поведение")]
    [Tooltip("Пауза времени при показе экрана.")]
    public bool pauseOnShow = true;       // Остановить ли время при показе

    [Tooltip("Затемнять экран при показе.")]
    public bool fadeIn = true;            // Включать ли плавный фейд

    [Tooltip("Время фейда (сек, в реальном времени).")]
    public float fadeDuration = 0.35f;    // Длительность анимации появления

    private bool _shown;                  // Был ли уже показан экран

    private void Awake()                  // Прячем панель и сбрасываем альфу
    {
        if (panel != null) panel.SetActive(false);
        if (canvasGroup != null) canvasGroup.alpha = 0f;
    }

    public void HandleLose()              // Показываем экран поражения
    {
        if (_shown) return;
        _shown = true;

        if (panel != null) panel.SetActive(true);

        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
            canvasGroup.interactable = true;
            canvasGroup.blocksRaycasts = true;
        }

        if (pauseOnShow) StartCoroutine(PauseNextFrame());
        if (fadeIn && canvasGroup != null) StartCoroutine(FadeCanvas(0f, 1f, fadeDuration));
        else if (canvasGroup != null) canvasGroup.alpha = 1f;

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        AudioListener.pause = true;
    }


    public void SetMessage(string text)   // Устанавливаем текст сообщения
    {
        if (messageText != null) messageText.text = text;
    }

    public void RestartCurrentScene()     // Перезапуск текущей сцены
    {
        AudioListener.pause = false;
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    public void LoadSceneByName(string sceneName) // Загрузка сцены по имени
    {
        AudioListener.pause = false;
        Time.timeScale = 1f;
        if (!string.IsNullOrEmpty(sceneName))
            SceneManager.LoadScene(sceneName);
    }

    public void QuitGame()                // Завершение игры или выхода из Play Mode
    {
        AudioListener.pause = false;
        Time.timeScale = 1f;
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private IEnumerator PauseNextFrame()  // Ставим игру на паузу кадром позже
    {
        yield return null;
        Time.timeScale = 0f;
    }

    private IEnumerator FadeCanvas(float a, float b, float duration) // Плавное изменение альфы
    {
        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / duration);
            canvasGroup.alpha = Mathf.Lerp(a, b, k);
            yield return null;
        }
        canvasGroup.alpha = b;
    }
}
