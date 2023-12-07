using Assets;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq; // Import the LINQ library : use -> Distinct()
using Assets.Scripts;

public class Controller : MonoBehaviour
{
    [Header("Particle Parameters")]
    public float dt = 0.001f;
    public Vector3 gravity = new Vector3(0, -10, 0);
    public GameObject floor;

    [Header("Compute Shader")]
    public ComputeShader computeShader;

    [Header("Material")]
    public Material material;

    //==============================
    //   Private Fields
    //==============================

    // clone object
    private Vector3[] vertices;
    private List<Transform> meshTransform = new List<Transform>(); // used to store objects after cloing

    // Mesh bounding
    private BoundingBox planeBounding = new BoundingBox();
    private List<BoundingBox> customMesh = new List<BoundingBox>();

    // collision pair
    private List<BoundingboxPair> collisionPairs = new List<BoundingboxPair>();
    public List<int> collidablePairIndex = new List<int>();
    private bool overlap = false;

    // index pair
    private List<IndexPair> indexPairs = new List<IndexPair>();

    // vertex DATA
    private List<Vector3> verticesRangeList = new List<Vector3>(); // store all combinded vertices of all objects
    private List<List<Vector3>> listOfVerticesList = new List<List<Vector3>>(); // list inside list, to find index of each list
    private List<int> indicesListRange = new List<int>(); // store index range of each object

    // physic particle
    private List<ParticleList> particleData = new List<ParticleList>();

    // Compute Buffer
    private ComputeBuffer positionsBuffer;
    private ComputeBuffer velocitiesBuffer;
    private ComputeBuffer floorCollisionResultBuffer;
    private ComputeBuffer objectBoundingBuffer;
    private ComputeBuffer pairResultsBuffer;
    private ComputeBuffer pairIndexBuffer;
    private ComputeBuffer combinedIndexBuffer;

    // kernel IDs
    private int aabbCollisionKernel;
    private int updatePositionKernel;
    private int floorObjCollisionKernel;
    private int updateReverseVelocityKernel;

    // Data
    private int totalNumVertices;
    private int[] pairCollisionresults;
    private BoundData[] objectBoundingArray;
    private PairData[] pairIndexArray;
    private Vector3[] positionsArray;
    private Vector3[] velocitiesArray;
    public List<int> collidableMeshIndex = new List<int>();

    void Start()
    {
        ReadPositionFromExcel();
        FindPlaneMinMax();
        FindKernelIDs();
        InitializeData();
        InitializeArray();

        AddCollisionPair();
        print(" pair " + collisionPairs.Count);

        CreateComputeBuffer();
    }

    // Import excel data
    private void ReadPositionFromExcel()
    {
        meshTransform = GetComponent<CSVImportPos>().ReadPositionFromExcel();

    }

    // Find min and max of floor
    private void FindPlaneMinMax()
    {
        GetMeshVertex.Getvertices(floor);
        planeBounding.Min = GetMeshVertex.minPos;
        planeBounding.Max = GetMeshVertex.maxPos;
        planeBounding.Max.y += 0.1f; // add offset to plane bounding
    }

    // Find kernel's IDs
    private void FindKernelIDs()
    {
        aabbCollisionKernel = computeShader.FindKernel("AABBCompute");
        updatePositionKernel = computeShader.FindKernel("UpdatePosition");
        floorObjCollisionKernel = computeShader.FindKernel("FloorCollision");
        updateReverseVelocityKernel = computeShader.FindKernel("UpdateReverseVelocity");
    }

    private void InitializeData()
    {
        for (int i = 0; i < meshTransform.Count; i++)
        {
            vertices = meshTransform[i].gameObject.GetComponent<MeshFilter>().mesh.vertices;

            BoundingBox mesh = new BoundingBox();
            mesh.Vertices = new List<Vector3>();
            foreach (Vector3 v in vertices) mesh.Vertices.Add(v);
            customMesh.Add(mesh);
        }

        // add vertices of all objects to one list, so list inside list and can find index of each list
        foreach (BoundingBox vert in customMesh) listOfVerticesList.Add(vert.Vertices.ToList());

        for (int i = 0; i < meshTransform.Count; i++)
        {
            for (int j = 0; j < customMesh[i].Vertices.Count; j++)
            {
                //print(" world " + i+" " + j + " " + meshTransform[0].gameObject.transform.TransformPoint(vertices[j]));

                customMesh[i].Vertices[j] = meshTransform[i].gameObject.transform.TransformPoint(vertices[j]);
                //customMesh[i].Vertices[j] = vertices[j];
            }
            verticesRangeList.AddRange(customMesh[i].Vertices); // add all vertices of object from vertex 1 -> n
        }


    }

