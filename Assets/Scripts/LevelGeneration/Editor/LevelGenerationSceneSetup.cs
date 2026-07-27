#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.Tilemaps;

namespace Roguelike.LevelGeneration.Editor
{
    /// <summary>
    /// Создаёт Grid, Tilemap и LevelGenerator в активной сцене.
    /// </summary>
    public static class LevelGenerationSceneSetup
    {
        private const string MenuPath = "Roguelike/Setup Level Generation Scene";

        [InitializeOnLoadMethod]
        private static void RegisterSceneSetup()
        {
            EditorApplication.delayCall += TryAutoSetupOpenScene;
        }

        [MenuItem(MenuPath)]
        public static void SetupScene()
        {
            var floorTile = AssetDatabase.LoadAssetAtPath<TileBase>("Assets/Tiles/FloorTile.asset");
            var wallTile = AssetDatabase.LoadAssetAtPath<TileBase>("Assets/Tiles/WallTile.asset");

            if (floorTile == null || wallTile == null)
            {
                EditorUtility.DisplayDialog(
                    "Level Generation",
                    "Не найдены Assets/Tiles/FloorTile.asset или WallTile.asset.",
                    "OK");
                return;
            }

            var grid = FindOrCreateGrid();
            var floorTilemap = FindOrCreateTilemap(grid.transform, "Floor", 0);
            var wallTilemap = FindOrCreateTilemap(grid.transform, "Wall", 1);
            var levelGenerator = FindOrCreateLevelGenerator(floorTilemap, wallTilemap, floorTile, wallTile);
            ConfigureCamera();
            ConfigureGlobalLight();

            Selection.activeGameObject = levelGenerator;
            EditorSceneManager.MarkSceneDirty(levelGenerator.scene);

            Debug.Log("Level Generation: сцена настроена. Нажмите Play или ПКМ → Generate Level.");
        }

        private static void TryAutoSetupOpenScene()
        {
            if (EditorApplication.isPlaying || EditorApplication.isCompiling)
            {
                return;
            }

            if (EditorSceneManager.GetActiveScene().path != "Assets/Scenes/SampleScene.unity")
            {
                return;
            }

            if (Object.FindFirstObjectByType<LevelGenerator>() != null
                && GameObject.Find("Grid") != null)
            {
                return;
            }

            SetupScene();
        }

        private static GameObject FindOrCreateGrid()
        {
            var existing = GameObject.Find("Grid");
            if (existing != null)
            {
                return existing;
            }

            var gridObject = new GameObject("Grid");
            gridObject.AddComponent<Grid>();
            return gridObject;
        }

        private static Tilemap FindOrCreateTilemap(Transform parent, string name, int sortingOrder)
        {
            var existing = parent.Find(name);
            GameObject tilemapObject;

            if (existing != null)
            {
                tilemapObject = existing.gameObject;
            }
            else
            {
                tilemapObject = new GameObject(name);
                tilemapObject.transform.SetParent(parent, false);
                tilemapObject.AddComponent<Tilemap>();
                tilemapObject.AddComponent<TilemapRenderer>();
            }

            var renderer = tilemapObject.GetComponent<TilemapRenderer>();
            renderer.sortingOrder = sortingOrder;
            AssignUnlitMaterial(renderer);

            return tilemapObject.GetComponent<Tilemap>();
        }

        private static void AssignUnlitMaterial(TilemapRenderer renderer)
        {
            var material = AssetDatabase.GetBuiltinExtraResource<Material>("Sprites-Default.mat");
            if (material != null)
            {
                renderer.material = material;
            }
        }

        private static GameObject FindOrCreateLevelGenerator(
            Tilemap floorTilemap,
            Tilemap wallTilemap,
            TileBase floorTile,
            TileBase wallTile)
        {
            var existing = Object.FindFirstObjectByType<LevelGenerator>();
            GameObject generatorObject;

            if (existing != null)
            {
                generatorObject = existing.gameObject;
            }
            else
            {
                generatorObject = new GameObject("LevelGenerator");
                generatorObject.AddComponent<LevelGenerator>();
            }

            var generator = generatorObject.GetComponent<LevelGenerator>();
            var serializedGenerator = new SerializedObject(generator);
            serializedGenerator.FindProperty("floorTilemap").objectReferenceValue = floorTilemap;
            serializedGenerator.FindProperty("wallTilemap").objectReferenceValue = wallTilemap;
            serializedGenerator.FindProperty("floorTile").objectReferenceValue = floorTile;
            serializedGenerator.FindProperty("wallTile").objectReferenceValue = wallTile;
            serializedGenerator.FindProperty("roomCount").intValue = 8;
            serializedGenerator.FindProperty("generateOnStart").boolValue = true;
            serializedGenerator.FindProperty("useRandomSeed").boolValue = true;
            serializedGenerator.ApplyModifiedPropertiesWithoutUndo();

            return generatorObject;
        }

        private static void ConfigureCamera()
        {
            var camera = Camera.main;
            if (camera == null)
            {
                return;
            }

            camera.orthographic = true;
            camera.orthographicSize = 35f;
            camera.transform.position = new Vector3(40f, 30f, -10f);
        }

        private static void ConfigureGlobalLight()
        {
            var globalLights = Object.FindObjectsByType<Light2D>(FindObjectsSortMode.None);

            foreach (var light in globalLights)
            {
                if (light.lightType != Light2D.LightType.Global)
                {
                    continue;
                }

                var sortingLayers = SortingLayer.layers;
                var layerIds = new int[sortingLayers.Length];

                for (var i = 0; i < sortingLayers.Length; i++)
                {
                    layerIds[i] = sortingLayers[i].id;
                }

                light.targetSortingLayers = layerIds;
            }
        }
    }
}
#endif
