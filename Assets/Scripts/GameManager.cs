using System;
using UnityEngine;

// Defines the overall lifecycle state of a game round.
public enum GameState
{
    Playing,
    GameOver,
    LevelComplete
}

// Manages core game rules, statistics (score, lives, timer, remaining diamonds),
// round lifecycle transitions, and system events.
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("Game Configuration")]
    public int maxLives = 5;
    [Tooltip("Round duration in seconds.")]
    public float roundDuration = 30f;

    [Header("References")]
    [Tooltip("Reference to the GridManager component.")]
    [SerializeField] private GridManager gridManager;

    [Header("Current Game Stats")]
    [SerializeField] private int currentScore = 0;
    [SerializeField] private int currentLives = 5;
    [SerializeField] private float remainingTime = 30f;
    [SerializeField] private int remainingDiamonds = 0;
    [SerializeField] private GameState currentState = GameState.Playing;

    public int CurrentScore => currentScore;
    public int CurrentLives => currentLives;
    public float RemainingTime => remainingTime;
    public int RemainingDiamonds => remainingDiamonds;
    public GameState CurrentState => currentState;

    public event Action<int> OnScoreChanged;
    public event Action<int> OnLivesChanged;
    public event Action<float> OnTimerChanged;
    public event Action<GameState> OnGameStateChanged;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    private void Start()
    {
        StartRound();
    }

    private void Update()
    {
        if (currentState != GameState.Playing) return;

        if (remainingTime > 0f)
        {
            remainingTime -= Time.deltaTime;
            if (remainingTime <= 0f)
            {
                remainingTime = 0f;
                TriggerGameOver("Time's Up!");
            }
            OnTimerChanged?.Invoke(remainingTime);
        }
    }

    // Initializes or resets all stats and starts a new gameplay round.
    public void StartRound()
    {
        currentScore = 0;
        currentLives = maxLives;
        remainingTime = roundDuration;
        currentState = GameState.Playing;

        OnScoreChanged?.Invoke(currentScore);
        OnLivesChanged?.Invoke(currentLives);
        OnTimerChanged?.Invoke(remainingTime);
        OnGameStateChanged?.Invoke(currentState);
    }

    // Sets the total count of diamond collectibles required to win the round.
    public void SetTotalDiamonds(int count)
    {
        remainingDiamonds = count;
    }

    // Adds points to the player's current score and notifies listeners.
    public void AddScore(int points)
    {
        if (currentState != GameState.Playing) return;

        currentScore += points;
        OnScoreChanged?.Invoke(currentScore);
    }

    // Decrements player lives by the specified amount and checks for game over.
    public void TakeDamage(int damageAmount)
    {
        if (currentState != GameState.Playing) return;

        currentLives = Mathf.Max(0, currentLives - damageAmount);
        OnLivesChanged?.Invoke(currentLives);

        if (currentLives <= 0)
        {
            TriggerGameOver("Out of Lives!");
        }
    }

    // Decrements remaining diamond count and checks if the win condition is met.
    public void OnDiamondCollected()
    {
        if (currentState != GameState.Playing) return;

        remainingDiamonds = Mathf.Max(0, remainingDiamonds - 1);
        
        if (remainingDiamonds <= 0)
        {
            TriggerLevelComplete();
        }
    }

    [Header("Auto Restart Settings")]
    public bool autoRestartOnGameEnd = true;
    public float autoRestartDelay = 5.0f;

    private Coroutine autoRestartCoroutine;

    // Triggers the Game Over state with an optional reason message.
    public void TriggerGameOver(string reason = "Game Over")
    {
        currentState = GameState.GameOver;
        Debug.Log($"[GameManager] Game Over: {reason}");
        OnGameStateChanged?.Invoke(currentState);
        StartAutoRestartSequence();
    }

    // Triggers the Level Complete state when all collectibles are gathered.
    public void TriggerLevelComplete()
    {
        currentState = GameState.LevelComplete;
        Debug.Log("[GameManager] Level Complete! All diamonds collected.");
        OnGameStateChanged?.Invoke(currentState);
        StartAutoRestartSequence();
    }

    private void StartAutoRestartSequence()
    {
        if (!autoRestartOnGameEnd) return;

        if (autoRestartCoroutine != null)
        {
            StopCoroutine(autoRestartCoroutine);
        }
        autoRestartCoroutine = StartCoroutine(AutoRestartRoutine());
    }

    private System.Collections.IEnumerator AutoRestartRoutine()
    {
        yield return new WaitForSeconds(autoRestartDelay);
        RestartRound();
    }

    // Resets the current round and regenerates the tile grid.
    public void RestartRound()
    {
        if (autoRestartCoroutine != null)
        {
            StopCoroutine(autoRestartCoroutine);
            autoRestartCoroutine = null;
        }

        StartRound();

        if (gridManager != null)
        {
            gridManager.GenerateGrid();
        }
    }
}
