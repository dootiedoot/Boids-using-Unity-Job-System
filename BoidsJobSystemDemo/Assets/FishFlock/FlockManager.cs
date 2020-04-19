using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Collections;
using Unity.Jobs;
using Unity.Burst;
using UnityEngine.Jobs;

public class FlockManager : MonoBehaviour
{
    public int initialSpawnAmt = 500;
    public int spawnCount = 0;

    [Header("Fish Settings")]
    public GameObject fishPrefab;
    public ushort raycastDistance = 2;
    public float minSpeed = 0.05f;
    public float maxSpeed = 5;
    public float neighbourDistance = 10;
    public float rotationSpeed = 4;
    public LayerMask avoidanceLayer = ~0;
    public Transform Bounds;
    [Tooltip("The transform the flock will parent too. However, having no parent is much more performant.")]
    public Transform flockParent = null;

    [Header("Debug")]
    [SerializeField] private bool debug;

    private List<Transform> fishesTrs = new List<Transform>();
    private Vector3 swimLimits;
    private Vector3 goalPos;

    //  job system
    private TransformAccessArray fishTrsAccessArray;
    private NativeArray<float> fishSpeeds;

    // Use this for initialization
    void Awake()
    {

    }

    void Start()
    {
        AddFish(initialSpawnAmt);

        goalPos = this.transform.position;
    }

    [BurstCompile]
    void Update()
    {
        swimLimits = Bounds.localScale;

        if (UnityEngine.Random.Range(0, 100) < 10)
        {
            Vector3 ranges = new Vector3(UnityEngine.Random.Range(-swimLimits.x, swimLimits.x), UnityEngine.Random.Range(-swimLimits.y, swimLimits.y), UnityEngine.Random.Range(-swimLimits.z, swimLimits.z));
            goalPos = this.transform.position + ranges;
        }

        //	swim each fish
        int count = fishesTrs.Count;
        Bounds b = new Bounds(Bounds.position, Bounds.localScale);

        //  data containers
        NativeArray<Vector3> velocities = new NativeArray<Vector3>(count, Allocator.TempJob);
        NativeArray<Vector3> positions = new NativeArray<Vector3>(count, Allocator.TempJob);
        NativeArray<Vector3> forwards = new NativeArray<Vector3>(count, Allocator.TempJob);
        NativeArray<float> speeds = new NativeArray<float>(count, Allocator.TempJob);
        NativeArray<bool> withinBounds = new NativeArray<bool>(count, Allocator.TempJob);
        NativeArray<bool> isHitObstacles = new NativeArray<bool>(count, Allocator.TempJob);
        NativeArray<Vector3> hitNormals = new NativeArray<Vector3>(count, Allocator.TempJob);
        NativeArray<float> randomSpeeds = new NativeArray<float>(count, Allocator.TempJob);
        NativeArray<bool> changeRandomSpeedChances = new NativeArray<bool>(count, Allocator.TempJob);
        NativeArray<bool> applyRuleChances = new NativeArray<bool>(count, Allocator.TempJob);
        NativeArray<bool> turnings = new NativeArray<bool>(count, Allocator.TempJob);
        NativeArray<RaycastCommand> raycastCommands = new NativeArray<RaycastCommand>(count, Allocator.TempJob);
        NativeArray<RaycastHit> raycastHits = new NativeArray<RaycastHit>(count, Allocator.TempJob);

        //  initialization
        for (int i = 0; i < count; i++)
        {
            velocities[i] = fishesTrs[i].forward * raycastDistance;
            positions[i] = fishesTrs[i].position;
            forwards[i] = fishesTrs[i].forward;
            speeds[i] = UnityEngine.Random.Range(minSpeed, maxSpeed);
            withinBounds[i] = b.Contains(fishesTrs[i].position);
            changeRandomSpeedChances[i] = UnityEngine.Random.Range(0, 100) < 10 ? true : false;
            applyRuleChances[i] = UnityEngine.Random.Range(0, 100) < 20;
            randomSpeeds[i] = UnityEngine.Random.Range(minSpeed, maxSpeed);
            Vector3 pos = fishesTrs[i].position;
            Vector3 velocity = fishesTrs[i].forward * raycastDistance;
            raycastCommands[i] = new RaycastCommand(pos, velocity, raycastDistance);
        }

        //Schedule the batch of raycasts
        RaycastCommandJobs raycastJobs = new RaycastCommandJobs()
        {
            raycastDistance = raycastDistance,
            velocities = velocities,
            positions = positions,
            Raycasts = raycastCommands,
            layerMask = avoidanceLayer
        };
        var setupDependency = raycastJobs.Schedule(count, 32);
        JobHandle raycastHandle = RaycastCommand.ScheduleBatch(raycastCommands, raycastHits, 32, setupDependency);
        raycastHandle.Complete();

        //  we have raycast hit results...
        for (int i = 0; i < count; i++)
        {
            isHitObstacles[i] = raycastHits[i].collider ? true : false;
            hitNormals[i] = raycastHits[i].normal;
            turnings[i] = (withinBounds[i] == false || isHitObstacles[i]) ? true : false;

            if (debug)
            {
                if (isHitObstacles[i])
                {
                    Debug.DrawRay(positions[i], forwards[i] * raycastDistance, Color.yellow);
                }
            }
        }

        //  Swim forward job
        SwimJob transformJob = new SwimJob
        {
            deltaTime = Time.deltaTime,
            changeRandomSpeedChances = changeRandomSpeedChances,
            randomSpeeds = randomSpeeds,
            isHitObstacles = isHitObstacles,
            hitNormals = hitNormals,
            managerPosition = transform.position,
            speeds = speeds,
            forwards = forwards,
            rotationSpeed = rotationSpeed,
            withinBounds = withinBounds,
            turnings = turnings
        };
        var transformJobHandle = transformJob.Schedule(fishTrsAccessArray/*, raycastHandle*/);
        //JobHandle.ScheduleBatchedJobs();

        //  apply rules job
        ApplyRulesJob applyRulesJob = new ApplyRulesJob
        {
            deltaTime = Time.deltaTime,
            changeRandomSpeedChances = changeRandomSpeedChances,
            randomSpeeds = randomSpeeds,
            ruleApplyChances = applyRuleChances,
            speeds = speeds,
            rotationSpeed = rotationSpeed,
            turnings = turnings,
            goalPos = goalPos,
            neighbourDistance = neighbourDistance,
            otherFishesSpeeds = fishSpeeds,
            positions = positions
        };
        var applyRulesJobHandle = applyRulesJob.Schedule(fishTrsAccessArray);

        //  schedule transform jobs
        JobHandle.ScheduleBatchedJobs();
        transformJobHandle.Complete();
        applyRulesJobHandle.Complete();

        // Dispose the buffers
        velocities.Dispose();
        positions.Dispose();
        raycastHits.Dispose();
        raycastCommands.Dispose();
        forwards.Dispose();
        speeds.Dispose();
        withinBounds.Dispose();
        hitNormals.Dispose();
        isHitObstacles.Dispose();
        randomSpeeds.Dispose();
        changeRandomSpeedChances.Dispose();
        applyRuleChances.Dispose();
        turnings.Dispose();
    }

