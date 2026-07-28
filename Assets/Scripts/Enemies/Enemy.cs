using NavMeshPlus.Extensions;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(AgentOverride2d))]
public class Enemy : MonoBehaviour
{
    private const float AgentDrift = 0.0001f;

    [SerializeField] private float moveSpeed = 3.5f;
    [SerializeField] private float repathInterval = 0.2f;
    [SerializeField] private float stopDistance = 0.4f;
    [SerializeField] private Transform target;

    private NavMeshAgent _agent;
    private float _repathTimer;

    void Awake()
    {
        _agent = GetComponent<NavMeshAgent>();
        _agent.updateRotation = false;
        _agent.updateUpAxis = false;
        _agent.speed = moveSpeed;
        _agent.stoppingDistance = stopDistance;
        _agent.acceleration = 100f;
        _agent.autoBraking = true;

        var rb = GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.gravityScale = 0f;
            rb.freezeRotation = true;
        }
    }

    void Start()
    {
        if (target == null)
        {
            var player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
                target = player.transform;
        }

        if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 2f, NavMesh.AllAreas))
            _agent.Warp(hit.position);
    }

    void Update()
    {
        if (target == null || !_agent.isOnNavMesh)
            return;

        _repathTimer -= Time.deltaTime;
        if (_repathTimer > 0f)
            return;

        _repathTimer = repathInterval;
        SetDestination(target.position);
    }

    public void SetTarget(Transform newTarget)
    {
        target = newTarget;
        _repathTimer = 0f;
    }

    private void SetDestination(Vector3 destination)
    {
        if (Mathf.Abs(transform.position.x - destination.x) < AgentDrift)
            destination.x += AgentDrift;

        _agent.SetDestination(destination);
    }
}