    // Initiate the data arrays
    private void InitializeArray()
    {
        totalNumVertices = verticesRangeList.Count;


        positionsArray = new Vector3[verticesRangeList.Count];
        velocitiesArray = new Vector3[verticesRangeList.Count];
        objectBoundingArray = new BoundData[meshTransform.Count];

        for (int i = 0; i < verticesRangeList.Count; i++)
        {
            positionsArray[i] = verticesRangeList[i];
            velocitiesArray[i] = Vector3.zero;
        }

        indicesListRange = CalculateCombinedIndices(listOfVerticesList);
    }

    // Calculate combined index
    static List<int> CalculateCombinedIndices(List<List<Vector3>> lists)
    {
        List<int> combinedIndices = new List<int>();
        int currentIndex = 0;

        // Start with 0 as the initial index.
        combinedIndices.Add(currentIndex);

        foreach (List<Vector3> list in lists)
        {
            int listLength = list.Count;
            //print($"listLength vert { listLength}");

            currentIndex += listLength;
            // print($"currentIndex { currentIndex}");

            combinedIndices.Add(currentIndex);
        }

        return combinedIndices;

    }


    // Create the compute buffers and set their data
    private void CreateComputeBuffer()
    {
        pairCollisionresults = new int[collisionPairs.Count];

        // Create a compute buffer

        pairIndexBuffer = new ComputeBuffer(pairIndexArray.Length, sizeof(int) * 2);
        pairResultsBuffer = new ComputeBuffer(collisionPairs.Count, sizeof(int));

        positionsBuffer = new ComputeBuffer(verticesRangeList.Count, sizeof(float) * 3);
        velocitiesBuffer = new ComputeBuffer(verticesRangeList.Count, sizeof(float) * 3);

        floorCollisionResultBuffer = new ComputeBuffer(meshTransform.Count, sizeof(int));
        objectBoundingBuffer = new ComputeBuffer(meshTransform.Count, sizeof(float) * 6);

        combinedIndexBuffer = new ComputeBuffer(indicesListRange.Count + 1, sizeof(int));

        // Set the buffers to the compute shader
        pairIndexBuffer.SetData(pairIndexArray);
        velocitiesBuffer.SetData(velocitiesArray);
        positionsBuffer.SetData(positionsArray);
        combinedIndexBuffer.SetData(indicesListRange);
    }

    // Add Collision Pair
    private void AddCollisionPair()
    {
        // *** collision pair 
        // - add bounding pair
        // - check collision pair
        // by length of clone objects

        //collisionPairs.Clear();
        for (int i = 0; i < meshTransform.Count; i++)
        {
            customMesh[i] = new BoundingBox();
            for (int j = i + 1; j < meshTransform.Count; j++)
            {
                //print($" {i} {j}");

                customMesh[j] = new BoundingBox();
                AddBoundingPair(customMesh[i], customMesh[j], i, j);

                AddPairIndex(i, j);

            }
        }

        UpdatePairIndexArray();
    }

    // Add bounding pair by objects list => [0,1], [0,2], [1,2] ...
    private void AddBoundingPair(BoundingBox b1, BoundingBox b2, int i, int j)
    {
        // in pair needs to store array of bounding and add b1 and b2 to list
        List<BoundingBox> boundings = new List<BoundingBox>();
        boundings.Add(b1);
        boundings.Add(b2);

        List<int> indices = new List<int>();
        indices.Add(i);
        indices.Add(j);

        BoundingboxPair pair = new BoundingboxPair(boundings, indices);
        collisionPairs.Add(pair);

    }

    // Add index pair by game objects list => [0,1], [0,2], [1,2] ...
    private void AddPairIndex(int i1, int i2)
    {

        // in pair needs to store array of index and add i1 and i2 to list
        List<int> indices = new List<int>();
        indices.Add(i1);
        indices.Add(i2);

        IndexPair pair = new IndexPair(indices);
        indexPairs.Add(pair);
    }

