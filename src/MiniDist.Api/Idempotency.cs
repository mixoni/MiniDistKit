namespace MiniDist.Api;

public interface IIdempotencyStore
{
    bool Exists(string key);
    void Put(string key);
}

public class InMemoryIdempotencyStore : IIdempotencyStore
{
    private readonly HashSet<string> _set = new();
    private readonly object _gate = new();

    public bool Exists(string key) { lock (_gate) return _set.Contains(key); }
    public void Put(string key) { lock (_gate) _set.Add(key); }
}

public class IdempotencyMiddleware : IMiddleware
{
    private readonly IIdempotencyStore _store;
    private readonly ILogger<IdempotencyMiddleware> _log;

    public IdempotencyMiddleware(IIdempotencyStore store, ILogger<IdempotencyMiddleware> log)
    {
        _store = store; _log = log;
    }

    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        // If a client sends an Idempotency-Key, let's ensure 'at-most-once' at the API level (demo).
        if (context.Request.Method is "POST" or "PUT")
        {
            var key = context.Request.Headers["Idempotency-Key"].ToString();
            if (!string.IsNullOrWhiteSpace(key))
            {
                if (_store.Exists(key))
                {
                    _log.LogInformation("Idempotent replay blocked: {Key}", key);
                    context.Response.StatusCode = StatusCodes.Status409Conflict;
                    await context.Response.WriteAsync("Duplicate command (idempotent).");
                    return;
                }
                _store.Put(key);
            }
        }
        await next(context);
    }
}
