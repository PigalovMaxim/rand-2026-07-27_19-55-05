using System.Collections.Generic;
using NavMeshPlus.Extensions;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(AgentOverride2d))]
public class Enemy : MonoBehaviour
{
    private const float AgentDrift = 0.0001f;
    private const float ContactPadding = 0.05f;

    private static readonly List<Enemy> _enemies = new();
    private static readonly List<Collider2D> _enemyColliders = new();

    [SerializeField] private float moveSpeed = 3.5f;
    [SerializeField] private float repathInterval = 0.2f;
    [SerializeField] private float stopDistance = 0.4f;
    [SerializeField] private float loseVisionRange = 12f;
    [SerializeField] private Transform target;

    private NavMeshAgent _agent;
    private Collider2D _collider;
    private Collider2D _targetCollider;
    private float _repathTimer;
    private bool _seesTarget;
    private bool _seenThisFrame;

    public Vector2 VisionCenter => _collider != null ? _collider.bounds.center : transform.position;

    public static IReadOnlyList<Enemy> ActiveEnemies => _enemies;

    void Awake()
    {
        _agent = GetComponent<NavMeshAgent>();
        _collider = GetComponent<Collider2D>();
        _agent.updateRotation = false;
        _agent.updateUpAxis = false;
        _agent.speed = moveSpeed;
        _agent.stoppingDistance = stopDistance;
        _agent.acceleration = 100f;
        _agent.autoBraking = true;
    }

    void OnEnable()
    {
        if (!_enemies.Contains(this))
            _enemies.Add(this);

        RegisterEnemyCollider();
    }

    void OnDisable()
    {
        _enemies.Remove(this);
        UnregisterEnemyCollider();
    }

    void Start()
    {
        if (target == null)
        {
            var player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
                target = player.transform;
        }

        CacheTargetCollider();
        TryPlaceOnNavMesh();
    }

    private void RegisterEnemyCollider()
    {
        if (_collider == null)
            _collider = GetComponent<Collider2D>();

        if (_collider == null)
            return;

        for (var i = 0; i < _enemyColliders.Count; i++)
        {
            var other = _enemyColliders[i];
            if (other != null)
                Physics2D.IgnoreCollision(_collider, other, true);
        }

        if (!_enemyColliders.Contains(_collider))
            _enemyColliders.Add(_collider);
    }

    private void UnregisterEnemyCollider()
    {
        if (_collider == null)
            return;

        _enemyColliders.Remove(_collider);
    }

    void Update()
    {
        if (target == null)
            return;

        if (!_agent.isOnNavMesh)
        {
            TryPlaceOnNavMesh();
            return;
        }

        UpdateVisionRetention();

        if (!_seesTarget)
        {
            StopMoving();
            return;
        }

        if (IsInContactWithTarget())
        {
            StopMoving();
            return;
        }

        _agent.isStopped = false;

        _repathTimer -= Time.deltaTime;
        if (_repathTimer > 0f)
            return;

        _repathTimer = repathInterval;
        SetDestination(target.position);
    }

    public void SeePlayer(Transform playerTransform)
    {
        if (playerTransform == null)
            return;

        if (target != playerTransform)
        {
            target = playerTransform;
            _targetCollider = null;
            CacheTargetCollider();
        }

        _seesTarget = true;
        _seenThisFrame = true;
    }

    private void UpdateVisionRetention()
    {
        if (!_seesTarget || target == null)
        {
            _seenThisFrame = false;
            return;
        }

        if (_seenThisFrame)
        {
            _seenThisFrame = false;
            return;
        }

        if (Vector2.Distance(transform.position, target.position) > loseVisionRange)
            _seesTarget = false;
    }

    private void StopMoving()
    {
        _agent.isStopped = true;
        _agent.ResetPath();
    }

    private void TryPlaceOnNavMesh()
    {
        if (_agent.isOnNavMesh)
            return;

        if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 2f, NavMesh.AllAreas))
            _agent.Warp(hit.position);
    }

    public void SetTarget(Transform newTarget)
    {
        target = newTarget;
        _targetCollider = null;
        _seesTarget = false;
        CacheTargetCollider();
        _repathTimer = 0f;
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(1f, 0.4f, 0.2f, 0.25f);
        Gizmos.DrawWireSphere(transform.position, loseVisionRange);
    }

    private void CacheTargetCollider()
    {
        if (target == null)
            return;

        _targetCollider = target.GetComponent<Collider2D>();
    }

    private bool IsInContactWithTarget()
    {
        if (_collider == null)
            return false;

        if (_targetCollider == null)
            CacheTargetCollider();

        if (_targetCollider != null)
        {
            var distance = Physics2D.Distance(_collider, _targetCollider);
            return distance.isOverlapped || distance.distance <= ContactPadding;
        }

        return Vector2.Distance(transform.position, target.position) <= stopDistance;
    }

    private void SetDestination(Vector3 destination)
    {
        if (Mathf.Abs(transform.position.x - destination.x) < AgentDrift)
            destination.x += AgentDrift;

        _agent.SetDestination(destination);
    }
}
