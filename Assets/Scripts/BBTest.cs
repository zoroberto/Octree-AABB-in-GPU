using UnityEngine;

public class BBTest : MonoBehaviour
{
    [Header("Compute Shader")]
    public ComputeShader computeShader;

    [Header("Object")]
    public GameObject obj;

    private GameObject o;

    private ComputeBuffer posBuffer;
    private ComputeBuffer intermediateResultsBuffer;
    private Vector3[] positions;
    private Mesh mesh;

    private Vector3 min;
    private Vector3 max;

    int updateAABBKernel;
    int mergeAABBKernel;
    int vertexCount;

    int index = 0;

    void Start()
    {
        o = Instantiate(obj, Vector3.zero, Quaternion.identity);
        o.transform.parent = this.gameObject.transform;
        mesh = o.GetComponent<MeshFilter>().mesh;

        findKernelID();
    }

    void findKernelID()
    {
        updateAABBKernel = computeShader.FindKernel("UpdateAABBGroup");
        mergeAABBKernel = computeShader.FindKernel("MergeAABB");
    }

    void InitBuffers()
    {
        Vector3[] vertices = mesh.vertices;
        vertexCount = vertices.Length;
        positions = new Vector3[vertexCount];

        Matrix4x4 localToWorld = o.transform.localToWorldMatrix;
        for (int i = 0; i < vertexCount; i++)
        {
            positions[i] = localToWorld.MultiplyPoint3x4(vertices[i]);
        }

        // 버퍼 생성 및 설정
        if (posBuffer != null)
        {
            posBuffer.Release();
        }
        if (intermediateResultsBuffer != null)
        {
            intermediateResultsBuffer.Release();
        }
        posBuffer = new ComputeBuffer(vertexCount, sizeof(float) * 3);
        posBuffer.SetData(positions);
        intermediateResultsBuffer = new ComputeBuffer(2, sizeof(float) * 3); // Min, Max 값 저장

        computeShader.SetBuffer(updateAABBKernel, "Vertices", posBuffer);
        computeShader.SetBuffer(updateAABBKernel, "IntermediateResults", intermediateResultsBuffer);
        computeShader.SetBuffer(mergeAABBKernel, "IntermediateResults", intermediateResultsBuffer);
    }

    void DisPatchSolver()
    {
        computeShader.Dispatch(updateAABBKernel, Mathf.CeilToInt(vertexCount / 1024f), 1, 1);
        computeShader.Dispatch(mergeAABBKernel, 1, 1, 1);
    }

    void UpdatePosition()
    {
        if (index++ % 1000 == 0)
        {
            index = 1;
            o.transform.position = new Vector3(Random.Range(-10.0f, 10.0f), Random.Range(0.0f, 10.0f), Random.Range(-10.0f, 10.0f));
        }
    }

    void Update()
    {
        UpdatePosition();
        InitBuffers();
        DisPatchSolver();

        // 결과 읽기
        Vector3[] results = new Vector3[2];
        intermediateResultsBuffer.GetData(results);
        min = results[0];
        max = results[1];
    }

    private void OnDestroy()
    {
        if (intermediateResultsBuffer != null) intermediateResultsBuffer.Release();
        if (posBuffer != null) posBuffer.Release();
    }

    void OnDrawGizmos()
    {
        Gizmos.color = Color.green; // 박스 색상 설정
        Gizmos.DrawWireCube((min + max) * 0.5f, max - min);
    }
}
