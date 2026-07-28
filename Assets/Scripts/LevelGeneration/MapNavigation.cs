using NavMeshPlus.Components;
using NavMeshPlus.Extensions;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Tilemaps;

[DefaultExecutionOrder(-100)]
[RequireComponent(typeof(NavMeshSurface))]
[RequireComponent(typeof(CollectSources2d))]
public class MapNavigation : MonoBehaviour
{
    [SerializeField] private string walkableLayerName = "Walkable";
    [SerializeField] private bool bakeOnStart;
    [SerializeField] private bool hideEditorLogs = true;

    private NavMeshSurface _surface;

    void Awake()
    {
        _surface = GetComponent<NavMeshSurface>();
        ConfigureSurface();
    }

    void Start()
    {
        if (bakeOnStart)
            Bake();
    }

    public void Bake()
    {
        if (_surface == null)
            _surface = GetComponent<NavMeshSurface>();

        try
        {
            PrepareNavigationSources();
            PrepareTilemapSources();
            Physics2D.SyncTransforms();
            _surface.BuildNavMesh();
        }
        catch (System.Exception exception)
        {
            Debug.LogWarning($"MapNavigation: не удалось собрать NavMesh — {exception.Message}");
        }
    }

    private void ConfigureSurface()
    {
        transform.rotation = Quaternion.Euler(-90f, 0f, 0f);

        var walkableLayer = LayerMask.NameToLayer(walkableLayerName);
        if (walkableLayer >= 0)
            _surface.layerMask = (1 << walkableLayer) | (1 << 0);
        else
            _surface.layerMask = ~0;

        _surface.useGeometry = NavMeshCollectGeometry.RenderMeshes;
        _surface.collectObjects = CollectObjects.All;
        _surface.hideEditorLogs = hideEditorLogs;
    }

    private void PrepareNavigationSources()
    {
        var walkableLayer = LayerMask.NameToLayer(walkableLayerName);

        var renderers = FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None);
        for (var i = 0; i < renderers.Length; i++)
        {
            var go = renderers[i].gameObject;
            if (go.GetComponent<NavMeshAgent>() != null)
                continue;
            if (go.CompareTag("Player"))
                continue;

            var modifier = go.GetComponent<NavMeshModifier>();
            if (modifier == null)
                modifier = go.AddComponent<NavMeshModifier>();

            var isWalkable = walkableLayer >= 0 && go.layer == walkableLayer;
            if (isWalkable)
            {
                modifier.overrideArea = false;
                modifier.ignoreFromBuild = false;
                continue;
            }

            if (go.CompareTag("Wall"))
            {
                modifier.overrideArea = true;
                modifier.area = 1;
                modifier.ignoreFromBuild = false;
            }
            else
                modifier.ignoreFromBuild = true;
        }
    }

    private void PrepareTilemapSources()
    {
        var walkableLayer = LayerMask.NameToLayer(walkableLayerName);
        var tilemapRenderers = FindObjectsByType<TilemapRenderer>(FindObjectsSortMode.None);

        for (var i = 0; i < tilemapRenderers.Length; i++)
        {
            var tilemap = tilemapRenderers[i].GetComponent<Tilemap>();
            if (tilemap == null || tilemap.layoutGrid == null)
                continue;

            var go = tilemapRenderers[i].gameObject;
            var modifier = go.GetComponent<NavMeshModifier>();
            if (modifier == null)
                modifier = go.AddComponent<NavMeshModifier>();

            var isFloor = go.name == "Floor" || (walkableLayer >= 0 && go.layer == walkableLayer);
            if (isFloor)
            {
                modifier.overrideArea = false;
                modifier.ignoreFromBuild = false;
                continue;
            }

            if (go.name == "Wall" || go.CompareTag("Wall"))
            {
                modifier.overrideArea = true;
                modifier.area = 1;
                modifier.ignoreFromBuild = false;
            }
            else
                modifier.ignoreFromBuild = true;
        }
    }
}
