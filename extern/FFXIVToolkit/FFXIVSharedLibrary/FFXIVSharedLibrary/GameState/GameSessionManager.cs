namespace FFXIVSharedLibrary.GameState;

public enum GameSessionState
{
    Inactive,
    Active,
    Paused,
    Completed
}

public delegate void GameStateChangedHandler<T>(GameSessionState oldState, GameSessionState newState, T gameState);
public delegate void GameSessionCompletedHandler<T>(T gameState, string? winner);

public class GameSessionManager<T> where T : class, new()
{
    private GameSessionState currentState = GameSessionState.Inactive;
    private T gameState = new();
    private CancellationTokenSource? cancellationTokenSource;
    private readonly object lockObject = new object();

    public GameSessionState CurrentState
    {
        get
        {
            lock (lockObject)
            {
                return currentState;
            }
        }
    }

    public T GameState
    {
        get
        {
            lock (lockObject)
            {
                return gameState;
            }
        }
    }

    public bool IsActive => CurrentState == GameSessionState.Active;
    public bool IsInactive => CurrentState == GameSessionState.Inactive;
    public bool IsPaused => CurrentState == GameSessionState.Paused;
    public bool IsCompleted => CurrentState == GameSessionState.Completed;

    public CancellationToken CancellationToken => cancellationTokenSource?.Token ?? CancellationToken.None;

    public event GameStateChangedHandler<T>? StateChanged;
    public event GameSessionCompletedHandler<T>? SessionCompleted;

    public bool StartSession(T? initialState = null)
    {
        lock (lockObject)
        {
            if (currentState == GameSessionState.Active)
                return false;

            var oldState = currentState;
            gameState = initialState ?? new T();
            currentState = GameSessionState.Active;
            cancellationTokenSource?.Cancel();
            cancellationTokenSource = new CancellationTokenSource();

            StateChanged?.Invoke(oldState, currentState, gameState);
            return true;
        }
    }

    public bool PauseSession()
    {
        lock (lockObject)
        {
            if (currentState != GameSessionState.Active)
                return false;

            var oldState = currentState;
            currentState = GameSessionState.Paused;
            StateChanged?.Invoke(oldState, currentState, gameState);
            return true;
        }
    }

    public bool ResumeSession()
    {
        lock (lockObject)
        {
            if (currentState != GameSessionState.Paused)
                return false;

            var oldState = currentState;
            currentState = GameSessionState.Active;
            StateChanged?.Invoke(oldState, currentState, gameState);
            return true;
        }
    }

    public bool StopSession(string? winner = null)
    {
        lock (lockObject)
        {
            if (currentState == GameSessionState.Inactive)
                return false;

            var oldState = currentState;
            currentState = GameSessionState.Inactive;
            cancellationTokenSource?.Cancel();

            SessionCompleted?.Invoke(gameState, winner);
            StateChanged?.Invoke(oldState, currentState, gameState);

            gameState = new T();
            return true;
        }
    }

    public bool CompleteSession(string? winner = null)
    {
        lock (lockObject)
        {
            if (currentState != GameSessionState.Active)
                return false;

            var oldState = currentState;
            currentState = GameSessionState.Completed;

            SessionCompleted?.Invoke(gameState, winner);
            StateChanged?.Invoke(oldState, currentState, gameState);
            return true;
        }
    }

    public void UpdateGameState(Action<T> updateAction)
    {
        lock (lockObject)
        {
            updateAction(gameState);
        }
    }

    public TResult GetGameStateValue<TResult>(Func<T, TResult> selector)
    {
        lock (lockObject)
        {
            return selector(gameState);
        }
    }

    public Task<bool> StartSessionWithTimeout(TimeSpan timeout, T? initialState = null)
    {
        if (!StartSession(initialState))
            return Task.FromResult(false);

        _ = Task.Delay(timeout, CancellationToken).ContinueWith(task =>
        {
            if (!task.IsCanceled)
            {
                StopSession();
            }
        });

        return Task.FromResult(true);
    }

    public void Dispose()
    {
        StopSession();
        cancellationTokenSource?.Dispose();
    }
}

public class TimedGameSessionManager<T> : GameSessionManager<T> where T : class, new()
{
    private Timer? sessionTimer;
    private readonly TimeSpan defaultTimeout;

    public TimedGameSessionManager(TimeSpan defaultTimeout)
    {
        this.defaultTimeout = defaultTimeout;
    }

    public bool StartSession(T? initialState = null, TimeSpan? customTimeout = null)
    {
        if (!base.StartSession(initialState))
            return false;

        var timeout = customTimeout ?? defaultTimeout;
        sessionTimer?.Dispose();
        sessionTimer = new Timer(OnTimerElapsed, null, timeout, Timeout.InfiniteTimeSpan);
        return true;
    }

    public new bool StopSession(string? winner = null)
    {
        sessionTimer?.Dispose();
        sessionTimer = null;
        return base.StopSession(winner);
    }

    private void OnTimerElapsed(object? state)
    {
        StopSession();
    }

    public new void Dispose()
    {
        sessionTimer?.Dispose();
        base.Dispose();
    }
}