using UnityEngine;
using Live2D.Cubism.Core;
using Live2D.Cubism.Framework;
using Mediapipe.Unity.Sample.HandLandmarkDetection;

/// <summary>
/// 흰색 캐릭터의 뺨을 Pinch 제스처로 잡아당기는 컨트롤러
/// HandLandmarkerRunner의 Pinch 감지 로직을 참고하여 작성
/// </summary>
public class WhiteCharacterCheekController : MonoBehaviour
{
    [Header("=== Live2D 모델 참조 ===")]
    [SerializeField] private CubismModel cubismModel;

    [Header("=== 카메라 설정 ===")]
    [SerializeField] private Camera mainCamera;
    [Tooltip("캐릭터와 카메라 사이 Z 거리 (월드 좌표 변환용)")]
    [SerializeField] private float cameraDistance = 10f;

    [Header("=== Collider 참조 ===")]
    [SerializeField] private Collider2D leftCheekCollider;
    [SerializeField] private Collider2D rightCheekCollider;

    [Header("=== 뺨 중심 위치 (Transform) ===")]
    [SerializeField] private Transform leftCheekCenter;
    [SerializeField] private Transform rightCheekCenter;

    [Header("=== Pinch 감지 설정 (HandLandmarkerRunner 참조) ===")]
    [Tooltip("Pinch 시작 인식 임계값 (3D 거리)")]
    [SerializeField] private float pinchOnThreshold = 0.06f;

    [Tooltip("Pinch 해제 인식 임계값 (3D 거리, ON보다 커야 함)")]
    [SerializeField] private float pinchOffThreshold = 0.09f;

    [Header("=== 뺨 당기기 설정 ===")]
    [Tooltip("최대 당길 수 있는 거리 (Unity units)")]
    [SerializeField] private float maxPullDistance = 1.5f;

    [Tooltip("당기기 민감도 (1.0 = 기본)")]
    [SerializeField] private float pullSensitivity = 1.2f;

    [Tooltip("부드러운 이동 시간 (SmoothDamp)")]
    [SerializeField] private float smoothTime = 0.06f;

    [Tooltip("탄성 복원 속도 (0~1, 작을수록 빠름)")]
    [SerializeField] private float returnSpeed = 0.3f;

    [Header("=== 블러시 효과 ===")]
    [SerializeField] private bool enableBlush = true;
    [SerializeField] private float blushDuration = 2.0f;

    [Header("=== 디버그 ===")]
    [SerializeField] private bool showDebugGizmos = true;
    [SerializeField] private bool logPinchEvents = true;

    // ===================================
    // Private Variables
    // ===================================

    // 뺨 데이터 구조체
    private struct CheekData
    {
        public bool isPinching;           // 현재 핀치 중인가?
        public bool wasInCollider;        // 이전 프레임에 Collider 안에 있었는가?
        public Vector3 pinchStartWorld;   // Pinch 시작 위치 (월드 좌표)
        public Vector2 currentDeform;     // 현재 변형량 (X, Y)
        public Vector2 targetDeform;      // 목표 변형량
        public Vector2 velocity;          // SmoothDamp 속도 버퍼
        public float blushTimer;          // 블러시 효과 타이머
    }

    private CheekData leftCheek;
    private CheekData rightCheek;

    // Live2D 파라미터 캐시
    private CubismParameter paramCheekLPullX;
    private CubismParameter paramCheekLPullY;
    private CubismParameter paramCheekRPullX;
    private CubismParameter paramCheekRPullY;
    private CubismParameter paramCheekBlush;

    // 캐시된 화면 크기
    private float cachedScreenWidth;
    private float cachedScreenHeight;

    // ===================================
    // Unity Lifecycle
    // ===================================

    void Start()
    {
        // 자동 참조 설정
        if (cubismModel == null)
            cubismModel = GetComponent<CubismModel>();

        if (mainCamera == null)
            mainCamera = Camera.main;

        // 화면 크기 캐싱
        cachedScreenWidth = Screen.width;
        cachedScreenHeight = Screen.height;

        // Live2D 파라미터 찾기
        FindLive2DParameters();

        // 초기화
        leftCheek = new CheekData();
        rightCheek = new CheekData();

        Debug.Log("[WhiteCharacterCheekController] 초기화 완료");
        Debug.Log($"  - Camera Distance: {cameraDistance}");
        Debug.Log($"  - Pinch Threshold: {pinchOnThreshold} ~ {pinchOffThreshold}");
        Debug.Log($"  - Max Pull Distance: {maxPullDistance}");
    }

