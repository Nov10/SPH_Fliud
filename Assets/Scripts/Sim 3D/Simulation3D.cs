using UnityEngine;
using Unity.Mathematics;
using UnityEngine.XR;

public class Simulation3D : MonoBehaviour
{
    struct AABB
    {
        public Vector3 min;
        public Vector3 max;
    }
    public event System.Action SimulationStepCompleted;

    [Header("Settings")]
    [SerializeField] float timeScale = 1;
    [SerializeField] bool fixedTimeStep;
    [SerializeField] int InverseDeltaTime = 120;
    [SerializeField] int iterationsPerFrame;
    [SerializeField] Vector3 Gravity = new Vector3(0, -10, 0);
    [Range(0, 1)] public float CollisionDampingScale = 0.05f;
    [SerializeField] float SmoothingKernelRadius = 0.2f;
    [SerializeField] float TargetDensity;
    [SerializeField] float PressureForceScale;
    [SerializeField] float NearPressureForceScale;
    [SerializeField] float ViscosityForceScale;

    [Header("References")]
    [SerializeField] ComputeShader compute;
    [SerializeField] Spawner3D spawner;
    [SerializeField] ParticleDisplay3D display;
    [SerializeField] Transform floorDisplay;

    // Buffers
    ComputeBuffer Buffer_Position;
    ComputeBuffer Buffer_Velocity;
    ComputeBuffer Buffer_CollisionFlag;
    ComputeBuffer Buffer_preVelocity;
    ComputeBuffer Buffer_Density;
    ComputeBuffer Buffer_ObstacleAABB;
    ComputeBuffer Buffer_PredictedPosition;
    ComputeBuffer Buffer_SpatialHashIndices;
    ComputeBuffer Buffer_SpatialOffsets;
    ComputeBuffer Buffer_ObstalceVertices;
    ComputeBuffer Buffer_ObstacleTriangles;

    float DeltaTime;

    // Kernel IDs
    const int externalForcesKernel = 0;
    const int spatialHashKernel = 1;
    const int densityKernel = 2;
    const int pressureKernel = 3;
    const int viscosityKernel = 4;
    const int updatePositionsKernel = 5;

    GPUSort gpuSort;

    // State
    bool isPaused;
    bool pauseNextFrame;
    Spawner3D.SpawnData spawnData;

    [SerializeField] Transform obstacleTransform;
    MeshFilter obstacleMeshFilter;
    [SerializeField] Material ObstacleMaterial;



    void Start()
    {
        DeltaTime = 1 / (float)InverseDeltaTime;
        
        spawnData = spawner.GetSpawnData();

        InitializeBuffers(spawnData);

        gpuSort = new();
        gpuSort.SetBuffers(Buffer_SpatialHashIndices, Buffer_SpatialOffsets);

        display.Init(Buffer_Position, Buffer_Velocity);
        //FindObjectOfType<Master>().SetData(Buffer_Position);
    }

    void FixedUpdate()
    {
        // Run simulation if in fixed timestep mode
        if (fixedTimeStep)
        {
            RunSimulationFrame(DeltaTime);
        }
    }

    void Update()
    {

        // Run simulation if not in fixed timestep mode
        // (skip running for first few frames as timestep can be a lot higher than usual)
        if (!fixedTimeStep && Time.frameCount > 10)
        {
            RunSimulationFrame(Time.deltaTime);
        }

        if (pauseNextFrame)
        {
            isPaused = true;
            pauseNextFrame = false;
        }
        floorDisplay.transform.localScale = new Vector3(1, 1 / transform.localScale.y * 0.1f, 1);

        HandleInput();
    }

    void RunSimulationFrame(float frameTime)
    {
        if (!isPaused)
        {
            float timeStep = frameTime / iterationsPerFrame * timeScale;

            UpdateSettings(timeStep);

            for (int i = 0; i < iterationsPerFrame; i++)
            {
                RunSimulationStep();
                SimulationStepCompleted?.Invoke();
            }
        }
    }

