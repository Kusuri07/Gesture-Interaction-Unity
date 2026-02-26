using UnityEngine;
using Mediapipe.Tasks.Vision.HandLandmarker;
using Mediapipe.Unity.Sample.HandLandmarkDetection;

/// <summary>
/// mishojo 아래의 bone_1, bone_3 등에 붙어서
/// 핀치 제스처로 해당 뼈(bone)를 살짝 끌어당기는 컨트롤러.
/// (SpriteSkin이 뼈를 따라 스프라이트를 변형합니다)
/// </summary>
public class PNGCheekPinchController : MonoBehaviour
{
    [Header("Refs")]
    [Tooltip("핀치 위치가 이 콜라이더 안일 때만 작동")]
    public Collider2D cheekArea;

    [Tooltip("보통 Main Camera")]
    public Camera mainCamera;

    [Header("Hand")]
    [Tooltip("true = 왼손, false = 오른손")]
    public bool useLeftHand = false;

    [Header("Pinch Threshold (normalized distance)")]
    [Tooltip("이 거리보다 작아지면 핀치 시작")]
    public float pinchOn = 0.05f;

    [Tooltip("이 거리보다 커지면 핀치 해제")]
    public float pinchOff = 0.08f;

    [Header("Bone 이동 설정")]
    [Tooltip("뺨을 최대 얼마나 멀리까지 움직일지 (World)")]
    public float maxPullDistance = 1.0f;

    [Tooltip("뺨 당기기 민감도")]
    public float pullSensitivity = 1.0f;

    [Tooltip("뼈 이동 부드럽게 (SmoothDamp, 작을수록 느림)")]
    public float smoothTime = 0.06f;

    [Header("Debug")]
    public bool debugLog = true;

    // 내부 상태
    private bool _pinching;
    private bool _grabbing;
    private Vector3 _pinchStartWorld;

    private Vector3 _boneLocalOrigin;
    private Vector3 _boneLocalTarget;
    private Vector3 _boneLocalVelocity;

    private Transform _tf;

    private void Awake()
    {
        _tf = transform;
        _boneLocalOrigin = _tf.localPosition;
        _boneLocalTarget = _boneLocalOrigin;
    }

    private void Start()
    {
        if (mainCamera == null) mainCamera = Camera.main;
    }

    private void LateUpdate()
    {
        var dm = HandLandmarkDataManager.Instance;
        if (dm == null || mainCamera == null)
            return;

        // 손 감지 여부
        bool detected = useLeftHand ? dm.IsLeftHandDetected : dm.IsRightHandDetected;
        if (!detected)
        {
            _pinching = false;
            _grabbing = false;
            _boneLocalTarget = _boneLocalOrigin;
            SmoothBone();
            return;
        }

        // 엄지/검지 거리 (0~1 정규화 좌표에서 2D 거리)
        Vector3 thumb = dm.GetThumbTip(useLeftHand);
        Vector3 index = dm.GetIndexFingerTip(useLeftHand);
        float dist = Vector2.Distance(new Vector2(thumb.x, thumb.y), new Vector2(index.x, index.y));

        bool pinchNow = dist < pinchOn;
        bool releaseNow = dist > pinchOff;

        if (!_pinching && pinchNow)
            _pinching = true;
        else if (_pinching && releaseNow)
            _pinching = false;

        // 핀치가 아닌 상태에서는 원위치로 복귀
        if (!_pinching)
        {
            _grabbing = false;
            _boneLocalTarget = _boneLocalOrigin;
            SmoothBone();
            return;
        }

        // 핀치 위치 (월드 좌표)
        Vector3 pinchWorld = GetPinchWorldPoint(dm);
        bool overCheek = cheekArea != null && cheekArea.OverlapPoint((Vector2)pinchWorld);

        if (debugLog)
            Debug.Log($"[PNGCheekPinchController] pinching={_pinching}, overCheek={overCheek}, dist={dist:F3}");

        // 처음 핀치 시작했을 때 기준점 기록 (콜라이더 안이 아니어도 허용)
        if (!_grabbing)
        {
            _grabbing = true;
            _pinchStartWorld = pinchWorld;

            if (debugLog)
                Debug.Log("[PNGCheekPinchController] GRAB START");
        }

        // 당기는 양 계산 (cheek 중심 → 손 방향)
        Vector3 center = cheekArea != null ? cheekArea.bounds.center : _tf.position;
        Vector3 dir = (pinchWorld - center);
        if (dir.sqrMagnitude < 1e-6f) dir = Vector3.right;
        dir.Normalize();

        float amount = Vector3.Dot(pinchWorld - _pinchStartWorld, dir) * pullSensitivity;
        amount = Mathf.Clamp(amount, -maxPullDistance, maxPullDistance);

        Vector3 deltaWorld = dir * amount;
        Vector3 deltaLocal = _tf.parent != null
            ? _tf.parent.InverseTransformVector(deltaWorld)
            : deltaWorld;

        _boneLocalTarget = _boneLocalOrigin + deltaLocal;
        SmoothBone();
    }

    private void SmoothBone()
    {
        _tf.localPosition = Vector3.SmoothDamp(
            _tf.localPosition,
            _boneLocalTarget,
            ref _boneLocalVelocity,
            smoothTime);
    }

    private Vector3 GetPinchWorldPoint(HandLandmarkDataManager dm)
    {
        Vector3 thumb = dm.GetThumbTip(useLeftHand);
        Vector3 index = dm.GetIndexFingerTip(useLeftHand);

        Vector2 c01 = new Vector2(
            (thumb.x + index.x) * 0.5f,
            (thumb.y + index.y) * 0.5f);

        float sx = c01.x * Screen.width;
        float sy = (1f - c01.y) * Screen.height;

        float z = Mathf.Abs(mainCamera.transform.position.z - _tf.position.z);
        Vector3 screen = new Vector3(sx, sy, z);
        return mainCamera.ScreenToWorldPoint(screen);
    }

    // HandLandmarkerRunner에서 호출하는 콜백은 지금은 선택적 디버그용으로만 사용
    private static bool _debugLogged;

    public void ProcessHandLandmarks(HandLandmarkerResult result)
    {
        if (_debugLogged || result.handLandmarks == null) return;
        _debugLogged = true;
        Debug.Log($"[PNGCheekPinchController] First result received. handCount={result.handLandmarks.Count}");
    }
}