    void Update()
    {
        // HandLandmarkDataManager 체크
        if (HandLandmarkDataManager.Instance == null)
            return;

        // 왼쪽 뺨 처리 (오른손 사용)
        ProcessCheek(
            ref leftCheek,
            leftCheekCollider,
            leftCheekCenter,
            true,  // useRightHand
            paramCheekLPullX,
            paramCheekLPullY
        );

        // 오른쪽 뺨 처리 (왼손 사용)
        ProcessCheek(
            ref rightCheek,
            rightCheekCollider,
            rightCheekCenter,
            false, // useRightHand = false (왼손)
            paramCheekRPullX,
            paramCheekRPullY
        );

        // 블러시 효과 업데이트
        UpdateBlushEffect();
    }

    // ===================================
    // Core Logic
    // ===================================

    /// <summary>
    /// 개별 뺨 처리 (HandLandmarkerRunner의 Pinch 로직 참조)
    /// </summary>
    private void ProcessCheek(
        ref CheekData cheekData,
        Collider2D collider,
        Transform cheekCenter,
        bool useRightHand,
        CubismParameter paramX,
        CubismParameter paramY)
    {
        if (collider == null || cheekCenter == null)
            return;

        var dataManager = HandLandmarkDataManager.Instance;

        // 손 감지 여부
        bool handDetected = useRightHand
            ? dataManager.IsRightHandDetected
            : dataManager.IsLeftHandDetected;

        if (!handDetected)
        {
            // 손이 감지되지 않으면 복원
            ReleaseCheek(ref cheekData);
            ApplyDeformToParameters(cheekData, paramX, paramY);
            return;
        }

        // HandLandmarkerRunner 스타일로 Pinch 거리 계산
        float pinchDist3D = CalculatePinchDistance3D(useRightHand);

        // Pinch 상태 업데이트 (Hysteresis 적용)
        bool wasPinching = cheekData.isPinching;

        if (!cheekData.isPinching && pinchDist3D < pinchOnThreshold)
        {
            // Pinch 시작
            cheekData.isPinching = true;

            if (logPinchEvents)
                Debug.Log($"[Cheek] 🟢 Pinch START ({(useRightHand ? "Right" : "Left")} hand) dist={pinchDist3D:F3}");
        }
        else if (cheekData.isPinching && pinchDist3D > pinchOffThreshold)
        {
            // Pinch 해제
            cheekData.isPinching = false;

            if (logPinchEvents)
                Debug.Log($"[Cheek] 🟡 Pinch RELEASE ({(useRightHand ? "Right" : "Left")} hand) dist={pinchDist3D:F3}");
        }

        // Pinch 중인 경우
        if (cheekData.isPinching)
        {
            // Pinch 중심점 (Thumb + Index 중간) → 월드 좌표
            Vector3 pinchWorldPos = GetPinchWorldPosition(useRightHand);

            // Collider 안에 있는지 확인
            Vector2 pinchScreen2D = mainCamera.WorldToScreenPoint(pinchWorldPos);
            Vector3 colliderWorldPos = collider.transform.position;
            Vector2 colliderScreen2D = mainCamera.WorldToScreenPoint(colliderWorldPos);

            bool isInCollider = collider.OverlapPoint(colliderScreen2D);

            // Pinch 시작 순간 → 시작 위치 저장
            if (!wasPinching && isInCollider)
            {
                cheekData.pinchStartWorld = pinchWorldPos;
                cheekData.wasInCollider = true;

                if (logPinchEvents)
                    Debug.Log($"[Cheek] ✅ Pinch in Collider! Start position: {pinchWorldPos}");
            }

            // Collider 안에서 Pinch 시작했다면 → 변형 계산
            if (cheekData.wasInCollider)
            {
                Vector3 pullVector = pinchWorldPos - cheekData.pinchStartWorld;
                pullVector *= pullSensitivity;

                // 최대 거리 제한
                if (pullVector.magnitude > maxPullDistance)
                    pullVector = pullVector.normalized * maxPullDistance;

                cheekData.targetDeform = new Vector2(pullVector.x, pullVector.y);

                // 블러시 타이머 초기화
                cheekData.blushTimer = blushDuration;
            }
        }
        else
        {
            // Pinch 해제 → 복원
            ReleaseCheek(ref cheekData);
        }

        // SmoothDamp로 부드럽게 적용
        cheekData.currentDeform = Vector2.SmoothDamp(
            cheekData.currentDeform,
            cheekData.targetDeform,
            ref cheekData.velocity,
            smoothTime
        );

        // Live2D 파라미터에 적용
        ApplyDeformToParameters(cheekData, paramX, paramY);
    }

