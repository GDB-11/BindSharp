# BindSharp 2.2.0 Release Notes

## 🎉 What's New

Version 2.2.0 adds **`finally` support** to all `Try` / `TryAsync` overloads, enabling guaranteed cleanup at the exception-handling boundary with a functional, composable style.

### The Feature: `finally` Parameter

Every `Try` and `TryAsync` overload now accepts an optional `finally` callback that **always executes** — whether the operation succeeds, fails, or throws an exception. This brings the power of try-catch-finally blocks into functional pipelines without breaking composition.

**The Problem:**

Previously, cleanup code around `Try` calls required either:
1. Manual try-finally blocks wrapping the entire pipeline (verbose, breaks composition)
2. Cleanup scattered across `Tap` (success path) and `TapError` (failure path), duplicating logic
3. External using statements that couldn't compose inside the Result flow

```csharp
// ❌ Before: Awkward cleanup patterns
try {
    AcquireLock();
    var result = await Result.TryAsync(
        async () => await ProcessDataAsync(),
        ex => $"Failed: {ex.Message}")
        .BindAsync(ValidateDataAsync)
        .TapAsync(data => _metrics.RecordSuccess())
        .TapErrorAsync(err => _metrics.RecordFailure());  // Duplicated metric logic
    
    return result;
}
finally {
    ReleaseLock();  // Always need this, but can't compose
}
```

**The Solution:**

```csharp
// ✅ After: Clean, composable cleanup
var result = await Result.TryAsync(
        async () => {
            AcquireLock();
            return await ProcessDataAsync();
        },
        ex => $"Failed: {ex.Message}",
        @finally: () => ReleaseLock())           // ✨ Guaranteed cleanup
    .BindAsync(ValidateDataAsync)
    .DoAsync(
        data => _metrics.RecordSuccess(),
        err  => _metrics.RecordFailure());
```

---

## 📦 New API Signatures

All four `Try` / `TryAsync` overloads now include the `finally` parameter:

### 1. Try with Custom Error Factory

```csharp
public static Result<T, TError> Try<T, TError>(
    Func<T> operation,
    Func<Exception, TError> errorFactory,
    Action? @finally = null)                    // ✨ NEW
```

### 2. Try Exception-First

```csharp
public static Result<T, Exception> Try<T>(
    Func<T> operation,
    Action? @finally = null)                    // ✨ NEW
```

### 3. TryAsync with Custom Error Factory

```csharp
public static Task<Result<T, TError>> TryAsync<T, TError>(
    Func<Task<T>> operation,
    Func<Exception, TError> errorFactory,
    Func<Task>? @finally = null)                // ✨ NEW
```

### 4. TryAsync Exception-First

```csharp
public static Task<Result<T, Exception>> TryAsync<T>(
    Func<Task<T>> operation,
    Func<Task>? @finally = null)                // ✨ NEW
```

---

## 💡 Usage Examples

### Example 1: Lock Management

The classic lock acquire/release pattern, now functional:

```csharp
public Result<ProcessedData, string> ProcessWithLock(RawData data)
{
    return Result.Try(
        () => {
            _lock.Acquire();
            return ProcessData(data);
        },
        ex => $"Processing failed: {ex.Message}",
        @finally: () => _lock.Release()              // ✅ Always releases
    );
}
```

**Without `finally`, you'd need:**

```csharp
// ❌ Verbose and breaks composition
try {
    _lock.Acquire();
    return Result.Try(
        () => ProcessData(data),
        ex => $"Processing failed: {ex.Message}");
}
finally {
    _lock.Release();
}
```

### Example 2: Database Connection Lifecycle

```csharp
public async Task<Result<QueryResult, string>> ExecuteQueryAsync(string sql)
{
    return await Result.TryAsync(
        async () => {
            await _db.OpenConnectionAsync();
            return await _db.QueryAsync(sql);
        },
        ex => $"Query failed: {ex.Message}",
        @finally: async () => await _db.CloseConnectionAsync()  // ✅ Always closes
    );
}
```

### Example 3: Metrics Recording (Success and Failure)

```csharp
public async Task<Result<Response, string>> CallExternalApiAsync(string endpoint)
{
    var stopwatch = Stopwatch.StartNew();

    return await Result.TryAsync(
        async () => await _httpClient.GetAsync(endpoint),
        ex => $"API call failed: {ex.Message}",
        @finally: async () => {                                  // ✅ Always records
            stopwatch.Stop();
            await _metrics.RecordLatencyAsync(endpoint, stopwatch.Elapsed);
            await _metrics.IncrementRequestCountAsync(endpoint);
        })
        .TapErrorAsync(ex => _logger.LogError(ex, "Request to {Endpoint} failed", endpoint))
        .MapErrorAsync(ex => "Service unavailable");
}
```

### Example 4: Exception-First with Cleanup