    public void AddFish(int amt)
    {
        spawnCount += amt;
        fishSpeeds = new NativeArray<float>(spawnCount, Allocator.Persistent);
        for (int i = 0; i < amt; i++)
        {
            Vector3 pos = this.transform.position + new Vector3(UnityEngine.Random.Range(-swimLimits.x, swimLimits.x), UnityEngine.Random.Range(-swimLimits.y, swimLimits.y), UnityEngine.Random.Range(-swimLimits.z, swimLimits.z));

            fishesTrs.Add(Instantiate(fishPrefab, pos, Quaternion.identity, flockParent ? flockParent : null).transform);
            float speed = UnityEngine.Random.Range(minSpeed, maxSpeed);
            fishSpeeds[i] = speed;
        }
        fishTrsAccessArray = new TransformAccessArray(fishesTrs.ToArray());
    }

    public void RemoveFish(Transform fishTr)
    {
        if (fishesTrs.Remove(fishTr))
        {
            spawnCount--;

            fishSpeeds = new NativeArray<float>(spawnCount, Allocator.Persistent);
            for (int i = 0; i < spawnCount; i++)
            {
                float speed = UnityEngine.Random.Range(minSpeed, maxSpeed);
                fishSpeeds[i] = speed;
            }
            fishTrsAccessArray = new TransformAccessArray(fishesTrs.ToArray());
        }
    }

