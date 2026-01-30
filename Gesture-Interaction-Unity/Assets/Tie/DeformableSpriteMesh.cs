using UnityEngine;

public class DeformableSpriteMesh : MonoBehaviour
{
    [Header("Output Mesh Target (child object)")]
    public MeshFilter targetMeshFilter; // mishojo_mesh의 MeshFilter 연결

    [Header("Grid Density")]
    [Range(2, 200)] public int gridX = 30;
    [Range(2, 200)] public int gridY = 30;

    [Header("Material")]
    public Shader shaderOverride; // 비워두면 Unlit/Transparent -> Sprites/Default 순으로 찾음

    private SpriteRenderer _sr;
    private MeshRenderer _targetMr;

    public Mesh MeshInstance => targetMeshFilter != null ? targetMeshFilter.mesh : null;

    private void Awake()
    {
        _sr = GetComponent<SpriteRenderer>();
    }

    private void Start()
    {
        if (_sr == null || _sr.sprite == null)
        {
            Debug.LogError("[DeformableSpriteMesh] SpriteRenderer or Sprite is missing.");
            return;
        }

        if (targetMeshFilter == null)
        {
            Debug.LogError("[DeformableSpriteMesh] targetMeshFilter is not assigned.");
            return;
        }

        _targetMr = targetMeshFilter.GetComponent<MeshRenderer>();
        if (_targetMr == null)
        {
            _targetMr = targetMeshFilter.gameObject.AddComponent<MeshRenderer>();
        }

        BuildMeshFromSprite();
        ApplyMaterialFromSpriteTexture();

        // 원본 스프라이트 렌더러는 끄고 MeshRenderer가 대신 그림
        _sr.enabled = false;
    }

    private void ApplyMaterialFromSpriteTexture()
    {
        var sprite = _sr.sprite;
        var tex = sprite.texture;

        Shader sh = shaderOverride;
        if (sh == null) sh = Shader.Find("Unlit/Transparent");
        if (sh == null) sh = Shader.Find("Sprites/Default");

        if (sh == null)
        {
            Debug.LogError("[DeformableSpriteMesh] Could not find a suitable shader (Unlit/Transparent or Sprites/Default).");
            return;
        }

        if (_targetMr.sharedMaterial == null || _targetMr.sharedMaterial.shader != sh)
        {
            _targetMr.sharedMaterial = new Material(sh);
        }

        // Unlit/Transparent는 보통 _MainTex, Sprites/Default도 _MainTex를 씀
        if (_targetMr.sharedMaterial.HasProperty("_MainTex"))
        {
            _targetMr.sharedMaterial.SetTexture("_MainTex", tex);
        }
        else
        {
            _targetMr.sharedMaterial.mainTexture = tex;
        }
    }

    private void BuildMeshFromSprite()
    {
        var sprite = _sr.sprite;

        var mesh = new Mesh();
        mesh.name = "DeformableSpriteMesh";

        // 스프라이트 로컬 크기
        var b = sprite.bounds; // local space
        float w = b.size.x;
        float h = b.size.y;

        // pivot 보정
        Vector2 pivot01 = sprite.pivot / sprite.rect.size; // 0~1
        float left = -w * pivot01.x;
        float bottom = -h * pivot01.y;

        // UV (textureRect 기준)
        Rect tr = sprite.textureRect;
        Texture2D tex = sprite.texture;
        Vector2 uvMin = new Vector2(tr.xMin / tex.width, tr.yMin / tex.height);
        Vector2 uvMax = new Vector2(tr.xMax / tex.width, tr.yMax / tex.height);

        int vxCount = (gridX + 1) * (gridY + 1);
        Vector3[] verts = new Vector3[vxCount];
        Vector2[] uvs = new Vector2[vxCount];
        int[] tris = new int[gridX * gridY * 6];

        int v = 0;
        for (int y = 0; y <= gridY; y++)
        {
            float ty = (float)y / gridY;
            for (int x = 0; x <= gridX; x++)
            {
                float tx = (float)x / gridX;

                float px = left + tx * w;
                float py = bottom + ty * h;

                verts[v] = new Vector3(px, py, 0f);
                uvs[v] = new Vector2(Mathf.Lerp(uvMin.x, uvMax.x, tx), Mathf.Lerp(uvMin.y, uvMax.y, ty));
                v++;
            }
        }

        int t = 0;
        for (int y = 0; y < gridY; y++)
        {
            for (int x = 0; x < gridX; x++)
            {
                int i0 = y * (gridX + 1) + x;
                int i1 = i0 + 1;
                int i2 = i0 + (gridX + 1);
                int i3 = i2 + 1;

                tris[t++] = i0; tris[t++] = i2; tris[t++] = i1;
                tris[t++] = i1; tris[t++] = i2; tris[t++] = i3;
            }
        }

        mesh.vertices = verts;
        mesh.uv = uvs;
        mesh.triangles = tris;
        mesh.RecalculateBounds();

        targetMeshFilter.sharedMesh = mesh;
    }
}
