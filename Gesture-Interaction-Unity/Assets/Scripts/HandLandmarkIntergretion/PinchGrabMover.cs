using UnityEngine;
using Mediapipe.Unity.Sample.HandLandmarkDetection;

public class PinchGrabMover : MonoBehaviour
{
    [Header("Grab Target (비워두면 이 오브젝트를 잡음)")]
    public Transform target;

    [Header("Camera")]
    public Camera cam;

    [Header("Pinch Threshold (normalized)")]
    public float pinchStartThreshold = 0.08f;
    public float pinchReleaseThreshold = 0.12f;

    [Header("Raycast / Layer")]
    public LayerMask grabbableMask = ~0;
    public bool requireHitOnTarget = true;

    [Header("Follow")]
    public float followSpeed = 15f;

    [Header("Debug")]
    public bool debugLog = true;
    public bool debugOnScreen = true;

    [Header("Fallback (Raycast 실패 시 화면거리로 집기)")]
    public bool useScreenDistancePick = true;
    public float pickRadiusPx = 120f;

    private bool isGrabbing = false;
    private Plane grabPlane;
    private Vector3 grabOffset;
    private Transform grabbed;

    // debug state
    private bool wasPinching = false;
    private string lastMsg = "-";
    private string lastHitName = "-";
    private float lastPinchDist = 999f;

    void Awake()
    {
        if (target == null) target = transform;
        if (cam == null) cam = Camera.main;
    }

    void Update()
    {
        if (HandLandmarkDataManager.Instance == null || cam == null) return;

        bool hasRight = HandLandmarkDataManager.Instance.IsRightHandDetected;
        bool hasLeft = HandLandmarkDataManager.Instance.IsLeftHandDetected;

        if (!hasRight && !hasLeft)
        {
            if (isGrabbing) EndGrab("손 인식 끊김 -> 놓기");
            wasPinching = false;
            return;
        }

        // 오른손 우선, 없으면 왼손
        bool useLeft = !hasRight && hasLeft;

        Vector3 thumb = HandLandmarkDataManager.Instance.GetThumbTip(useLeft);       // 4
        Vector3 index = HandLandmarkDataManager.Instance.GetIndexFingerTip(useLeft); // 8

        float pinchDist = Vector2.Distance(new Vector2(thumb.x, thumb.y), new Vector2(index.x, index.y));
        lastPinchDist = pinchDist;

        if (Time.frameCount % 15 == 0) // 15프레임마다 한 번
        {
            Debug.Log($"[PinchGrabMover] pinchDist={pinchDist:F3}  start={pinchStartThreshold:F3}  release={pinchReleaseThreshold:F3}");
        }

        // pinch screen point (thumb-index midpoint)
        Vector2 pinchNorm = new Vector2((thumb.x + index.x) * 0.5f, (thumb.y + index.y) * 0.5f);
        Vector2 pinchScreen = new Vector2(pinchNorm.x * Screen.width, (1f - pinchNorm.y) * Screen.height);

        bool pinchStart = pinchDist < pinchStartThreshold;
        bool pinchRelease = pinchDist > pinchReleaseThreshold;

        // 핀치 시작/해제 로그 (상태 변화만 찍음)
        if (!wasPinching && pinchStart)
        {
            Log($"PINCH START (hand={(useLeft ? "Left" : "Right")}) dist={pinchDist:F3} screen=({pinchScreen.x:F1},{pinchScreen.y:F1})");
        }
        if (wasPinching && pinchRelease)
        {
            Log($"PINCH RELEASE dist={pinchDist:F3}");
        }
        wasPinching = pinchStart ? true : (pinchRelease ? false : wasPinching);

        if (!isGrabbing)
        {
            if (pinchStart)
                TryBeginGrab(pinchScreen);
        }
        else
        {
            if (pinchRelease)
            {
                EndGrab("pinchRelease");
            }
            else
            {
                UpdateGrab(pinchScreen);
            }
        }
    }

    private void TryBeginGrab(Vector2 pinchScreen)
    {
        Ray ray = cam.ScreenPointToRay(pinchScreen);
        lastHitName = "-";

        // 1) Raycast로 집기 시도
        if (Physics.Raycast(ray, out RaycastHit hit, 200f, grabbableMask))
        {
            lastHitName = hit.transform.name;

            if (requireHitOnTarget)
            {
                if (hit.transform != target && !hit.transform.IsChildOf(target))
                {
                    Log($"RAY HIT '{hit.transform.name}' BUT NOT TARGET '{target.name}' -> grab fail");
                    lastMsg = "Raycast hit but not target";
                    return;
                }
            }

            BeginGrab(ray, "Raycast hit");
            return;
        }

        Log("RAY NO HIT");

        // 2) 대안: collider/레이캐스트가 애매하면 “화면거리”로도 집기 허용
        if (useScreenDistancePick)
        {
            Vector3 t = cam.WorldToScreenPoint(target.position);
            float d = Vector2.Distance(new Vector2(t.x, t.y), pinchScreen);

            if (d <= pickRadiusPx)
            {
                Log($"FALLBACK PICK OK (screenDist={d:F1}px <= {pickRadiusPx}px)");
                BeginGrab(ray, "Fallback screen-distance pick");
                return;
            }
            else
            {
                Log($"FALLBACK PICK FAIL (screenDist={d:F1}px > {pickRadiusPx}px)");
            }
        }

        lastMsg = "No hit / fallback fail";
    }

    private void BeginGrab(Ray ray, string reason)
    {
        grabbed = target;

        // 카메라 평면에 고정된 드래그(튀는 것 방지)
        grabPlane = new Plane(cam.transform.forward, grabbed.position);

        if (grabPlane.Raycast(ray, out float enter))
        {
            Vector3 fingerWorld = ray.GetPoint(enter);
            grabOffset = grabbed.position - fingerWorld;
            isGrabbing = true;
            lastMsg = $"GRAB BEGIN ({reason})";
            Log(lastMsg);
        }
        else
        {
            lastMsg = "Plane.Raycast failed";
            Log(lastMsg);
            grabbed = null;
            isGrabbing = false;
        }
    }

    private void UpdateGrab(Vector2 pinchScreen)
    {
        if (grabbed == null) { EndGrab("grabbed null"); return; }

        Ray ray = cam.ScreenPointToRay(pinchScreen);
        if (!grabPlane.Raycast(ray, out float enter)) { lastMsg = "Plane.Raycast fail in update"; return; }

        Vector3 fingerWorld = ray.GetPoint(enter);
        Vector3 targetPos = fingerWorld + grabOffset;

        grabbed.position = Vector3.Lerp(grabbed.position, targetPos, followSpeed * Time.deltaTime);
        lastMsg = "GRABBING...";
    }

    private void EndGrab(string reason)
    {
        isGrabbing = false;
        grabbed = null;
        lastMsg = $"GRAB END ({reason})";
        Log(lastMsg);
    }

    private void Log(string msg)
    {
        if (!debugLog) return;
        Debug.Log($"[PinchGrabMover] {msg}");
    }

    void OnGUI()
    {
        if (!debugOnScreen) return;
        GUI.Label(new Rect(10, 10, 520, 22), $"pinchDist={lastPinchDist:F3}  grabbing={isGrabbing}  lastHit={lastHitName}");
        GUI.Label(new Rect(10, 30, 900, 22), $"state: {lastMsg}");
    }
}
