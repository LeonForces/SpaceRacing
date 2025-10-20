using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Управляет панелью победы: показывает UI, ставит игру на паузу и даёт методы для перезапуска/выхода.
/// </summary>
public class VictoryPanelManager : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private GameObject panel;          // Корневой объект панели победы
    [SerializeField] private CanvasGroup canvasGroup;   // CanvasGroup для управления прозрачностью/кликами

    [Header("Поведение")]
    [SerializeField] private bool pauseOnShow = true;   // Останавливать ли время после показа
    [SerializeField] private bool fadeIn = true;        // Использовать ли плавное появление
    [SerializeField] private float fadeDuration = 0.35f; // Длительность фейда

    private bool shown;                                 // Флаг, что панель уже была показана
    private bool panelIsSelf;                           // true, если панель — тот же объект, что скрипт

    // Инициализация ссылок и стартовое скрытие панели
    private void Awake()
    {
        if (panel == null)
        {
            panel = gameObject;
        }

        panelIsSelf = panel == gameObject;

        if (canvasGroup == null && panel != null)
        {
            canvasGroup = panel.GetComponent<CanvasGroup>();
            if (canvasGroup == null)
            {
                canvasGroup = panel.AddComponent<CanvasGroup>();
            }
        }

        if (panel != null && !panelIsSelf)
        {
            panel.SetActive(false);
        }

        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }
        else
        {
            Debug.LogWarning("VictoryPanelManager: canvasGroup reference is missing", this);
        }
    }

    // Публичный метод, вызываемый при победе игрока
    public void ShowVictory()
    {
        if (shown) return;
        shown = true;

        if (panel != null && !panel.activeSelf)
        {
            panel.SetActive(true);
        }

        if (canvasGroup != null)
        {
            canvasGroup.interactable = true;
            canvasGroup.blocksRaycasts = true;

            if (fadeIn)
            {
                StartCoroutine(FadeCanvas(0f, 1f, fadeDuration));
            }
            else
            {
                canvasGroup.alpha = 1f;
            }
        }

        if (pauseOnShow)
        {
            StartCoroutine(PauseNextFrame());
        }

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        AudioListener.pause = true;
    }

    // Перезапускает текущую сцену (кнопка "Повторить")
    public void RestartScene()
    {
        AudioListener.pause = false;
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    // Выход из игры или остановка Play Mode в редакторе
    public void QuitGame()
    {
        AudioListener.pause = false;
        Time.timeScale = 1f;
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    // Ставит игру на паузу кадром позже, чтобы успели активироваться UI-компоненты
    private IEnumerator PauseNextFrame()
    {
        yield return null;
        Time.timeScale = 0f;
    }

    // Плавное изменение прозрачности CanvasGroup
    private IEnumerator FadeCanvas(float from, float to, float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / Mathf.Max(0.0001f, duration));
            canvasGroup.alpha = Mathf.Lerp(from, to, t);
            yield return null;
        }
        canvasGroup.alpha = to;
    }
}