    /// <summary>
    /// HandLandmarkerRunner 참조: Thumb(4) - Index(8) 3D 거리 계산
    /// </summary>
    private float CalculatePinchDistance3D(bool useRightHand)
    {
        var dataManager = HandLandmarkDataManager.Instance;

        // Normalized Landmarks 가져오기
        var landmarks = useRightHand
            ? dataManager.RightHandLandmarks
            : dataManager.LeftHandLandmarks;

        if (landmarks == null || landmarks.Count < 9)
            return 999f; // 매우 큰 값 (Pinch 아님)

        // Thumb Tip (4번), Index Tip (8번)
        var thumb = landmarks[4];
        var index = landmarks[8];

        // 3D Euclidean Distance
        float dx = thumb.x - index.x;
        float dy = thumb.y - index.y;
        float dz = thumb.z - index.z;

        return Mathf.Sqrt(dx * dx + dy * dy + dz * dz);
    }

    /// <summary>
    /// HandLandmarkerRunner 참조: Pinch 중심점의 월드 좌표 계산
    /// </summary>
    private Vector3 GetPinchWorldPosition(bool useRightHand)
    {
        var dataManager = HandLandmarkDataManager.Instance;

        var landmarks = useRightHand
            ? dataManager.RightHandLandmarks
            : dataManager.LeftHandLandmarks;

        if (landmarks == null || landmarks.Count < 9)
            return Vector3.zero;

        var thumb = landmarks[4];
        var index = landmarks[8];

        // 중간점 (Normalized 0~1)
        float midX = (thumb.x + index.x) * 0.5f;
        float midY = (thumb.y + index.y) * 0.5f;

        // Screen 좌표로 변환 (Y축 반전!)
        float screenX = midX * cachedScreenWidth;
        float screenY = (1f - midY) * cachedScreenHeight; // ⚠️ 중요: Y 반전

        // 월드 좌표로 변환
        Vector3 screenPoint = new Vector3(screenX, screenY, cameraDistance);
        return mainCamera.ScreenToWorldPoint(screenPoint);
    }

    /// <summary>
    /// 뺨 해제 → 탄성 복원
    /// </summary>
    private void ReleaseCheek(ref CheekData cheekData)
    {
        cheekData.isPinching = false;
        cheekData.wasInCollider = false;

        // 탄성 복원 (EaseOut)
        cheekData.targetDeform = Vector2.Lerp(
            cheekData.targetDeform,
            Vector2.zero,
            1f - returnSpeed
        );
    }

    /// <summary>
    /// 변형량을 Live2D 파라미터에 적용
    /// </summary>
    private void ApplyDeformToParameters(
        CheekData cheekData,
        CubismParameter paramX,
        CubismParameter paramY)
    {
        if (paramX == null || paramY == null)
            return;

        // 정규화: -1 ~ 1 범위로 변환
        float normalizedX = Mathf.Clamp(cheekData.currentDeform.x / maxPullDistance, -1f, 1f);
        float normalizedY = Mathf.Clamp(cheekData.currentDeform.y / maxPullDistance, -1f, 1f);

        // Live2D 파라미터 범위 (-30 ~ 30)로 매핑
        paramX.Value = normalizedX * 30f;
        paramY.Value = normalizedY * 30f;
    }