```csharp
// Log exception details, then transform, with guaranteed cleanup
public async Task<Result<Data, string>> FetchDataAsync(string url)
{
    return await Result.TryAsync(
            async () => await _client.GetStringAsync(url),
            @finally: async () => await _metrics.RecordAttemptAsync("fetch"))
        .TapErrorAsync(ex => {
            // Full exception context available
            _logger.LogError(ex, "Fetch failed for {Url}", url);
            if (ex is HttpRequestException http)
                _alerting.NotifyNetworkError(http.StatusCode);
        })
        .MapErrorAsync(ex => ex switch {
            HttpRequestException    => "Network error",
            TaskCanceledException   => "Request timeout",
            _                       => "Failed to fetch data"
        });
}
```

### Example 5: Nested Try Calls with Independent Cleanup

```csharp
public async Task<Result<Report, string>> GenerateReportAsync(int orderId)
{
    return await Result.TryAsync(
            async () => await _db.BeginTransactionAsync(),
            ex => "Failed to start transaction",
            @finally: async () => await _db.CloseConnectionAsync())  // DB cleanup
        .BindAsync(async tx => await Result.TryAsync(
                async () => {
                    var data = await FetchOrderDataAsync(orderId);
                    await tx.CommitAsync();
                    return data;
                },
                ex => $"Data fetch failed: {ex.Message}",
                finally: async () => await tx.DisposeAsync())      // Transaction cleanup
            .MapAsync(data => GenerateReportFromData(data)));
}
```

### Example 6: Resource Reservation Pattern

```csharp
public async Task<Result<Order, string>> ProcessOrderAsync(Order order)
{
    return await Result.TryAsync(
            async () => {
                await _inventory.ReserveItemsAsync(order.Items);
                return order;
            },
            ex => $"Failed to reserve inventory: {ex.Message}",
            @finally: async () => {
                // Release reservation on both success and failure
                await _inventory.ReleaseReservationAsync(order.Items);
            })
        .BindAsync(o => ChargePaymentAsync(o))
        .BindAsync(o => CreateOrderRecordAsync(o));
}
```

---

## 🔄 Migration from 2.1.x

**No migration needed!** All changes are **100% backward compatible**.

### What Continues to Work

Every existing `Try` and `TryAsync` call without a `finally` parameter continues to work exactly as before:

```csharp
// ✅ All existing code still works
var result1 = Result.Try(
    () => int.Parse(input),
    ex => $"Parse failed: {ex.Message}"
);

var result2 = await Result.TryAsync(
    async () => await _api.FetchDataAsync(),
    ex => "API error"
);

var result3 = Result.Try(() => File.ReadAllText(path));

var result4 = await Result.TryAsync(
    async () => await _db.QueryAsync(sql)
);
```

### What You Can Now Do

Simply add the `finally` parameter when you need guaranteed cleanup:

```csharp
// ✨ New capability — add finally for cleanup
var result1 = Result.Try(
    () => int.Parse(input),
    ex => $"Parse failed: {ex.Message}",
    @finally: () => _metrics.RecordParseAttempt()
);

var result2 = await Result.TryAsync(
    async () => await _api.FetchDataAsync(),
    ex => "API error",
    @finally: async () => await _metrics.RecordApiCallAsync()
);
```

---

## 🎯 When to Use `finally`

### ✅ Use `finally` when:

1. **Resource acquisition happens inside the operation** — locks acquired, connections opened, reservations made
2. **Cleanup must happen regardless of outcome** — release locks, close connections, record metrics
3. **The cleanup belongs at the exception-handling boundary** — right where `Try` wraps the external API
4. **Cleanup logic is the same for success and failure** — no need to branch, just execute

### ❌ Don't use `finally` when:

1. **Cleanup needs to branch on success/failure** — use `Do` / `DoAsync` instead
2. **Resource is managed by `IDisposable`** — use `Using` / `UsingAsync` for proper disposal semantics
3. **No cleanup is needed** — omit the parameter entirely
4. **Cleanup is deep in the pipeline** — use `Tap` or `Do` at that step instead

---

## 📊 Comparison: `finally` vs Alternatives

| Pattern | Use Case | Example |
|---------|----------|---------|
| **`finally` parameter** | Cleanup at exception boundary, same for success/failure | Release lock, close connection, record attempt metric |
| **`Do` / `DoAsync`** | Different cleanup for success vs failure | Success → cache result, Failure → retry scheduler |
| **`Using` / `UsingAsync`** | `IDisposable` resource management | Database transaction, file stream, HTTP response |
| **`Tap` / `TapAsync`** | Success-only side effect | Update cache on success |
| **`TapError` / `TapErrorAsync`** | Failure-only side effect | Alert admin on error |

---

## 🎓 Best Practices

### DO:

✅ **Use `finally` for resource cleanup** at the `Try` boundary
```csharp
Result.Try(
    () => { AcquireLock(); return ProcessData(); },
    ex => "Failed",
    @finally: () => ReleaseLock()
)
```

