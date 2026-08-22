using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

// Handles procedural tile grid creation, noise-based tile type distribution,
// dynamic tile spacing, and surrounding border wall/corner placement.
public class GridManager : MonoBehaviour
{
    [Header("Grid Dimensions")]
    public int columns = 16;
    public int rows = 8;

    [Tooltip("Tile spacing in world units when auto-detection is disabled.")]
    public float tileSize = 2.56f;

    [Tooltip("Automatically calculates tile spacing from sprite bounds to prevent overlap.")]
    public bool autoDetectTileSize = true;

    [Header("Tile Prefabs")]
    [Tooltip("Prefab for Island / Grass safe tiles.")]
    public GameObject islandPrefab;
    
    [Tooltip("Prefab for Lava danger tiles.")]
    public GameObject lavaPrefab;
    
    [Tooltip("Prefab for Diamond collectible tiles.")]
    public GameObject diamondPrefab;

    [Header("Border Prefabs")]
    [Tooltip("Wall tile prefab for grid borders.")]
    public GameObject wallPrefab;

    [Tooltip("Corner tile prefab for grid borders.")]
    public GameObject cornerPrefab;

    [Tooltip("Rotation angle offset for wall tiles in degrees.")]
    public float wallRotationOffset = 0f;

    [Tooltip("Rotation angle offset for corner tiles in degrees.")]
    public float cornerRotationOffset = 0f;

    [Header("Tile Distribution Counts")]
    public int greenTileCount = 64; //Island safe tiles, occupy half the total grid tile count (128 tiles)
    
    public int blueTileCount = 25;//diamond collectible tiles, occupy 25 tiles of the total grid tile count

    public int redTileCount = 39; //lava danger tiles, occupy the remaining tiles of the total grid tile count

    [Header("Parent Container")]
    [Tooltip("Parent transform under which grid tiles are instantiated.")]
    public Transform board;

    [Header("Procedural Generation Settings")]
    public float noiseScale = 0.25f;
    public bool randomizeSeedOnRefresh = true;
    public float seedX = 0f;
    public float seedY = 0f;

    private void Start()
    {
        GenerateGrid();
    }

    private void Update()
    {
        // // Debug shortcut to regenerate grid (R or Space key) Meant solely for testing
        // if (Keyboard.current != null && (Keyboard.current.rKey.wasPressedThisFrame || Keyboard.current.spaceKey.wasPressedThisFrame))
        // {
        //     GenerateGrid();
        // }
    }

    // Calculates effective tile spacing using prefab sprite bounds auto-detection or fallback size.
    public float GetEffectiveTileSize()
    {
        if (autoDetectTileSize && islandPrefab != null)
        {
            SpriteRenderer sr = islandPrefab.GetComponent<SpriteRenderer>();
            if (sr == null)
            {
                sr = islandPrefab.GetComponentInChildren<SpriteRenderer>();
            }

            if (sr != null && sr.sprite != null)
            {
                return sr.sprite.rect.width / sr.sprite.pixelsPerUnit;
            }
        }

        return tileSize;
    }

    // Clears any existing grid and procedurally generates a new tile layout surrounded by border walls.
    [ContextMenu("Generate Grid")]
    public void GenerateGrid()
    {
        ClearGrid();

        int totalCells = columns * rows;
        if (totalCells <= 0) return;

        Transform parentHolder = GetGridParent();

        if (randomizeSeedOnRefresh)
        {
            seedX = Random.Range(0f, 1000f);
            seedY = Random.Range(0f, 1000f);
        }

        float spacing = GetEffectiveTileSize();

        // Sample Perlin noise for grid coordinates
        List<CellData> cells = new List<CellData>(totalCells);
        for (int x = 0; x < columns; x++)
        {
            for (int y = 0; y < rows; y++)
            {
                float noiseVal = Mathf.PerlinNoise((x + seedX) * noiseScale, (y + seedY) * noiseScale);
                cells.Add(new CellData(x, y, noiseVal));
            }
        }

        // Sort cells by noise value to form cohesive clusters
        cells.Sort((a, b) => b.noiseValue.CompareTo(a.noiseValue));

        // Assign tile types based on requested distribution counts
        TileType[] assignedTypes = new TileType[totalCells];
        int blueAssigned = 0;
        int redAssigned = 0;

        for (int i = 0; i < cells.Count; i++)
        {
            CellData cell = cells[i];
            int cellIndex = cell.x * rows + cell.y;

            if (blueAssigned < blueTileCount)
            {
                assignedTypes[cellIndex] = TileType.Diamond;
                blueAssigned++;
            }
            else if (redAssigned < redTileCount)
            {
                assignedTypes[cellIndex] = TileType.Lava;
                redAssigned++;
            }
            else
            {
                assignedTypes[cellIndex] = TileType.Island;
            }
        }

        Vector3 originOffset = new Vector3((columns - 1) * spacing * 0.5f, (rows - 1) * spacing * 0.5f, 0f);

        // Instantiate grid tiles
        for (int x = 0; x < columns; x++)
        {
            for (int y = 0; y < rows; y++)
            {
                int index = x * rows + y;
                TileType type = assignedTypes[index];
                GameObject prefabToSpawn = GetPrefabForType(type);

                if (prefabToSpawn == null)
                {
                    Debug.LogWarning($"[GridManager] Prefab for tile type {type} is not assigned in the Inspector!");
                    continue;
                }

                Vector3 localPos = new Vector3(x * spacing, y * spacing, 0f) - originOffset;
                Vector3 worldPos = parentHolder.position + localPos;

                GameObject tileObj = Instantiate(prefabToSpawn, worldPos, Quaternion.identity, parentHolder);
                tileObj.name = $"Tile_{x}_{y}_{type}";

                Tile tileComponent = tileObj.GetComponent<Tile>();
                if (tileComponent == null)
                {
                    tileComponent = tileObj.AddComponent<Tile>();
                }
                tileComponent.type = type;
            }
        }

        // Generate surrounding border walls and corners
        GenerateBorder(parentHolder, spacing, originOffset);

        if (GameManager.Instance != null)
        {
            GameManager.Instance.SetTotalDiamonds(blueAssigned);
        }

        Debug.Log($"[GridManager] Grid Generated: {columns}x{rows} with spacing {spacing:F2} centered at {parentHolder.position}. (Blue: {blueAssigned}, Red: {redAssigned}, Green: {totalCells - blueAssigned - redAssigned})");
    }

