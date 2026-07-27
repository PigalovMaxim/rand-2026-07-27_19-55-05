using NavMeshPlus.Components;
using NavMeshPlus.Extensions;
using UnityEngine;
using UnityEngine.AI;

[DefaultExecutionOrder(-100)]
[RequireComponent(typeof(NavMeshSurface))]
[RequireComponent(typeof(CollectSources2d))]
public class MapNavigation : MonoBehaviour
{
    [SerializeField] private string walkableLayerName = "Walkable";
    [SerializeField] private bool bakeOnStart = true;
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

        PrepareNavigationSources();
        Physics2D.SyncTransforms();
        _surface.BuildNavMesh();
    }

    private void ConfigureSurface()
    {
        transform.rotation = Quaternion.Euler(-90f, 0f, 0f);

        int walkableLayer = LayerMask.NameToLayer(walkableLayerName);
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
        int walkableLayer = LayerMask.NameToLayer(walkableLayerName);

        SpriteRenderer[] renderers = FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None);
        for (int i = 0; i < renderers.Length; i++)
        {
            GameObject go = renderers[i].gameObject;
            if (go.GetComponent<NavMeshAgent>() != null)
                continue;
            if (go.CompareTag("Player"))
                continue;

            NavMeshModifier modifier = go.GetComponent<NavMeshModifier>();
            if (modifier == null)
                modifier = go.AddComponent<NavMeshModifier>();

            bool isWalkable = walkableLayer >= 0 && go.layer == walkableLayer;
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
            {
                modifier.ignoreFromBuild = true;
            }
        }
    }
}
