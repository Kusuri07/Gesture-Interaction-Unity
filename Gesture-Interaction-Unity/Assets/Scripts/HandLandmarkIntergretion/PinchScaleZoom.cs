using UnityEngine;
using Mediapipe.Unity.Sample.HandLandmarkDetection;

public class PinchScaleZoom : MonoBehaviour
{
    [Header("Target (비우면 이 오브젝트 스케일 변경)")]
    public Transform target;

    [Header("Zoom Engage/Release (3D distance, normalized)")]
    [Tooltip("이 값보다 가까워지면(핀치) 줌 모드 시작")]
    public float zoomEngageThreshold = 0.20f;

    [Tooltip("이 값보다 멀어지면 줌 모드 종료(놓기)")]
    public float zoomReleaseThreshold = 0.45f;

    [Header("Scale Limits")]
    public float minUniformScale = 0.3f;
    public float maxUniformScale = 2.5f;

    [Header("Tuning")]
    [Tooltip("스케일 반응 민감도(1=기본, 1.5~2면 더 민감)")]
    public float sensitivity = 1.2f;

    [Tooltip("스케일 변화 부드럽게(클수록 빠르게 따라감)")]
    public float smooth = 15f;

    [Header("Debug")]
    public bool debugLog = true;
    public bool debugOnGUI = true;

    private bool zooming = false;
    private float startDist = 0.0001f;
    private Vector3 startScale;
    private float currentDist = 999f;
    private string msg = "-";

    void Awake()
    {
        if (target == null) target = transform;
        startScale = target.localScale;
    }

    void Update()
    {
        var dm = HandLandmarkDataManager.Instance;
        if (dm == null) return;

        bool hasRight = dm.IsRightHandDetected;
        bool hasLeft = dm.IsLeftHandDetected;

        if (!hasRight && !hasLeft)
        {
            if (zooming) EndZoom("hand lost");
            return;
        }

        // 오른손 우선, 없으면 왼손
        bool useLeft = !hasRight && hasLeft;

        Vector3 thumb = dm.GetThumbTip(useLeft);        // 4
        Vector3 index = dm.GetIndexFingerTip(useLeft);  // 8

        float dist = Vector3.Distance(thumb, index); // ✅ 3D 거리
        currentDist = dist;

        if (!zooming)
        {
            if (dist < zoomEngageThreshold)
                BeginZoom(dist);
        }
        else
        {
            if (dist > zoomReleaseThreshold)
            {
                EndZoom("release");
            }
            else
            {
                ApplyZoom(dist);
            }
        }
    }

    private void BeginZoom(float dist)
    {
        zooming = true;
        startDist = Mathf.Max(dist, 0.0001f);
        startScale = target.localScale;
        msg = "ZOOM BEGIN";

        if (debugLog)
            Debug.Log($"[PinchScaleZoom] {msg} startDist={startDist:F3} startScale={startScale.x:F2}");
    }

    private void ApplyZoom(float dist)
    {
        // dist가 커지면(손가락 벌림) => 확대, 작아지면 => 축소
        float factor = dist / startDist;
        factor = Mathf.Pow(factor, sensitivity);

        float desiredUniform = Mathf.Clamp(startScale.x * factor, minUniformScale, maxUniformScale);
        Vector3 desiredScale = Vector3.one * desiredUniform;

        target.localScale = Vector3.Lerp(target.localScale, desiredScale, smooth * Time.deltaTime);

        msg = (factor >= 1f) ? "ZOOM IN (scale up)" : "ZOOM OUT (scale down)";
    }

    private void EndZoom(string reason)
    {
        zooming = false;
        msg = $"ZOOM END ({reason})";

        if (debugLog)
            Debug.Log($"[PinchScaleZoom] {msg} finalScale={target.localScale.x:F2}");
    }

    void OnGUI()
    {
        if (!debugOnGUI) return;
        GUI.Label(new Rect(10, 60, 800, 22),
            $"[PinchScaleZoom] zooming={zooming} dist={currentDist:F3} scale={target.localScale.x:F2} msg={msg}");
        GUI.Label(new Rect(10, 80, 800, 22),
            $"threshold engage<{zoomEngageThreshold:F2}  release>{zoomReleaseThreshold:F2}");
    }
}
