using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Rigidbody))]
public class MouseFlightOneCamOnly : MonoBehaviour
{
    [Header("Camera / HUD")]
    [SerializeField] private Camera cam;             // единственная камера
    [SerializeField] private Rigidbody rb;           // Rigidbody корабля
    [SerializeField] private RectTransform canvas;   // Root Canvas (Overlay)
    [SerializeField] private RectTransform aimUI;    // прицел (мышь)
    [SerializeField] private RectTransform courseUI; // курс (скорость)
    [SerializeField] private Image deviationLine;    // линия между ними

    [Header("Tuning")]
    [SerializeField] private float maxAimAngle = 60f;
    [SerializeField] private float rotationSmoothing = 10f;
    [SerializeField] private float lineThickness = 2f;
    [SerializeField, Range(0.5f, 0.99f)]
    private float viewportRadClamp = 0.92f;

    [Header("Visibility")]
    [Tooltip("Показывать индикатор курса для ЭТОЙ камеры. " +
             "Включи для 1-го лица, выключи для 3-го.")]
    [SerializeField] private bool showCourseOnThisCamera = true;

    [Header("Pause")]
    [SerializeField] private bool respectTimescalePause = true;

    private bool manualPaused;
    private bool lastPaused;

    public void SetPaused(bool paused)
    {
        manualPaused = paused;
        UpdateCanvasActive();
    }

    private bool IsPaused()
    {
        if (!Application.isPlaying) return true;
        if (manualPaused) return true;
        if (respectTimescalePause && Time.timeScale <= 0f) return true;
        return false;
    }

    private void UpdateCanvasActive()
    {
        bool paused = IsPaused();
        if (canvas) canvas.gameObject.SetActive(!paused);
        lastPaused = paused;
    }

    private void Start()
    {
        if (!rb) rb = GetComponent<Rigidbody>();
        if (!cam) cam = Camera.main;
        UpdateCanvasActive();
    }

    private void Update()
    {
        bool p = IsPaused();
        if (p != lastPaused) UpdateCanvasActive();
    }

    private void FixedUpdate()
    {
        if (IsPaused() || !cam || !IsThisCameraActiveUnderMouse()) return;

        Rect pr = cam.pixelRect;
        Vector2 m = Input.mousePosition;
        Vector2 c = pr.center;
        Vector2 d = m - c;
        float r = Mathf.Min(pr.width, pr.height) * 0.5f;
        Vector2 n = Vector2.ClampMagnitude(d / Mathf.Max(1f, r), 1f);

        float yaw = n.x * maxAimAngle;
        float pitch = -n.y * maxAimAngle;

        Quaternion aimRot =
            Quaternion.AngleAxis(yaw, transform.up) *
            Quaternion.AngleAxis(pitch, transform.right);

        Vector3 aimDir = aimRot * transform.forward;
        Quaternion target = Quaternion.LookRotation(aimDir, transform.up);

        float t = 1f - Mathf.Exp(-rotationSmoothing * Time.fixedDeltaTime);
        rb.MoveRotation(Quaternion.Slerp(rb.rotation, target, t));
    }

    private void LateUpdate()
    {
        if (IsPaused() || !cam || !canvas) return;

        bool activeUnderMouse = IsThisCameraActiveUnderMouse();

        // A) Прицел (только если «наша» камера активна под курсором)
        if (aimUI)
        {
            aimUI.gameObject.SetActive(activeUnderMouse);
            if (activeUnderMouse)
            {
                aimUI.anchoredPosition =
                    ScreenToCanvasOverlay(Input.mousePosition);
            }
        }

        // B) Курс (только для 1-го лица и когда «наша» камера активна)
        bool showCourseNow = activeUnderMouse && showCourseOnThisCamera;

        if (courseUI) courseUI.gameObject.SetActive(showCourseNow);
        if (deviationLine) deviationLine.enabled = false;

        if (showCourseNow && courseUI)
        {
            Vector3 v = rb.linearVelocity;
            if (v.sqrMagnitude < 1e-6f)
                v = transform.forward * 1e-4f;

            Vector3 vc = cam.transform.InverseTransformDirection(v);
            float z = Mathf.Max(Mathf.Abs(vc.z), 1e-4f);
            float fovY = cam.fieldOfView * Mathf.Deg2Rad;
            float tanY = Mathf.Tan(fovY * 0.5f);
            float tanX = tanY * cam.aspect;

            Vector2 ndc = new Vector2(vc.x / (z * tanX), vc.y / (z * tanY));
            float mag = ndc.magnitude;
            if (mag > viewportRadClamp) ndc *= viewportRadClamp / mag;

            Vector2 vp = new Vector2(
                0.5f + 0.5f * ndc.x, 0.5f + 0.5f * ndc.y
            );
            Vector2 courseLocal = ViewportToCanvas(vp);
            courseUI.anchoredPosition = courseLocal;

            // C) Линия — только если прицел тоже активен
            if (deviationLine && aimUI && aimUI.gameObject.activeSelf)
            {
                RectTransform line = deviationLine.rectTransform;
                Vector2 aimLocal = aimUI.anchoredPosition;
                Vector2 diff = aimLocal - courseLocal;
                float len = diff.magnitude;

                line.anchoredPosition = courseLocal + diff * 0.5f;
                line.localRotation = Quaternion.Euler(
                    0f, 0f, Mathf.Atan2(diff.y, diff.x) * Mathf.Rad2Deg
                );
                line.sizeDelta = new Vector2(len, lineThickness);
                deviationLine.enabled = len > 1f;
            }
        }
    }

    // Управление — только если:
    // 1) курсор внутри pixelRect этой камеры;
    // 2) нет другой активной камеры на том же дисплее с большим depth.
    private bool IsThisCameraActiveUnderMouse()
    {
        if (!cam || !cam.isActiveAndEnabled) return false;

        Vector2 mouse = Input.mousePosition;
        if (!cam.pixelRect.Contains(mouse)) return false;

        int disp = cam.targetDisplay;
        float myDepth = cam.depth;
        Camera[] cams = Camera.allCameras;

        for (int i = 0; i < cams.Length; i++)
        {
            Camera c = cams[i];
            if (!c || !c.isActiveAndEnabled || c == cam) continue;
            if (c.targetDisplay != disp) continue;
            if (!c.pixelRect.Contains(mouse)) continue;
            if (c.depth > myDepth) return false;
        }
        return true;
    }

    private Vector2 ViewportToCanvas(Vector2 vp)
    {
        Vector2 screen = new Vector2(vp.x * Screen.width, vp.y * Screen.height);
        return ScreenToCanvasOverlay(screen);
    }

    private Vector2 ScreenToCanvasOverlay(Vector2 screen)
    {
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvas, screen, null, out var local
        );
        return local;
    }
}
