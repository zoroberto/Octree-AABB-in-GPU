using UnityEngine;
using System.Collections.Generic;

public class BBTest : MonoBehaviour
{
    [Header("Compute Shader")]
    public ComputeShader computeShader;

    [Header("Object")]
    public GameObject obj;

    [Header("Object Count")]
    public int num_of_obj = 1;

    [Header("Debug Mode")]
    public bool debugMode = true;

    //compute buffers
    private ComputeBuffer posBuffer;
    private ComputeBuffer velBuffer;
    private ComputeBuffer aabbBuffer;
    private ComputeBuffer objectIndexBuffer;
    private ComputeBuffer collisionBuffer;
    //data
    private Vector3[] positions;
    private Vector3[] velocities;
    private Vector2Int[] indicies;
    private int[] collisions;

    //instantiate objects
    private GameObject[] o;
    private Mesh[] mesh_list;

    private Vector3[] min;
    private Vector3[] max;

    //kernels
    int updateAABBKernel;
    int updatePositionKernel;
    int collisionFloorKernel;

    //const value
    int vertexCount;
    private int dispatchAABBGroupSize = 1;
    private int dispatchPositionGroupSize = 1;

    void Start()
    {
        o = new GameObject[num_of_obj];
        mesh_list = new Mesh[num_of_obj];
        min = new Vector3[num_of_obj];
        max = new Vector3[num_of_obj];

        for (int i = 0; i < num_of_obj; i++)
        {
            var _obj = Instantiate(obj, new Vector3(Random.Range(-10.0f, 10.0f), Random.Range(7.0f, 15.0f), Random.Range(-10.0f, 10.0f)), Quaternion.identity);
            _obj.transform.parent = this.transform;
            o[i] = _obj;
            o[i].name = "object_" + i;
            mesh_list[i] = _obj.GetComponent<MeshFilter>().mesh;
        }

        findKernelID();
        setupBuffers();
    }

    void Update()
    {
        UpdateBuffers();
        DisPatchSolver();
        UpdatePosition();

        if (debugMode)
        {
            // 결과 읽기
            Vector3[] results = new Vector3[num_of_obj * 2];
            aabbBuffer.GetData(results);
            for (int i = 0; i < num_of_obj; i++)
            {
                min[i] = results[i * 2];
                max[i] = results[i * 2 + 1];
            }
        }
    }

    void findKernelID()
    {
        updateAABBKernel = computeShader.FindKernel("UpdateAABBGroup");
        updatePositionKernel = computeShader.FindKernel("UpdatePosition");
        collisionFloorKernel = computeShader.FindKernel("CollisionWithFloor");
    }

    void setupBuffers()
    {
        int st_index = 0;
        List<Vector3> vertices = new List<Vector3>();
        indicies = new Vector2Int[num_of_obj];
        collisions = new int[num_of_obj];
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
            collisions[i] = -1;
            st_index += _vertexCount;
        }
        vertexCount = vertices.Count;
        positions = new Vector3[vertexCount];
        velocities = new Vector3[vertexCount];

        for (int i = 0; i < vertices.Count; i++)
        {
            positions[i] = vertices[i];
            velocities[i] = Vector3.zero;
        }

        posBuffer = new ComputeBuffer(vertexCount, sizeof(float) * 3);
        velBuffer = new ComputeBuffer(vertexCount, sizeof(float) * 3);
        velBuffer.SetData(velocities);
        aabbBuffer = new ComputeBuffer(num_of_obj * 2, sizeof(float) * 3); // Min, Max 값 저장
        objectIndexBuffer = new ComputeBuffer(num_of_obj, sizeof(int) * 2); // Min, Max 값 저장
        collisionBuffer = new ComputeBuffer(num_of_obj, sizeof(int));       // 충돌 여부 저장

        dispatchAABBGroupSize = Mathf.CeilToInt(num_of_obj / 1024f);
        dispatchPositionGroupSize = Mathf.CeilToInt(vertexCount / 1024f);

        print("dispatchAABBGroupSize:" + dispatchAABBGroupSize);
        print("dispatchPositionGroupSize:" + dispatchPositionGroupSize);

        posBuffer.SetData(positions);
        objectIndexBuffer.SetData(indicies);
        collisionBuffer.SetData(collisions);
    }

    void UpdateBuffers()
    {
        computeShader.SetInt("maxVertexCount", vertexCount);
        computeShader.SetBuffer(updatePositionKernel, "Positions", posBuffer);
        computeShader.SetBuffer(updatePositionKernel, "Velocities", velBuffer);

        computeShader.SetBuffer(collisionFloorKernel, "Positions", posBuffer);
        computeShader.SetBuffer(collisionFloorKernel, "Velocities", velBuffer);
        computeShader.SetBuffer(collisionFloorKernel, "floorCollisionResult", collisionBuffer);
        computeShader.SetBuffer(collisionFloorKernel, "ObjectIndex", objectIndexBuffer);

        computeShader.SetBuffer(updateAABBKernel, "Positions", posBuffer);
        computeShader.SetBuffer(updateAABBKernel, "AABB", aabbBuffer);
        computeShader.SetBuffer(updateAABBKernel, "ObjectIndex", objectIndexBuffer);
        computeShader.SetBuffer(updateAABBKernel, "floorCollisionResult", collisionBuffer);
    }

    void DisPatchSolver()
    {
        computeShader.Dispatch(updatePositionKernel, dispatchPositionGroupSize, 1, 1);
        computeShader.Dispatch(updateAABBKernel, dispatchAABBGroupSize, 1, 1);
        computeShader.Dispatch(collisionFloorKernel, dispatchAABBGroupSize, 1, 1);
    }

    void UpdatePosition()
    {
        posBuffer.GetData(positions);

        for (int i = 0; i < num_of_obj; i++)
        {
            var src = indicies[i].x;
            var dst = indicies[i].y;

            var _vertices = mesh_list[i].vertices;
            var worldToLocal = o[i].transform.worldToLocalMatrix;
            Vector3[] new_verts = new Vector3[_vertices.Length];
            for (int j = 0; j < (dst - src); j++)
            {
                new_verts[j] = worldToLocal.MultiplyPoint3x4(positions[j + src]);
            }
            mesh_list[i].vertices = new_verts;
            mesh_list[i].RecalculateNormals();
        }
    }



    private void OnDestroy()
    {
        if (aabbBuffer != null) aabbBuffer.Release();
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