    // Spawns wall and corner border tiles surrounding the active grid bounds.
    private void GenerateBorder(Transform parentHolder, float spacing, Vector3 originOffset)
    {
        if (wallPrefab == null && cornerPrefab == null)
        {
            return;
        }

        // Spawn Corners
        if (cornerPrefab != null)
        {
            SpawnBorderTile(cornerPrefab, -1, rows, Quaternion.Euler(0f, 0f, 0f + cornerRotationOffset), "Corner_TopLeft", parentHolder, spacing, originOffset, TileType.Corner);
            SpawnBorderTile(cornerPrefab, columns, rows, Quaternion.Euler(0f, 0f, -90f + cornerRotationOffset), "Corner_TopRight", parentHolder, spacing, originOffset, TileType.Corner);
            SpawnBorderTile(cornerPrefab, columns, -1, Quaternion.Euler(0f, 0f, 180f + cornerRotationOffset), "Corner_BottomRight", parentHolder, spacing, originOffset, TileType.Corner);
            SpawnBorderTile(cornerPrefab, -1, -1, Quaternion.Euler(0f, 0f, 90f + cornerRotationOffset), "Corner_BottomLeft", parentHolder, spacing, originOffset, TileType.Corner);
        }

        // Spawn Walls
        if (wallPrefab != null)
        {
            for (int y = 0; y < rows; y++)
            {
                SpawnBorderTile(wallPrefab, -1, y, Quaternion.Euler(0f, 0f, 0f + wallRotationOffset), $"Wall_Left_{y}", parentHolder, spacing, originOffset, TileType.Wall);
            }

            for (int y = 0; y < rows; y++)
            {
                SpawnBorderTile(wallPrefab, columns, y, Quaternion.Euler(0f, 0f, 180f + wallRotationOffset), $"Wall_Right_{y}", parentHolder, spacing, originOffset, TileType.Wall);
            }

            for (int x = 0; x < columns; x++)
            {
                SpawnBorderTile(wallPrefab, x, rows, Quaternion.Euler(0f, 0f, -90f + wallRotationOffset), $"Wall_Top_{x}", parentHolder, spacing, originOffset, TileType.Wall);
            }

            for (int x = 0; x < columns; x++)
            {
                SpawnBorderTile(wallPrefab, x, -1, Quaternion.Euler(0f, 0f, 90f + wallRotationOffset), $"Wall_Bottom_{x}", parentHolder, spacing, originOffset, TileType.Wall);
            }
        }
    }

    private void SpawnBorderTile(GameObject prefab, int gridX, int gridY, Quaternion rotation, string objectName, Transform parentHolder, float spacing, Vector3 originOffset, TileType borderType)
    {
        Vector3 localPos = new Vector3(gridX * spacing, gridY * spacing, 0f) - originOffset;
        Vector3 worldPos = parentHolder.position + localPos;

        GameObject tileObj = Instantiate(prefab, worldPos, rotation, parentHolder);
        tileObj.name = objectName;

        Tile tileComponent = tileObj.GetComponent<Tile>();
        if (tileComponent == null)
        {
            tileComponent = tileObj.AddComponent<Tile>();
        }
        tileComponent.type = borderType;
    }

    // Destroys all existing child tile objects under the board container.
    public void ClearGrid()
    {
        Transform parentTransform = GetGridParent();
        if (parentTransform != null)
        {
            for (int i = parentTransform.childCount - 1; i >= 0; i--)
            {
                Transform child = parentTransform.GetChild(i);
                if (Application.isPlaying)
                {
                    Destroy(child.gameObject);
                }
                else
                {
                    DestroyImmediate(child.gameObject);
                }
            }
        }
    }

    private Transform GetGridParent()
    {
        if (board != null)
        {
            return board;
        }

        Transform existingBoard = transform.Find("Board");
        if (existingBoard != null)
        {
            board = existingBoard;
            return board;
        }

        GameObject newBoardObj = new GameObject("Board");
        newBoardObj.transform.SetParent(transform);
        newBoardObj.transform.localPosition = Vector3.zero;
        board = newBoardObj.transform;
        return board;
    }

    private GameObject GetPrefabForType(TileType type)
    {
        switch (type)
        {
            case TileType.Diamond:
                return diamondPrefab;
            case TileType.Lava:
                return lavaPrefab;
            case TileType.Island:
                return islandPrefab;
            default:
                return islandPrefab;
        }
    }

    private struct CellData
    {
        public int x;
        public int y;
        public float noiseValue;

        public CellData(int x, int y, float noiseValue)
        {
            this.x = x;
            this.y = y;
            this.noiseValue = noiseValue;
        }
    }
}