    [ContextMenu("Add fish!!")]
    public void AddMoreFish()
    {
        AddFish(500);
    }
    [ContextMenu("Remove fish!!")]
    public void RemoveRandomFishes()
    {
        int removeAmt = UnityEngine.Random.Range(1, fishesTrs.Count);
        for (int i = 0; i < removeAmt; i++)
        {
            RemoveFish(fishesTrs[0]);
        }
    }

    [BurstCompile]
    struct RaycastCommandJobs : IJobParallelFor
    {
        [ReadOnly] public ushort raycastDistance;
        [ReadOnly] public NativeArray<Vector3> velocities;
        [ReadOnly] public NativeArray<Vector3> positions;
        [ReadOnly] public LayerMask layerMask;
        public NativeArray<RaycastCommand> Raycasts;

        public void Execute(int i)
        {
            Raycasts[i] = new RaycastCommand(positions[i], velocities[i], raycastDistance, layerMask);
        }
    }

    [BurstCompile]
    public struct SwimJob : IJobParallelForTransform
    {
        [ReadOnly] public NativeArray<float> randomSpeeds;
        [ReadOnly] public NativeArray<bool> changeRandomSpeedChances;
        [ReadOnly] public Vector3 managerPosition;
        [ReadOnly] public NativeArray<bool> isHitObstacles;
        [ReadOnly] public NativeArray<Vector3> hitNormals;
        [ReadOnly] public float rotationSpeed;
        [ReadOnly] public NativeArray<float> speeds;
        [ReadOnly] public float deltaTime;
        [ReadOnly] public NativeArray<Vector3> forwards;
        [ReadOnly] public NativeArray<bool> withinBounds;
        [ReadOnly] public NativeArray<bool> turnings;


        public void Execute(int i, TransformAccess transform)
        {
            Vector3 direction = Vector3.zero;
            float speed = speeds[i];
            //bool turning = false;

            if (withinBounds[i] == false)
                direction = managerPosition - transform.position;
            else if (isHitObstacles[i])
                //turning = true;
                direction = Vector3.Reflect(forwards[i], hitNormals[i]);

            if (turnings[i])
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(direction), rotationSpeed * deltaTime);

            transform.position += forwards[i] * deltaTime * speed;
        }
    }

    [BurstCompile]
    public struct ApplyRulesJob : IJobParallelForTransform
    {
        [ReadOnly] public Vector3 goalPos;
        [ReadOnly] public float neighbourDistance;
        [ReadOnly] public NativeArray<float> otherFishesSpeeds;
        [ReadOnly] public NativeArray<Vector3> positions;
        [ReadOnly] public NativeArray<bool> ruleApplyChances;
        [ReadOnly] public NativeArray<float> randomSpeeds;
        [ReadOnly] public NativeArray<bool> changeRandomSpeedChances;
        [ReadOnly] public float rotationSpeed;
        [ReadOnly] public NativeArray<float> speeds;
        [ReadOnly] public float deltaTime;
        [ReadOnly] public NativeArray<bool> turnings;


        public void Execute(int i, TransformAccess transform)
        {
            Vector3 vcentre = Vector3.zero;
            Vector3 vavoid = Vector3.zero;
            float gSpeed = 0.01f;
            float nDistance;
            int groupSize = 0;
            float speed = speeds[i];

            if (!turnings[i])
            {
                if (changeRandomSpeedChances[i])
                {
                    speed = randomSpeeds[i];
                }
                if (ruleApplyChances[i])
                {
                    for (int j = 0; j < positions.Length; j++)
                    {
                        //  ignore self
                        if (positions[j] == transform.position)
                            continue;

                        nDistance = Vector3.Distance(positions[j], transform.position);
                        if (nDistance <= neighbourDistance)
                        {
                            vcentre += positions[j];
                            groupSize++;

                            if (nDistance < 1.0f)
                            {
                                vavoid = vavoid + (transform.position - positions[j]);
                            }

                            gSpeed = gSpeed + otherFishesSpeeds[j];
                        }
                    }

                    if (groupSize > 0)
                    {
                        vcentre = vcentre / groupSize + (goalPos - transform.position);
                        speed = gSpeed / groupSize;

                        Vector3 direction = (vcentre + vavoid) - transform.position;
                        if (direction != Vector3.zero)
                            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(direction), rotationSpeed * deltaTime);
                    }
                }
            }
        }
    }
}