    // Update pair array
    private void UpdatePairIndexArray()
    {
        pairIndexArray = new PairData[collisionPairs.Count];
        for (int i = 0; i < indexPairs.Count; i++)
        {
            pairIndexArray[i] = new PairData
            {
                i1 = indexPairs[i].Index[0],
                i2 = indexPairs[i].Index[1]
            };
        }
    }

    void Update()
    {
        FindMeshMinMax();
        SetComputeShader();
        DispatchComputeShader();
        GetDataToCPU();

    }


    private void FindMeshMinMax()
    {
        for (int i = 0; i < meshTransform.Count; i++)
        {
            GetMeshVertex.Getvertices(meshTransform[i].gameObject);

            customMesh[i].Min = GetMeshVertex.minPos;
            customMesh[i].Max = GetMeshVertex.maxPos;
            customMesh[i].Center = (customMesh[i].Max + customMesh[i].Min) / 2;

            objectBoundingArray[i].Min = customMesh[i].Min;
            objectBoundingArray[i].Max = customMesh[i].Max;

        }

    }

    // Set the compute shader parameters
    private void SetComputeShader()
    {
        // Set the data to the buffer
        objectBoundingBuffer.SetData(objectBoundingArray);
        computeShader.SetFloat("deltaTime", dt);
        computeShader.SetVector("gravity", gravity);
        computeShader.SetInt("numTotalVertices", totalNumVertices);
        computeShader.SetInt("numMesh", meshTransform.Count);
        computeShader.SetInt("boundingIndexBuffer", meshTransform.Count);
        computeShader.SetFloat("floorPos", floor.transform.position.y);

        // Set the buffers to the compute shader
        computeShader.SetBuffer(aabbCollisionKernel, "objectBoundingBuffer", objectBoundingBuffer);
        computeShader.SetBuffer(aabbCollisionKernel, "pairIndexBuffer", pairIndexBuffer);
        computeShader.SetBuffer(aabbCollisionKernel, "pairCollisionResult", pairResultsBuffer);

        computeShader.SetBuffer(updatePositionKernel, "positions", positionsBuffer);
        computeShader.SetBuffer(updatePositionKernel, "velocities", velocitiesBuffer);

        computeShader.SetVector("floorMinPos", planeBounding.Min);
        computeShader.SetVector("floorMaxPos", planeBounding.Max);

        computeShader.SetBuffer(floorObjCollisionKernel, "objectBoundingBuffer", objectBoundingBuffer);
        computeShader.SetBuffer(floorObjCollisionKernel, "floorCollisionResult", floorCollisionResultBuffer);

        computeShader.SetBuffer(updateReverseVelocityKernel, "combinedIndexBuffer", combinedIndexBuffer);

        computeShader.SetBuffer(updateReverseVelocityKernel, "positions", positionsBuffer);
        computeShader.SetBuffer(updateReverseVelocityKernel, "velocities", velocitiesBuffer);
        computeShader.SetBuffer(updateReverseVelocityKernel, "floorCollisionResult", floorCollisionResultBuffer);

    }

    // Dispatch the compute shader
    private void DispatchComputeShader()
    {
        float maximumThread = 1024;

        int numGroups_AABBCollision = Mathf.CeilToInt(collisionPairs.Count / 8f); // One thread per object, 6
        computeShader.Dispatch(aabbCollisionKernel, numGroups_AABBCollision, 1, 1);

        int numGroupsVerts = Mathf.CeilToInt(totalNumVertices / maximumThread);
        computeShader.Dispatch(updatePositionKernel, numGroupsVerts, 1, 1);

        int mesh = Mathf.CeilToInt(meshTransform.Count / maximumThread);
        computeShader.Dispatch(floorObjCollisionKernel, mesh, 1, 1);

        int reverseVelo = Mathf.CeilToInt(totalNumVertices / maximumThread);
        computeShader.Dispatch(updateReverseVelocityKernel, reverseVelo, 1, 1);

    }


    // Get the result data from the compute buffer to CPU
    private void GetDataToCPU()
    {
        GetAabbCollisionResult();
        GetPositionsArray();
        //GetFloorCollisionResult();
    }

