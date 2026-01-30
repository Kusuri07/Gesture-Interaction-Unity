using UnityEngine;
using Mediapipe.Unity.Sample.HandLandmarkDetection;

[DefaultExecutionOrder(20000)] // DeformableSpriteMesh보다 늦게 실행되게
public class PinchCheekDeformer : MonoBehaviour
{
    [Header("Refs")]
    public Camera mainCamera;
    public Collider2D cheekArea;
    public DeformableSpriteMesh deformableMesh;
    public bool useLeftHand = true;

    [Header("Pinch")]
    public float pinchThreshold = 0.04f;

    [Header("Deform")]
    public float pullStrength = 2.5f;
    public float returnSpeed = 12f;

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
        if (cheekArea == null) return;

        // ✅ 메시가 아직 없거나(혹은 빈 메시)면 매 프레임 재바인딩 시도
        if (_mesh == null || _mesh.vertexCount == 0)
        {
            _meshTf = deformableMesh.targetMeshFilter.transform;
            _mesh = deformableMesh.targetMeshFilter.sharedMesh; // 생성된 메시에 붙음

            if (_mesh == null || _mesh.vertexCount == 0)
            {
                if (debugLog) Debug.Log("[PinchCheek] Waiting for mesh build...");
                return;
            }

            _baseVerts = _mesh.vertices;
            _workVerts = new Vector3[_baseVerts.Length];

            if (debugLog) Debug.Log($"[PinchCheek] Mesh bind OK. verts={_mesh.vertexCount}");
        }

        bool pinching = IsPinching(useLeftHand);
        Vector3 pinchWorld = GetPinchWorldPoint(useLeftHand);
        bool overCheekNow = cheekArea.OverlapPoint((Vector2)pinchWorld);

        if (debugLog)
            Debug.Log($"[PinchCheek] pinching={pinching}, overCheek={overCheekNow}");

        // 잡기 시작: 볼 위에서 핀치일 때만
        if (!_grabbing)
        {
            if (pinching && overCheekNow)
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
            Vector3 center = cheekArea.bounds.center;
            Vector3 dir = (pinchWorld - center);
            if (dir.sqrMagnitude < 1e-8f) dir = Vector3.right;
            dir.Normalize();

            float amount = Vector3.Dot((pinchWorld - _grabStartWorld), dir);
            Vector3 deltaWorld = dir * amount;

            Vector3 deltaLocal = _meshTf.InverseTransformVector(deltaWorld) * pullStrength;

            for (int i = 0; i < _baseVerts.Length; i++)
            {
                Vector3 vWorld = _meshTf.TransformPoint(_baseVerts[i]);

                // 콜라이더 밖은 고정
                if (!cheekArea.OverlapPoint((Vector2)vWorld))
                {
                    _workVerts[i] = _baseVerts[i];
                    continue;
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
            // 복귀
            var cur = _mesh.vertices;
            for (int i = 0; i < cur.Length; i++)
                cur[i] = Vector3.Lerp(cur[i], _baseVerts[i], Time.deltaTime * returnSpeed);

            _mesh.vertices = cur;
            _mesh.RecalculateBounds();
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
