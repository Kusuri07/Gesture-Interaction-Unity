using UnityEngine;
using Mediapipe.Unity.Sample.HandLandmarkDetection;

/// <summary>
/// 검지 끝(Landmark 8)이 BoxCollider2D 안에 들어온 상태에서 Pinch 동작을 수행하면
/// 스프라이트가 핀치 중간점을 따라 이동하면서 GIF 프레임 애니메이션을 재생합니다.
///
/// 사용법:
///   1. LOGO_GIF 오브젝트에 이 스크립트를 추가합니다.
///   2. Inspector > Sprite Animation > Frames 배열에 GIF에서 슬라이스된 스프라이트들을 순서대로 할당합니다.
///   3. 필요에 따라 Pinch Threshold와 Follow Speed를 조정합니다.
/// </summary>
[RequireComponent(typeof(BoxCollider2D))]
[RequireComponent(typeof(SpriteRenderer))]
public class LogoPinchDragger : MonoBehaviour
{
    [Header("Hand")]
    [Tooltip("false = 오른손 우선 (없으면 왼손), true = 왼손 고정")]
    public bool preferLeftHand = false;

    [Header("Pinch Threshold (normalized 0~1)")]
    [Tooltip("이 거리 이하일 때 핀치로 인식")]
    public float pinchOnThreshold = 0.06f;
    [Tooltip("이 거리 이상일 때 핀치 해제")]
    public float pinchOffThreshold = 0.10f;

    [Header("Follow")]
    public float followSpeed = 15f;
    public Camera cam;

    [Header("Sprite Animation")]
    [Tooltip("GIF를 스프라이트 시트로 변환한 프레임들을 순서대로 등록하세요")]
    public Sprite[] frames;
    [Tooltip("초당 프레임 수")]
    public float fps = 12f;

    [Header("Debug")]
    public bool debugLog = false;

    // 내부 상태
    private BoxCollider2D col;
    private SpriteRenderer sr;
    private Sprite defaultSprite;

    private bool isGrabbing = false;
    private Vector3 grabOffset;     // 스프라이트 위치 - 핀치 중간점 (월드)

    private int frameIndex = 0;
    private float frameTimer = 0f;

    void Awake()
    {
        col = GetComponent<BoxCollider2D>();
        sr = GetComponent<SpriteRenderer>();
        defaultSprite = sr.sprite;
        if (cam == null) cam = Camera.main;
    }

    void Update()
    {
        var dm = HandLandmarkDataManager.Instance;
        if (dm == null) return;

        // 오른손 우선, 없으면 왼손
        bool hasRight = dm.IsRightHandDetected;
        bool hasLeft  = dm.IsLeftHandDetected;

        if (!hasRight && !hasLeft)
        {
            EndGrab();
            return;
        }

        // 사용할 손 결정
        bool useLeft = preferLeftHand
            ? (hasLeft || !hasRight)
            : (!hasRight && hasLeft);

        Vector3 thumb3 = dm.GetThumbTip(useLeft);        // Landmark 4
        Vector3 index3 = dm.GetIndexFingerTip(useLeft);  // Landmark 8

        // 핀치 거리 (normalized 2D)
        float pinchDist = Vector2.Distance(
            new Vector2(thumb3.x, thumb3.y),
            new Vector2(index3.x, index3.y)
        );

        bool pinchActive   = pinchDist < pinchOnThreshold;
        bool pinchReleased = pinchDist > pinchOffThreshold;

        // 검지 끝 → 월드 좌표 (콜라이더 겹침 판정)
        Vector2 indexWorld = NormToWorld(new Vector2(index3.x, index3.y));

        // 핀치 중간점 → 월드 좌표 (스프라이트 이동 기준)
        Vector2 midNorm  = new Vector2((thumb3.x + index3.x) * 0.5f,
                                       (thumb3.y + index3.y) * 0.5f);
        Vector2 midWorld = NormToWorld(midNorm);

        if (!isGrabbing)
        {
            // 조건: 핀치 활성 + 검지 끝이 콜라이더 안에 있음
            if (pinchActive && col.OverlapPoint(indexWorld))
            {
                BeginGrab(midWorld);
            }
        }
        else
        {
            if (pinchReleased)
            {
                EndGrab();
            }
            else
            {
                // 핀치 중간점을 따라 스프라이트 이동
                Vector3 targetPos = (Vector3)midWorld + grabOffset;
                targetPos.z = transform.position.z;
                transform.position = Vector3.Lerp(
                    transform.position, targetPos, followSpeed * Time.deltaTime);

                // 프레임 애니메이션 재생
                AdvanceAnimation();
            }
        }
    }

    void BeginGrab(Vector2 midWorld)
    {
        isGrabbing  = true;
        grabOffset  = transform.position - (Vector3)midWorld;
        grabOffset.z = 0f;
        frameIndex  = 0;
        frameTimer  = 0f;

        if (debugLog)
            Debug.Log($"[LogoPinchDragger] Grab BEGIN at world={midWorld}");
    }

    void EndGrab()
    {
        if (!isGrabbing) return;
        isGrabbing = false;

        // 애니메이션 초기 프레임으로 복원
        if (frames != null && frames.Length > 0 && defaultSprite != null)
            sr.sprite = defaultSprite;

        if (debugLog)
            Debug.Log("[LogoPinchDragger] Grab END");
    }

    void AdvanceAnimation()
    {
        if (frames == null || frames.Length == 0) return;

        frameTimer += Time.deltaTime;
        float interval = 1f / Mathf.Max(fps, 0.1f);

        if (frameTimer >= interval)
        {
            frameTimer -= interval;
            frameIndex = (frameIndex + 1) % frames.Length;
            sr.sprite  = frames[frameIndex];
        }
    }

    /// <summary>
    /// MediaPipe normalized (0~1) 좌표를 Unity 월드 좌표로 변환합니다.
    /// MediaPipe는 y=0이 위, y=1이 아래이므로 (1 - y) 반전이 필요합니다.
    /// </summary>
    Vector2 NormToWorld(Vector2 norm)
    {
        if (cam == null) cam = Camera.main;

        float sx = norm.x * Screen.width;
        float sy = (1f - norm.y) * Screen.height;

        float zDist = Mathf.Abs(cam.transform.position.z - transform.position.z);
        Vector3 world = cam.ScreenToWorldPoint(new Vector3(sx, sy, zDist));
        return world;
    }
}
