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

    void Start()
    {
        o = Instantiate(obj, Vector3.zero, Quaternion.identity);
        o.transform.parent = this.gameObject.transform;
        mesh = o.GetComponent<MeshFilter>().mesh;

        intermediateResultsBuffer = new ComputeBuffer(2, sizeof(float) * 3); // Min, Max 값 저장
    }

    void Update()
    {
        Vector3[] vertices = mesh.vertices;
        int vertexCount = vertices.Length;
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
        posBuffer = new ComputeBuffer(vertexCount, sizeof(float) * 3);
        posBuffer.SetData(positions);


        int kernelHandle = computeShader.FindKernel("CSMain");
        computeShader.SetBuffer(kernelHandle, "Vertices", posBuffer);
        computeShader.SetBuffer(kernelHandle, "IntermediateResults", intermediateResultsBuffer);

        computeShader.Dispatch(kernelHandle, Mathf.CeilToInt(vertexCount / 1024f), 1, 1);

        kernelHandle = computeShader.FindKernel("Reduce");
        computeShader.Dispatch(kernelHandle, 1, 1, 1);

        // 결과 읽기
        Vector3[] results = new Vector3[2];
        intermediateResultsBuffer.GetData(results);
        Vector3 min = results[0];
        Vector3 max = results[1];

        Debug.Log("AABB Min: " + min);
        Debug.Log("AABB Max: " + max);

        posBuffer.Release();
    }

    private void OnDestroy()
    {
        if (intermediateResultsBuffer != null)
        {
            intermediateResultsBuffer.Release();
        }
    }
}