    void RunSimulationStep()
    {
        ComputeHelper.Dispatch(compute, Buffer_Position.count, kernelIndex: externalForcesKernel);
        ComputeHelper.Dispatch(compute, Buffer_Position.count, kernelIndex: spatialHashKernel);
        gpuSort.SortAndCalculateOffsets();
        ComputeHelper.Dispatch(compute, Buffer_Position.count, kernelIndex: densityKernel);
        ComputeHelper.Dispatch(compute, Buffer_Position.count, kernelIndex: pressureKernel);
        ComputeHelper.Dispatch(compute, Buffer_Position.count, kernelIndex: viscosityKernel);
        ComputeHelper.Dispatch(compute, Buffer_Position.count, kernelIndex: updatePositionsKernel);


    }

    void UpdateSettings(float DeltaTime)
    {


        UpdateObstacleData(obstacleMeshFilter.mesh);

        Vector3 simBoundsSize = transform.localScale;
        Vector3 simBoundsCentre = transform.position;

        compute.SetFloat("DeltaTime", DeltaTime);
        compute.SetFloat("CollisionDampingScale", CollisionDampingScale);
        compute.SetFloat("SmoothingKernelRadius", SmoothingKernelRadius);
        compute.SetFloat("TargetDensity", TargetDensity);
        compute.SetFloat("PressureForceScale", PressureForceScale);
        compute.SetFloat("NearPressureForceScale", NearPressureForceScale);
        compute.SetFloat("ViscosityForceScale", ViscosityForceScale);
        compute.SetVector("Gravity", Gravity);
        compute.SetVector("BoundaryConditionSize", simBoundsSize);
        compute.SetVector("BoundaryConditionCenterPosition", simBoundsCentre);

        compute.SetMatrix("localToWorld", transform.localToWorldMatrix);
        compute.SetMatrix("worldToLocal", transform.worldToLocalMatrix);

        // 설정 추가: 시간 간격의 영향을 고려한 최소 이동 거리
        compute.SetFloat("minMoveDistance", SmoothingKernelRadius * 0.1f);
    }

