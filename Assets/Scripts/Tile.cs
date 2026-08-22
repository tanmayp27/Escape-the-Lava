using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

// Specifies the functional role and behavior of a grid tile.
public enum TileType
{
    Island,  // Safe tile
    Lava,    // Hazard tile
    Diamond, // Collectible tile
    Wall,    // Border wall
    Corner   // Border corner
}

// Handles user interaction, click detection, and event processing for individual board tiles.
[RequireComponent(typeof(BoxCollider2D))]
public class Tile : MonoBehaviour
{
    [Header("Tile Settings")]
    public TileType type = TileType.Island;

    [Header("Events")]
    public UnityEvent onTileClicked;

    private BoxCollider2D boxCollider;
    private int lastProcessedFrame = -1;

    private void Awake()
    {
        EnsureCollider();
    }

    private void EnsureCollider()
    {
        boxCollider = GetComponent<BoxCollider2D>();
        if (boxCollider == null)
        {
            boxCollider = gameObject.AddComponent<BoxCollider2D>();
        }

        SpriteRenderer sr = GetComponent<SpriteRenderer>();
        if (sr != null && sr.sprite != null)
        {
            boxCollider.size = sr.sprite.rect.size / sr.sprite.pixelsPerUnit;
        }
        else
        {
            boxCollider.size = new Vector2(1f, 1f);
        }
    }

    private void Update()
    {
        bool wasPressed = false;
        Vector2 pressPosition = Vector2.zero;

        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            wasPressed = true;
            pressPosition = Mouse.current.position.ReadValue();
        }
        else if (Pointer.current != null && Pointer.current.press.wasPressedThisFrame)
        {
            wasPressed = true;
            pressPosition = Pointer.current.position.ReadValue();
        }

        if (wasPressed && Camera.main != null)
        {
            if (UnityEngine.EventSystems.EventSystem.current != null && UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject())
            {
                return;
            }

            Vector3 worldPoint = Camera.main.ScreenToWorldPoint(pressPosition);
            RaycastHit2D hit = Physics2D.Raycast(worldPoint, Vector2.zero);
            if (hit.collider != null && hit.collider.gameObject == gameObject)
            {
                OnTilePressed();
            }
        }
    }

    private void OnMouseDown()
    {
        OnTilePressed();
    }

    // Processes input interaction on the tile, triggering visual feedback, audio, and tile effect logic.
    public void OnTilePressed()
    {
        if (Time.frameCount == lastProcessedFrame) return;
        lastProcessedFrame = Time.frameCount;

        if (GameManager.Instance != null && GameManager.Instance.CurrentState != GameState.Playing)
        {
            return;
        }

        onTileClicked?.Invoke();

        Vector3 clickPosition = GetClickWorldPosition();

        if (VFXController.Instance != null)
        {
            VFXController.Instance.PlayVFX(type, clickPosition);
        }

        switch (type)
        {
            case TileType.Diamond:
                HandleDiamondClick();
                break;
            case TileType.Lava:
                HandleLavaClick();
                break;
            case TileType.Island:
                HandleIslandClick();
                break;
            case TileType.Wall:
            case TileType.Corner:
                break;
        }
    }

    private Vector3 GetClickWorldPosition()
    {
        if (Camera.main != null)
        {
            Vector2 mouseScreen2D = Vector2.zero;
            if (Mouse.current != null)
            {
                mouseScreen2D = Mouse.current.position.ReadValue();
            }
            else if (Pointer.current != null)
            {
                mouseScreen2D = Pointer.current.position.ReadValue();
            }

            Vector3 mouseScreenPos = new Vector3(mouseScreen2D.x, mouseScreen2D.y, Mathf.Abs(Camera.main.transform.position.z - transform.position.z));
            Vector3 worldPos = Camera.main.ScreenToWorldPoint(mouseScreenPos);
            worldPos.z = 0f;
            return worldPos;
        }

        return transform.position;
    }

    private void HandleDiamondClick()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.AddScore(100);
            GameManager.Instance.OnDiamondCollected();
        }
        
        Destroy(gameObject);
    }

    private void HandleLavaClick()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.TakeDamage(1);
        }
    }

    private void HandleIslandClick()
    {
    }
}
