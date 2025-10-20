using UnityEngine;
using UnityEngine.Events;

[RequireComponent(typeof(Collider))]
public class RingPickup : MonoBehaviour
{
    [SerializeField] private string requiredTag = "Player";      // Тег, который может собирать кольцо
    [SerializeField] private bool destroyOnPickup = false;        // Уничтожить ли кольцо после сбора
    [SerializeField] private GameObject objectToActivate;         // Объект, который включается при сборе
    [SerializeField] private UnityEvent onCollected;              // Событие, вызываемое после сбора

    [Header("Поражение")]
    [SerializeField] private string loseOnTag = "Enemy";         // Тег, вызывающий поражение
    [SerializeField] private GameOverManager loseManager;         // Менеджер экрана поражения
    [SerializeField] private string loseMessage;                  // Сообщение для экрана поражения

    private Collider trigger;                                      // Кешированный триггер

    private void Reset()                                           // Настраиваем коллайдер как триггер в редакторе
    {
        trigger = GetComponent<Collider>();
        if (trigger != null)
        {
            trigger.isTrigger = true;
        }
    }

    private void Awake()                                           // Дополнительно убеждаемся, что коллайдер — триггер
    {
        trigger = GetComponent<Collider>();
        if (trigger != null && !trigger.isTrigger)
        {
            trigger.isTrigger = true;
        }
    }

    private void OnTriggerEnter(Collider other)                    // Обработка входа в кольцо
    {
        if (!string.IsNullOrEmpty(loseOnTag) && other.CompareTag(loseOnTag))
        {
            TriggerLose();
            return;
        }

        if (!string.IsNullOrEmpty(requiredTag))
        {
            if (!other.CompareTag(requiredTag))
            {
                return;
            }
        }
        else if (other.GetComponent<SpaceshipController>() == null)
        {
            return;
        }

        if (objectToActivate != null)
        {
            objectToActivate.SetActive(true);
        }

        onCollected?.Invoke();

        if (destroyOnPickup)
        {
            Destroy(gameObject);
        }
        else
        {
            gameObject.SetActive(false);
        }
    }

    private void TriggerLose()                                     // Включаем экран поражения с сообщением
    {
        if (loseManager == null) return;

        if (!string.IsNullOrEmpty(loseMessage))
        {
            loseManager.SetMessage(loseMessage);
        }

        loseManager.HandleLose();
    }
}