    void UpdateObstacleData(Mesh obstacleMesh)
    {
        if (obstacleTransform != null && obstacleMesh != null)
        {
            Vector3[] vertices = obstacleMesh.vertices;

            for (int i = 0; i < vertices.Length; i++)
            {
                vertices[i] = obstacleTransform.TransformPoint(vertices[i]);
            }
            int[] triangles = obstacleMesh.triangles;
            Buffer_ObstalceVertices.SetData(vertices);
            AABB[] aabbs = new AABB[Buffer_ObstacleTriangles.count / 3];
            for (int i = 0; i < aabbs.Length; i++)
            {
                Vector3 v0 = vertices[triangles[i * 3]];
                Vector3 v1 = vertices[triangles[i * 3 + 1]];
                Vector3 v2 = vertices[triangles[i * 3 + 2]];

                Vector3 min = new Vector3();
                Vector3 max = new Vector3();

                min.x = Mathf.Min(Mathf.Min(v0.x, v1.x), v2.x);
                min.y = Mathf.Min(Mathf.Min(v0.y, v1.y), v2.y);
                min.z = Mathf.Min(Mathf.Min(v0.z, v1.z), v2.z);

                max.x = Mathf.Max(Mathf.Max(v0.x, v1.x), v2.x);
                max.y = Mathf.Max(Mathf.Max(v0.y, v1.y), v2.y);
                max.z = Mathf.Max(Mathf.Max(v0.z, v1.z), v2.z);

                aabbs[i] = new AABB { min = min, max = max };
            }
            Buffer_ObstacleAABB.SetData(aabbs);
        }
    }
    int InitializeObstacleBuffers(Mesh obstacleMesh)
    {
        if (obstacleTransform != null && obstacleMesh != null)
        {
            Vector3[] vertices = obstacleMesh.vertices;
            int[] triangles = obstacleMesh.triangles;

            Buffer_ObstalceVertices = ComputeHelper.CreateStructuredBuffer<float3>(vertices.Length);
            Buffer_ObstacleTriangles = ComputeHelper.CreateStructuredBuffer<int>(triangles.Length);
            Buffer_ObstacleAABB = ComputeHelper.CreateStructuredBuffer<AABB>(triangles.Length / 3);
            Buffer_ObstalceVertices.SetData(vertices);
            Buffer_ObstacleTriangles.SetData(triangles);

            AABB[] aabbs = new AABB[triangles.Length / 3];
            for (int i = 0; i < triangles.Length / 3; i++)
            {
                Vector3 v0 = vertices[triangles[i * 3]];
                Vector3 v1 = vertices[triangles[i * 3 + 1]];
                Vector3 v2 = vertices[triangles[i * 3 + 2]];

                Vector3 min = Vector3.Min(Vector3.Min(v0, v1), v2);
                Vector3 max = Vector3.Max(Vector3.Max(v0, v1), v2);

                aabbs[i] = new AABB { min = min, max = max };
            }
            Buffer_ObstacleAABB.SetData(aabbs);
            return triangles.Length;
        }
        return 0;
    }
    Mesh CombineObstacleMeshes()
    {
        MeshFilter[] meshFilters = obstacleTransform.GetComponentsInChildren<MeshFilter>();

        CombineInstance[] combine = new CombineInstance[meshFilters.Length];

        Vector3 position = obstacleTransform.position;
        Vector3 scale = obstacleTransform.localScale;
        Quaternion rotation = obstacleTransform.rotation;
        obstacleTransform.position = Vector3.zero;
        obstacleTransform.rotation = Quaternion.identity;
        obstacleTransform.localScale = Vector3.one;

        for (int i = 0; i < meshFilters.Length; i++)
        {
            combine[i].mesh = meshFilters[i].sharedMesh;
            combine[i].transform = meshFilters[i].transform.localToWorldMatrix;
            meshFilters[i].gameObject.SetActive(false);
        }

        Mesh combinedMesh = new Mesh();
        combinedMesh.CombineMeshes(combine);
        MeshFilter meshFilter = obstacleTransform.gameObject.AddComponent<MeshFilter>();
        meshFilter.mesh = combinedMesh;
        obstacleMeshFilter = meshFilter;

        MeshRenderer meshRenderer = obstacleTransform.gameObject.AddComponent<MeshRenderer>();
        for (int i = 0; i < meshRenderer.sharedMaterials.Length; i++)
        {
            meshRenderer.sharedMaterials[i] = ObstacleMaterial;
        }
        meshRenderer.sharedMaterial = ObstacleMaterial;


        obstacleTransform.position = position;
        obstacleTransform.rotation = rotation;
        obstacleTransform.localScale = scale;
        return combinedMesh;
    }