    // Get collision result
    private void GetAabbCollisionResult()
    {
        // Read back the collision results from the buffer, Process the collision results for each pair
        // Create a new list to store indices to remove
        List<int> indicesToRemove = new List<int>();

        // collision 
        collidablePairIndex.Clear();
        indicesToRemove.Clear();

        pairResultsBuffer.GetData(pairCollisionresults);



        for (int i = 0; i < pairCollisionresults.Length; i++)
        {
            //print("i " + pairCollisionresults[i]);

            if (pairCollisionresults[i] == 1) // 1 boolean is true
            {
                //print("i " + i);
                //boundingBox[i] = new BoundingBox();
                //boundingBox[i].IsCollide.Add(1); // i -> n index of bounding box collided with floor
                overlap = true;
                collidablePairIndex.Add(i);
            }
            else
            {
                // If 'i' is in collisionIndex, mark it for removal
                if (collidablePairIndex.Contains(i))
                {
                    indicesToRemove.Add(i);
                }
            }

        }


        // Remove marked indices from collisionIndex
        foreach (int index in indicesToRemove)
        {
            collidablePairIndex.Remove(index);
        }

        //print("i " + collidablePairIndex.Count);
    }

    private void GetPositionsArray()
    {
        //velocitiesBuffer.GetData(velocitiesArray);
        positionsBuffer.GetData(positionsArray);

        particleData.Clear();
        for (int i = 0; i < indicesListRange.Count - 1; i++)
        {
            ParticleList p = new ParticleList();
            p.vertexList = new List<Vector3>();

            int start = indicesListRange[i];
            int end = indicesListRange[i + 1];
            //print("st " + start);
            //print("end " + end);

            for (int s = start; s < end; s++)
            {
                p.vertexList.Add(positionsArray[s]);
                //print("combinedIndices " + s+ " " + positionsArray[s]);
            }
            particleData.Add(p);
        }

        MovePositionObject();

    }


    // add motion
    private void MovePositionObject()
    {
        for (int i = 0; i < particleData.Count; i++)
        {
            meshTransform[i].gameObject.GetComponent<MeshFilter>().mesh.vertices = particleData[i].vertexList.ToArray();
            meshTransform[i].gameObject.GetComponent<Renderer>().material = material;

        }
    }

    // Get floor collision with obj
    private void GetFloorCollisionResult()
    {
        //floorCollisionResultBuffer.GetData(floorCollisionResults);

    }

    // private void OnDrawGizmos()
    // {
    //     if (overlap)
    //     {
    //         for (int i = 0; i < collisionPairs.Count; i++)
    //         {

    //             for (int j = 0; j < collisionPairs[i].Bound.Count; j++)
    //             {

    //                 Vector3 size = new Vector3(
    //                    Mathf.Abs(collisionPairs[i].Bound[j].Max.x - collisionPairs[i].Bound[j].Min.x),
    //                    Mathf.Abs(collisionPairs[i].Bound[j].Max.y - collisionPairs[i].Bound[j].Min.y),
    //                    Mathf.Abs(collisionPairs[i].Bound[j].Max.z - collisionPairs[i].Bound[j].Min.z)
    //                    );

    //                 collisionPairs[i].Bound[j].Center = (collisionPairs[i].Bound[j].Max + collisionPairs[i].Bound[j].Min) / 2;

    //                 for (int k = 0; k < collidablePairIndex.Count; k++)
    //                 {
    //                     if (i == collidablePairIndex[k])
    //                     {
    //                         Gizmos.color = Color.red;
    //                         Gizmos.DrawWireCube(collisionPairs[collidablePairIndex[k]].Bound[j].Center, size);
    //                         Gizmos.DrawWireCube(collisionPairs[collidablePairIndex[k]].Bound[j].Center, size);
    //                     }

    //                 }

    //             }

    //         }
    //     }


    // }


    private void OnDestroy()
    {
        if (positionsBuffer != null) positionsBuffer.Release();
        if (velocitiesBuffer != null) velocitiesBuffer.Release();
        if (floorCollisionResultBuffer != null) floorCollisionResultBuffer.Release();
        if (objectBoundingBuffer != null) objectBoundingBuffer.Release();
        if (combinedIndexBuffer != null) combinedIndexBuffer.Release();
        if (pairIndexBuffer != null) pairIndexBuffer.Release();
        if (pairResultsBuffer != null) pairResultsBuffer.Release();
        if (pairIndexBuffer != null) pairIndexBuffer.Release();
        if (combinedIndexBuffer != null) combinedIndexBuffer.Release();
    }

}
