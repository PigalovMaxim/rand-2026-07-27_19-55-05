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
            var startFloorTile = AssetDatabase.LoadAssetAtPath<TileBase>("Assets/Tiles/StartFloorTile.asset");
            var bossFloorTile = AssetDatabase.LoadAssetAtPath<TileBase>("Assets/Tiles/BossFloorTile.asset");
            var itemFloorTile = AssetDatabase.LoadAssetAtPath<TileBase>("Assets/Tiles/ItemFloorTile.asset");

            if (floorTile == null || wallTile == null || startFloorTile == null || bossFloorTile == null || itemFloorTile == null)
            {
                EditorUtility.DisplayDialog(
                    "Level Generation",
                    "Не найдены тайлы в Assets/Tiles/.",
                    "OK");
                return;
            }

            var grid = FindOrCreateGrid();
            var floorTilemap = FindOrCreateTilemap(grid.transform, "Floor", 0, true);
            var wallTilemap = FindOrCreateTilemap(grid.transform, "Wall", 1, false);
            var levelGenerator = FindOrCreateLevelGenerator(
                floorTilemap,
                wallTilemap,
                floorTile,
                wallTile,
                startFloorTile,
                bossFloorTile,
                itemFloorTile);
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
                if (existing.GetComponent<Grid>() == null)
                {
                    existing.AddComponent<Grid>();
                }

                return existing;
            }

            var gridObject = new GameObject("Grid");
            gridObject.AddComponent<Grid>();
            return gridObject;
        }

        private static Tilemap FindOrCreateTilemap(Transform parent, string name, int sortingOrder, bool isWalkableFloor)
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

            if (isWalkableFloor)
            {
                var walkableLayer = LayerMask.NameToLayer("Walkable");
                if (walkableLayer >= 0)
                {
                    tilemapObject.layer = walkableLayer;
                }
            }
            else
            {
                tilemapObject.tag = "Wall";
            }

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
            TileBase wallTile,
            TileBase startFloorTile,
            TileBase bossFloorTile,
            TileBase itemFloorTile)
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
            serializedGenerator.FindProperty("startFloorTile").objectReferenceValue = startFloorTile;
            serializedGenerator.FindProperty("bossFloorTile").objectReferenceValue = bossFloorTile;
            serializedGenerator.FindProperty("itemFloorTile").objectReferenceValue = itemFloorTile;
            serializedGenerator.FindProperty("roomCount").intValue = 8;
            serializedGenerator.FindProperty("itemRoomCount").intValue = 1;
            serializedGenerator.FindProperty("minRoomSize").intValue = 6;
            serializedGenerator.FindProperty("maxRoomSize").intValue = 12;
            serializedGenerator.FindProperty("mapWidth").intValue = 48;
            serializedGenerator.FindProperty("mapHeight").intValue = 32;
            serializedGenerator.FindProperty("enemiesPerRoom").intValue = 1;
            serializedGenerator.FindProperty("generateOnStart").boolValue = true;
            serializedGenerator.FindProperty("useRandomSeed").boolValue = true;
            serializedGenerator.FindProperty("anchorGenerationToPlayer").boolValue = true;
            serializedGenerator.FindProperty("disableLegacyRoom").boolValue = true;
            serializedGenerator.FindProperty("disableSceneEnemy").boolValue = true;

            var mapNavigation = Object.FindFirstObjectByType<MapNavigation>();
            if (mapNavigation != null)
            {
                serializedGenerator.FindProperty("mapNavigation").objectReferenceValue = mapNavigation;
            }

            var player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                serializedGenerator.FindProperty("playerTransform").objectReferenceValue = player.transform;
            }

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
            camera.orthographicSize = 8f;
            camera.transform.position = new Vector3(0f, 0f, -10f);
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