    void InitializeBuffers(Spawner3D.SpawnData spawnData)
    {
        float3[] allPoints = new float3[spawnData.points.Length];
        System.Array.Copy(spawnData.points, allPoints, spawnData.points.Length);

        int particleCount = spawnData.points.Length;
        compute.SetInt("numParticles", particleCount);

        Buffer_Position = ComputeHelper.CreateStructuredBuffer<float3>(particleCount);
        Buffer_PredictedPosition = ComputeHelper.CreateStructuredBuffer<float3>(particleCount);
        Buffer_Velocity = ComputeHelper.CreateStructuredBuffer<float3>(particleCount);
        Buffer_preVelocity = ComputeHelper.CreateStructuredBuffer<float3>(particleCount);
        Buffer_Density = ComputeHelper.CreateStructuredBuffer<float2>(particleCount);
        Buffer_CollisionFlag = ComputeHelper.CreateStructuredBuffer<float>(particleCount);
        Buffer_SpatialHashIndices = ComputeHelper.CreateStructuredBuffer<uint3>(particleCount);
        Buffer_SpatialOffsets = ComputeHelper.CreateStructuredBuffer<uint>(particleCount);

        Buffer_Position.SetData(allPoints);
        Buffer_PredictedPosition.SetData(allPoints);
        Buffer_Velocity.SetData(spawnData.velocities);
        Buffer_preVelocity.SetData(spawnData.velocities);


        int obstacleTriangleCount = InitializeObstacleBuffers(CombineObstacleMeshes());
        compute.SetInt("ObstacleTriangleCount", obstacleTriangleCount / 3);


        ComputeHelper.SetBuffer(compute, Buffer_ObstacleAABB, "ObstacleAABBs", updatePositionsKernel, externalForcesKernel);
        ComputeHelper.SetBuffer(compute, Buffer_ObstalceVertices, "ObstacleVertices", updatePositionsKernel, externalForcesKernel);
        ComputeHelper.SetBuffer(compute, Buffer_ObstacleTriangles, "ObstacleTriangles", updatePositionsKernel, externalForcesKernel);
        ComputeHelper.SetBuffer(compute, Buffer_ObstacleAABB, "ObstacleAABBs", updatePositionsKernel, externalForcesKernel);
        ComputeHelper.SetBuffer(compute, Buffer_Position, "Positions", externalForcesKernel, updatePositionsKernel);
        ComputeHelper.SetBuffer(compute, Buffer_PredictedPosition, "PredictedPositions", externalForcesKernel, spatialHashKernel, densityKernel, pressureKernel, viscosityKernel, updatePositionsKernel);
        ComputeHelper.SetBuffer(compute, Buffer_SpatialHashIndices, "SpatialIndices", spatialHashKernel, densityKernel, pressureKernel, viscosityKernel);
        ComputeHelper.SetBuffer(compute, Buffer_SpatialOffsets, "SpatialOffsets", spatialHashKernel, densityKernel, pressureKernel, viscosityKernel);
        ComputeHelper.SetBuffer(compute, Buffer_Density, "Densities", densityKernel, pressureKernel, viscosityKernel);
        ComputeHelper.SetBuffer(compute, Buffer_Velocity, "Velocities", externalForcesKernel, pressureKernel, viscosityKernel, updatePositionsKernel);
        ComputeHelper.SetBuffer(compute, Buffer_preVelocity, "pVelocities", updatePositionsKernel, externalForcesKernel);
        ComputeHelper.SetBuffer(compute, Buffer_CollisionFlag, "collisionBuffer", updatePositionsKernel, externalForcesKernel);
    }

    void HandleInput()
    {
        if (Input.GetKeyDown(KeyCode.Space))
        {
            isPaused = !isPaused;
        }

        if (Input.GetKeyDown(KeyCode.RightArrow))
        {
            isPaused = false;
            pauseNextFrame = true;
        }

        if (Input.GetKeyDown(KeyCode.R))
        {
            isPaused = true;
            InitializeBuffers(spawnData);
        }
    }

    void OnDestroy()
    {
        ComputeHelper.Release(Buffer_Position, Buffer_PredictedPosition, Buffer_Velocity, Buffer_Density, Buffer_SpatialHashIndices, Buffer_SpatialOffsets, Buffer_CollisionFlag);
        // Release obstacle buffers
        ComputeHelper.Release(Buffer_ObstalceVertices, Buffer_ObstacleTriangles, Buffer_ObstacleAABB);
    }

    void OnDrawGizmos()
    {
        // Draw Bounds
        var m = Gizmos.matrix;
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.color = new Color(1, 1, 1, 1f);
        Gizmos.DrawWireCube(Vector3.zero, Vector3.one);
        Gizmos.matrix = m;

    }
}