using UnityEngine;
using System.Collections.Generic;

public class BBTest : MonoBehaviour
{
    [Header("Compute Shader")]
    public ComputeShader computeShader;

    [Header("Object")]
    public GameObject obj;

    [Header("Random Loop Count")]
    public int num_of_loop = 1000;

    [Header("Object Count")]
    public int num_of_obj = 1;

    [Header("Debug Mode")]
    public bool debugMode = true;



    private ComputeBuffer posBuffer;
    private ComputeBuffer intermediateResultsBuffer;
    private ComputeBuffer objectIndexBuffer;
    private Vector3[] positions;
    private Vector2Int[] indicies;


    public GameObject[] o;
    public Mesh[] mesh_list;

    public Vector3[] min;
    public Vector3[] max;

    int updateAABBKernel;
    int vertexCount;

    int index = 0;

    void Start()
    {
        o = new GameObject[num_of_obj];
        mesh_list = new Mesh[num_of_obj];
        min = new Vector3[num_of_obj];
        max = new Vector3[num_of_obj];

        for (int i = 0; i < num_of_obj; i++)
        {
            var _o = Instantiate(obj, new Vector3(Random.Range(-10.0f, 10.0f), Random.Range(0.0f, 10.0f), Random.Range(-10.0f, 10.0f)), Quaternion.identity);
            _o.transform.parent = this.transform;
            o[i] = _o;
            mesh_list[i] = _o.GetComponent<MeshFilter>().mesh;
        }

        findKernelID();
    }

    void findKernelID()
    {
        updateAABBKernel = computeShader.FindKernel("UpdateAABBGroup");
    }

    void InitBuffers()
    {
        List<Vector3> vertices = new List<Vector3>();
        indicies = new Vector2Int[num_of_obj];

        int st_index = 0;
        for (int i = 0; i < num_of_obj; i++)
        {
            var _o = o[i];
            var _vertices = mesh_list[i].vertices;
            var _vertexCount = mesh_list[i].vertexCount;
            Matrix4x4 localToWorld = _o.transform.localToWorldMatrix;
            for (int j = 0; j < _vertexCount; j++)
            {
                vertices.Add(localToWorld.MultiplyPoint3x4(_vertices[j]));
            }
            indicies[i] = new Vector2Int(st_index, st_index + _vertexCount);
            st_index += _vertexCount;
        }
        vertexCount = vertices.Count;
        positions = vertices.ToArray();

        // 버퍼 생성 및 설정
        if (intermediateResultsBuffer != null) intermediateResultsBuffer.Release();
        if (posBuffer != null) posBuffer.Release();
        if (objectIndexBuffer != null) objectIndexBuffer.Release();

        posBuffer = new ComputeBuffer(vertexCount, sizeof(float) * 3);
        posBuffer.SetData(positions);
        intermediateResultsBuffer = new ComputeBuffer(num_of_obj * 2, sizeof(float) * 3); // Min, Max 값 저장
        objectIndexBuffer = new ComputeBuffer(num_of_obj, sizeof(int) * 2); // Min, Max 값 저장
        objectIndexBuffer.SetData(indicies);

        computeShader.SetBuffer(updateAABBKernel, "Vertices", posBuffer);
        computeShader.SetBuffer(updateAABBKernel, "IntermediateResults", intermediateResultsBuffer);
        computeShader.SetBuffer(updateAABBKernel, "ObjectIndex", objectIndexBuffer);
    }

    void DisPatchSolver()
    {
        computeShader.Dispatch(updateAABBKernel, Mathf.CeilToInt(vertexCount / 1024f), 1, 1);
    }

    void UpdatePosition()
    {
        if (index++ % num_of_loop == 0)
        {
            index = 1;
            for (int i = 0; i < num_of_obj; i++)
            {
                o[i].transform.position = new Vector3(Random.Range(-10.0f, 10.0f), Random.Range(0.0f, 10.0f), Random.Range(-10.0f, 10.0f));
            }
        }
    }

    void Update()
    {
        UpdatePosition();
        InitBuffers();
        DisPatchSolver();

        if (debugMode)
        {
            // 결과 읽기
            Vector3[] results = new Vector3[num_of_obj * 2];
            intermediateResultsBuffer.GetData(results);
            for (int i = 0; i < num_of_obj; i++)
            {
                min[i] = results[i * 2];
                max[i] = results[i * 2 + 1];
            }
        }
    }

    private void OnDestroy()
    {
        if (intermediateResultsBuffer != null) intermediateResultsBuffer.Release();
        if (posBuffer != null) posBuffer.Release();
        if (objectIndexBuffer != null) objectIndexBuffer.Release();
    }

    void OnDrawGizmos()
    {
        if (Application.isPlaying && debugMode)
        {
            Gizmos.color = Color.green; // 박스 색상 설정
            for (int i = 0; i < num_of_obj; i++)
            {
                Gizmos.DrawWireCube((min[i] + max[i]) * 0.5f, max[i] - min[i]);
            }
        }
    }
}