    /// <summary>
    /// 블러시 효과 업데이트
    /// </summary>
    private void UpdateBlushEffect()
    {
        if (!enableBlush || paramCheekBlush == null)
            return;

        // 양쪽 뺨 중 더 큰 타이머 사용
        float maxTimer = Mathf.Max(leftCheek.blushTimer, rightCheek.blushTimer);

        if (maxTimer > 0f)
        {
            leftCheek.blushTimer -= Time.deltaTime;
            rightCheek.blushTimer -= Time.deltaTime;

            // 블러시 강도 (0 ~ 1)
            float blushIntensity = Mathf.Clamp01(maxTimer / blushDuration);
            paramCheekBlush.Value = blushIntensity;
        }
        else
        {
            paramCheekBlush.Value = 0f;
        }
    }

    // ===================================
    // Initialization
    // ===================================

    /// <summary>
    /// Live2D 파라미터 찾기
    /// </summary>
    private void FindLive2DParameters()
    {
        if (cubismModel == null)
        {
            Debug.LogError("[WhiteCharacterCheekController] CubismModel이 없습니다!");
            return;
        }

        var parameters = cubismModel.Parameters;

        // 왼쪽 뺨
        paramCheekLPullX = FindParameter(parameters, "ParamCheekLPullX");
        paramCheekLPullY = FindParameter(parameters, "ParamCheekLPullY");

        // 오른쪽 뺨
        paramCheekRPullX = FindParameter(parameters, "ParamCheekRPullX");
        paramCheekRPullY = FindParameter(parameters, "ParamCheekRPullY");

        // 블러시
        paramCheekBlush = FindParameter(parameters, "ParamCheekBlush");

        // 로그
        Debug.Log($"[Live2D Parameters]");
        Debug.Log($"  - ParamCheekLPullX: {(paramCheekLPullX != null ? "✅" : "❌")}");
        Debug.Log($"  - ParamCheekLPullY: {(paramCheekLPullY != null ? "✅" : "❌")}");
        Debug.Log($"  - ParamCheekRPullX: {(paramCheekRPullX != null ? "✅" : "❌")}");
        Debug.Log($"  - ParamCheekRPullY: {(paramCheekRPullY != null ? "✅" : "❌")}");
        Debug.Log($"  - ParamCheekBlush: {(paramCheekBlush != null ? "✅" : "❌")}");
    }

    /// <summary>
    /// 파라미터 이름으로 찾기
    /// </summary>
    private CubismParameter FindParameter(CubismParameter[] parameters, string name)
    {
        foreach (var param in parameters)
        {
            if (param.Id == name)
                return param;
        }

        Debug.LogWarning($"[Live2D] 파라미터를 찾을 수 없음: {name}");
        return null;
    }

    // ===================================
    // Debug Gizmos
    // ===================================

    void OnDrawGizmos()
    {
        if (!showDebugGizmos)
            return;

        // 왼쪽 뺨
        if (leftCheekCollider != null)
        {
            Gizmos.color = leftCheek.isPinching ? Color.green : Color.yellow;
            Gizmos.DrawWireSphere(leftCheekCollider.transform.position, 0.1f);

            if (leftCheek.isPinching && leftCheek.wasInCollider)
            {
                Gizmos.color = Color.red;
                Vector3 endPos = leftCheekCollider.transform.position +
                                 new Vector3(leftCheek.currentDeform.x, leftCheek.currentDeform.y, 0f);
                Gizmos.DrawLine(leftCheekCollider.transform.position, endPos);
            }
        }

        // 오른쪽 뺨
        if (rightCheekCollider != null)
        {
            Gizmos.color = rightCheek.isPinching ? Color.green : Color.yellow;
            Gizmos.DrawWireSphere(rightCheekCollider.transform.position, 0.1f);

            if (rightCheek.isPinching && rightCheek.wasInCollider)
            {
                Gizmos.color = Color.red;
                Vector3 endPos = rightCheekCollider.transform.position +
                                 new Vector3(rightCheek.currentDeform.x, rightCheek.currentDeform.y, 0f);
                Gizmos.DrawLine(rightCheekCollider.transform.position, endPos);
            }
        }
    }
}