✅ **Use `finally` for unconditional metrics/logging**
```csharp
Result.TryAsync(
    async () => await CallApiAsync(),
    ex => "API error",
    @finally: async () => await _metrics.RecordAttemptAsync()
)
```

✅ **Combine `finally` with exception-first for logging + cleanup**
```csharp
Result.Try(() => operation(), finally: () => Cleanup())
    .TapError(ex => _logger.LogError(ex, "Failed"))
    .MapError(ex => "User-friendly message")
```

✅ **Use async `finally` for async cleanup**
```csharp
Result.TryAsync(
    async () => await operation(),
    @finally: async () => await CloseConnectionAsync()
)
```

### DON'T:

❌ **Don't use `finally` when you need to branch on outcome**
```csharp
// ❌ Bad — branching inside finally
Result.Try(
    () => operation(),
    @finally: () => {
        if (/* somehow check result? */) CacheResult();
        else ScheduleRetry();  // Can't access result state!
    }
)

// ✅ Good — use Do instead
Result.Try(() => operation())
    .Do(
        result => CacheResult(),
        error  => ScheduleRetry()
    )
```

❌ **Don't duplicate cleanup across success/failure paths**
```csharp
// ❌ Bad — cleanup duplicated
Result.Try(() => operation())
    .Tap(r => { RecordMetrics(); Cleanup(); })
    .TapError(e => { RecordMetrics(); Cleanup(); })

// ✅ Good — cleanup once in finally
Result.Try(
    () => operation(),
    @finally: () => { RecordMetrics(); Cleanup(); })
```

❌ **Don't use `finally` for disposal — use `Using` instead**
```csharp
// ❌ Bad — manual disposal
Result.Try(
    () => {
        var stream = File.OpenRead(path);
        return ProcessStream(stream);
    },
    @finally: () => stream?.Dispose()  // stream is out of scope!
)

// ✅ Good — proper disposal
Result.Try(() => File.OpenRead(path))
    .Using(stream => ProcessStream(stream))
```

---

## 📋 Changelog

### Added

- **`finally` parameter** to `Result.Try<T, TError>` — optional `Action?` for guaranteed cleanup
- **`finally` parameter** to `Result.Try<T>` — optional `Action?` for guaranteed cleanup
- **`finally` parameter** to `Result.TryAsync<T, TError>` — optional `Func<Task>?` for guaranteed async cleanup
- **`finally` parameter** to `Result.TryAsync<T>` — optional `Func<Task>?` for guaranteed async cleanup
- Comprehensive XML documentation for new parameters
- 16 unit tests covering all finally scenarios (sync/async, success/fail, exception paths)

### Changed

- None (backward compatible)

### Fixed

- None

### Removed

- None

---

## 🔗 Related Features

This feature complements existing BindSharp capabilities:

- **`Using` / `UsingAsync`** (v1.1.0) — For `IDisposable` resource management with guaranteed disposal
- **`Do` / `DoAsync`** (v2.0.0) — For branching cleanup logic based on success/failure
- **`Tap` / `TapAsync`** (v1.1.0) — For success-only side effects
- **`TapError` / `TapErrorAsync`** (v1.5.0) — For failure-only side effects
- **Exception-first Try** (v1.6.0) — Returns `Result<T, Exception>` for full exception inspection

---

## 🚀 Upgrade Today

```bash
dotnet add package BindSharp --version 2.2.0
```

Or update your `.csproj`:

```xml
<PackageReference Include="BindSharp" Version="2.2.0" />
```

---

## 💬 Feedback

Found a bug? Have a feature request? Open an issue on [GitHub](https://github.com/BindSharp/BindSharp/issues).

---

## 🙏 Acknowledgments

Thanks to the community for requesting better resource cleanup patterns in functional pipelines. This feature was inspired by real-world scenarios where cleanup needed to happen at the exception boundary but couldn't break composition.

Special thanks to **[Zoran Horvat](https://www.youtube.com/@zoran-horvat)** for his foundational work on Railway-Oriented Programming in C#.

---

**Previous Releases:**
- [2.0.0](RELEASE_NOTES_2_0_0.md) — `Do`/`DoAsync` dual side effects
- [1.6.0](RELEASE_NOTES_1_6_0.md) — Exception-first `Try`, mixed async/sync
- [1.5.0](RELEASE_NOTES_1_5_0.md) — `TapError` / `TapErrorAsync`
- [1.4.1](RELEASE_NOTES_1_4_1.md) — `BindIf` conditional processing
- [1.3.0](RELEASE_NOTES_1_3_0.md) — Equality & implicit conversions
- [1.2.0](RELEASE_NOTES_1_2_0.md) — `Unit` type
- [1.1.0](RELEASE_NOTES_1_1_0.md) — `Ensure`, `Tap`, `Using`, `ToResult`

---

Built with ❤️ for the .NET community
