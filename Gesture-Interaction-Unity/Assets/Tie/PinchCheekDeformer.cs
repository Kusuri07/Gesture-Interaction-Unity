using UnityEngine;
using Mediapipe.Unity.Sample.HandLandmarkDetection;

[DefaultExecutionOrder(20000)] // DeformableSpriteMesh보다 늦게 실행되게
public class PinchCheekDeformer : MonoBehaviour
{
    [Header("Refs")]
    public Camera mainCamera;
    public Collider2D cheekArea;
    public DeformableSpriteMesh deformableMesh;
    [Tooltip("true = 왼손만, false = 오른손만, 아래 useBothHands 로 양손 모두 사용 가능")]
    public bool useLeftHand = true;
    [Tooltip("체크 시 왼손·오른손 핀치 모두 인식")]
    public bool useBothHands = true;

    [Header("Center (Bone 기준 설정)")]
    [Tooltip("뺨 중심이 되는 뼈(bone_1, bone_2 등). 지정하면 cheek_area 대신 이 위치를 기준으로 당깁니다.")]
    public Transform cheekCenter;
    [Tooltip("cheekCenter 주변에서만 변형되는 반경(월드). 0 이하면 전체 메시에 적용.")]
    public float cheekRadius = 0f;
    [Tooltip("왜곡 범위 줄이기. 1=전체 반경, 0.5=반경 절반만. 작을수록 왜곡 범위 감소.")]
    [Range(0.15f, 1f)]
    public float deformRangeScale = 0.15f;

    [Header("Pinch")]
    public float pinchThreshold = 0.04f;

    [Header("Deform")]
    public float pullStrength = 4.0f;
    public float returnSpeed = 10f;

    [Tooltip("0이면 부드러운 가장자리 비활성(권장: 먼저 0으로 테스트)")]
    public float softEdgeWorld = 0f;

    [Header("Debug")]
    public bool debugLog = true;

    bool _grabbing;
    Vector3 _grabStartWorld;

    Mesh _mesh;
    Transform _meshTf;

    Vector3[] _baseVerts;
    Vector3[] _workVerts;

    void Start()
    {
        if (mainCamera == null) mainCamera = Camera.main;
        if (deformableMesh == null) deformableMesh = GetComponent<DeformableSpriteMesh>();
    }

