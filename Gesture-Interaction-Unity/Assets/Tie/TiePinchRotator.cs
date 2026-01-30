using UnityEngine;
using Mediapipe.Unity.Sample.HandLandmarkDetection;

public class TiePinchRotator : MonoBehaviour
{
    [Header("References")]
    public Transform tiePivot;          // 회전축(TiePivot)
    public Camera cam;                  // 보통 Main Camera
    public Collider2D tieCollider;      // (선택) 넥타이 콜라이더. 없으면 아무데서나 핀치해도 회전

    [Header("Hand")]
    public bool useLeftHand = false;    // false=오른손, true=왼손 (원하는 쪽으로)

    [Header("Pinch Threshold (normalized)")]
    public float pinchOn = 0.05f;       // 핀치 시작 거리(작을수록 엄격)
    public float pinchOff = 0.07f;      // 핀치 해제 거리(핀치On보다 크게)

    [Header("Rotation")]
    public float maxAngle = 50f;        // 좌/우 최대 회전 각도
    public float sensitivity = 1.0f;    // 회전 민감도
    public float smoothTime = 0.06f;    // 부드럽게(작을수록 빠르게)

    private bool pinching;
    private float startPointerAngle;
    private float startPivotAngle;
    private float angVel;

    void Reset()
    {
        cam = Camera.main;
    }

    void Update()
    {
        var dm = HandLandmarkDataManager.Instance;
        if (dm == null || tiePivot == null) return;

        // 손 선택
        bool detected = useLeftHand ? dm.IsLeftHandDetected : dm.IsRightHandDetected;
        if (!detected)
        {
            pinching = false;
            return;
        }

        // 엄지/검지 tip (normalized 0~1)
        Vector3 thumb3 = dm.GetThumbTip(useLeftHand);
        Vector3 index3 = dm.GetIndexFingerTip(useLeftHand);

        Vector2 thumb = new Vector2(thumb3.x, thumb3.y);
        Vector2 index = new Vector2(index3.x, index3.y);

        float dist = Vector2.Distance(thumb, index);
        Vector2 mid = (thumb + index) * 0.5f; // 핀치 포인트

        // 핀치 시작/해제
        if (!pinching)
        {
            if (dist < pinchOn && IsOverTie(mid))
            {
                BeginPinch(mid);
            }
        }
        else
        {
            if (dist > pinchOff)
            {
                pinching = false;
                return;
            }

            UpdateRotation(mid);
        }
    }

    void BeginPinch(Vector2 pinchMidNorm)
    {
        pinching = true;

        Vector2 worldMid = NormToWorld(pinchMidNorm);
        Vector2 v = worldMid - (Vector2)tiePivot.position;

        startPointerAngle = Mathf.Atan2(v.y, v.x) * Mathf.Rad2Deg;
        startPivotAngle = Mathf.DeltaAngle(0f, tiePivot.eulerAngles.z); // -180~180로 정규화
        angVel = 0f;
    }

    void UpdateRotation(Vector2 pinchMidNorm)
    {
        Vector2 worldMid = NormToWorld(pinchMidNorm);
        Vector2 v = worldMid - (Vector2)tiePivot.position;

        float pointerAngle = Mathf.Atan2(v.y, v.x) * Mathf.Rad2Deg;
        float delta = Mathf.DeltaAngle(startPointerAngle, pointerAngle) * sensitivity;

        float desired = Mathf.Clamp(startPivotAngle + delta, -maxAngle, maxAngle);

        float current = Mathf.DeltaAngle(0f, tiePivot.eulerAngles.z);
        float z = Mathf.SmoothDampAngle(current, desired, ref angVel, smoothTime);

        tiePivot.rotation = Quaternion.Euler(0f, 0f, z);
    }

    bool IsOverTie(Vector2 pinchMidNorm)
    {
        if (tieCollider == null) return true;
        Vector2 worldMid = NormToWorld(pinchMidNorm);
        return tieCollider.OverlapPoint(worldMid);
    }

    Vector2 NormToWorld(Vector2 norm)
    {
        if (cam == null) cam = Camera.main;

        // Mediapipe normalized는 보통 y가 "위=0 아래=1"인 경우가 많아서 Unity Screen 좌표로 뒤집어줌
        float sx = norm.x * Screen.width;
        float sy = (1f - norm.y) * Screen.height;

        float zDist = Mathf.Abs(cam.transform.position.z - tiePivot.position.z);
        Vector3 world = cam.ScreenToWorldPoint(new Vector3(sx, sy, zDist));
        return world;
    }
}