    void LateUpdate()
    {
        if (HandLandmarkDataManager.Instance == null) return;
        if (deformableMesh == null || deformableMesh.targetMeshFilter == null) return;

        // ✅ 메시가 아직 없거나(혹은 빈 메시), 또는 버텍스 수가 달라졌으면 재바인딩
        if (_mesh == null || _mesh.vertexCount == 0 ||
            (_baseVerts != null && _mesh.vertexCount != _baseVerts.Length))
        {
            _meshTf = deformableMesh.targetMeshFilter.transform;
            _mesh = deformableMesh.targetMeshFilter.sharedMesh;

            if (_mesh == null || _mesh.vertexCount == 0)
            {
                if (debugLog) Debug.Log("[PinchCheek] Waiting for mesh build...");
                return;
            }

            _baseVerts = _mesh.vertices;
            _workVerts = new Vector3[_baseVerts.Length];
            _grabbing = false;

            if (debugLog) Debug.Log($"[PinchCheek] Mesh bind OK. verts={_mesh.vertexCount}");
        }

        // 왼손/오른손 둘 다 인식 (useBothHands) 또는 한 손만
        bool leftPinch = IsPinching(true);
        bool rightPinch = IsPinching(false);
        bool pinching = useBothHands ? (leftPinch || rightPinch) : (useLeftHand ? leftPinch : rightPinch);
        bool useLeftForPos = leftPinch || (!rightPinch && useLeftHand);
        Vector3 pinchWorld = pinching ? GetPinchWorldPoint(useLeftForPos) : Vector3.zero;

        // cheekArea 가 있으면 그대로 사용, 없으면 cheekCenter 기준 거리로만 디버그용 판정
        bool overCheekNow = false;
        Vector3 center = cheekCenter != null
            ? cheekCenter.position
            : (cheekArea != null ? (Vector3)cheekArea.bounds.center : _meshTf.position);

        if (cheekArea != null)
            overCheekNow = cheekArea.OverlapPoint((Vector2)pinchWorld);
        else if (cheekCenter != null && cheekRadius > 0f)
            overCheekNow = (pinchWorld - center).sqrMagnitude <= cheekRadius * cheekRadius;
        else
            overCheekNow = true;

        if (debugLog)
            Debug.Log($"[PinchCheek] pinching={pinching}, overCheek={overCheekNow}");

        // 잡기 시작: 핀치가 발생하면 어디서나 시작 (다만 실제 변형은 cheekArea 내부 버텍스에만 적용)
        if (!_grabbing)
        {
            if (pinching)
            {
                _grabbing = true;
                _grabStartWorld = pinchWorld;

                // 시작 순간 메시를 기준으로 저장
                _baseVerts = _mesh.vertices;
                if (_workVerts == null || _workVerts.Length != _baseVerts.Length)
                    _workVerts = new Vector3[_baseVerts.Length];

                if (debugLog) Debug.Log("[PinchCheek] GRAB START");
            }
            else
            {
                return;
            }
        }

        // 핀치 놓음 -> 복귀
        if (_grabbing && !pinching)
        {
            _grabbing = false;
            if (debugLog) Debug.Log("[PinchCheek] GRAB END");
        }

        if (_grabbing)
        {
            // ----- 바깥 방향 당김(볼 중심→손 방향) -----
            Vector3 dir = (pinchWorld - center);
            if (dir.sqrMagnitude < 1e-8f) dir = Vector3.right;
            dir.Normalize();

            float amount = Vector3.Dot((pinchWorld - _grabStartWorld), dir);
            Vector3 deltaWorld = dir * amount;

            Vector3 deltaLocal = _meshTf.InverseTransformVector(deltaWorld) * pullStrength;

            for (int i = 0; i < _baseVerts.Length; i++)
            {
                Vector3 vWorld = _meshTf.TransformPoint(_baseVerts[i]);

                // 변형 영역 마스크
                if (cheekArea != null)
                {
                    // 콜라이더 밖은 고정
                    if (!cheekArea.OverlapPoint((Vector2)vWorld))
                    {
                        _workVerts[i] = _baseVerts[i];
                        continue;
                    }
                }
                else if (cheekCenter != null && cheekRadius > 0f)
                {
                    // 뼈 중심에서 (cheekRadius * deformRangeScale) 밖은 고정 → deformRangeScale 로 왜곡 범위 조절
                    float effectiveRadius = cheekRadius * Mathf.Clamp01(deformRangeScale);
                    float distFromCenter = Vector3.Distance(vWorld, center);
                    if (distFromCenter > effectiveRadius)
                    {
                        _workVerts[i] = _baseVerts[i];
                        continue;
                    }
                }

                float w = 1f;

                // (선택) soft edge: 0이면 비활성
                if (softEdgeWorld > 0f)
                {
                    float dist = Vector3.Distance(vWorld, center);
                    float t = Mathf.Clamp01(1f - (dist / softEdgeWorld));
                    w = t * t * (3f - 2f * t);
                }

                _workVerts[i] = _baseVerts[i] + deltaLocal * w;
            }

            _mesh.vertices = _workVerts;
            _mesh.RecalculateBounds();
        }
        else
        {
            // 놓으면 즉시 원래 형태로 복귀
            if (_baseVerts != null && _baseVerts.Length == _mesh.vertexCount)
            {
                _mesh.vertices = _baseVerts;
                _mesh.RecalculateBounds();
            }
        }
    }

    bool IsPinching(bool left)
    {
        if (left && !HandLandmarkDataManager.Instance.IsLeftHandDetected) return false;
        if (!left && !HandLandmarkDataManager.Instance.IsRightHandDetected) return false;

        Vector3 thumb = HandLandmarkDataManager.Instance.GetThumbTip(left);
        Vector3 index = HandLandmarkDataManager.Instance.GetIndexFingerTip(left);

        float d = Vector2.Distance(new Vector2(thumb.x, thumb.y), new Vector2(index.x, index.y));
        return d < pinchThreshold;
    }

    Vector3 GetPinchWorldPoint(bool left)
    {
        Vector3 thumb = HandLandmarkDataManager.Instance.GetThumbTip(left);
        Vector3 index = HandLandmarkDataManager.Instance.GetIndexFingerTip(left);

        Vector2 c01 = new Vector2((thumb.x + index.x) * 0.5f, (thumb.y + index.y) * 0.5f);

        Vector3 screen = new Vector3(c01.x * Screen.width, (1f - c01.y) * Screen.height, 0f);
        screen.z = Mathf.Abs(mainCamera.transform.position.z - _meshTf.position.z);

        return mainCamera.ScreenToWorldPoint(screen);
    }
}
