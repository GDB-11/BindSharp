# BindSharp 1.4.0 Release Notes

## 🎉 What's New

Version 1.4.0 introduces **conditional branching** to functional pipelines, enabling you to create clean short-circuit logic without breaking your Railway-Oriented Programming chains.

### New Feature: BindIf / BindIfAsync

**The Problem:**
Previously, when you needed conditional logic in a pipeline (e.g., "if already valid, skip processing"), you had to break the chain or use awkward workarounds:
```csharp
// ❌ Before: Awkward nested Bind
var result = GetPayload()
    .Bind(payload => 
        payload.StartsWith("{") 
            ? Result<string, string>.Success(payload)  // Already JSON
            : ExtractJsonFromPrefix(payload)           // Extract it
    );
```

**The Solution:**
`BindIf` provides clean conditional branching:
```csharp
// ✅ After: Clean and readable
var result = GetPayload()
    .BindIf(
        payload => payload.StartsWith("{"),  // If already JSON
        payload => ExtractJsonFromPrefix(payload)  // Otherwise extract
    );
```

---

## 📦 New API Methods

### FunctionalResult

#### `BindIf<T, TError>`
```csharp
public static Result<T, TError> BindIf<T, TError>(
    this Result<T, TError> result,
    Func<T, bool> predicate,
    Func<T, Result<T, TError>> continuation)
```

Conditionally applies a continuation function based on a predicate:
- If predicate returns **true** → returns original result unchanged (short-circuit)
- If predicate returns **false** → applies continuation function
- If result is already failed → propagates error without evaluating predicate

### AsyncFunctionalResult

#### `BindIfAsync<T, TError>` - 7 Overloads

Complete async support covering all scenarios:

1. **Task\<Result\> + sync predicate + sync continuation**
2. **Result + sync predicate + async continuation**
3. **Task\<Result\> + sync predicate + async continuation**
4. **Result + async predicate + sync continuation**
5. **Task\<Result\> + async predicate + sync continuation**
6. **Result + async predicate + async continuation**
7. **Task\<Result\> + async predicate + async continuation**

This matches BindSharp's comprehensive async pattern seen in `MatchAsync`.

---

## 💡 Usage Examples

### Example 1: JSON Extraction with Short-Circuit
```csharp
// Extract JSON that might be prefixed with "request:id:"
public Result<string, string> ExtractJson(string payload)
{
    return Result<string, string>.Success(payload)
        .Map(p => p.TrimStart())
        .BindIf(
            // If already JSON, return as-is
            p => p.StartsWith("{") || p.StartsWith("["),
            // Otherwise, extract from prefixed format
            p => ExtractJsonAfterPrefix(p)
        )
        .Ensure(json => !string.IsNullOrEmpty(json), "JSON cannot be empty");
}
```

### Example 2: Conditional User Enrichment
```csharp
public async Task<Result<User, string>> GetUserAsync(int id)
{
    return await FetchUserAsync(id)
        .BindIfAsync(
            user => user.IsComplete,
            async user => await EnrichFromDatabaseAsync(user)
        )
        .TapAsync(async user => await CacheUserAsync(user));
}
```

### Example 3: Async Predicate (Database Check)
```csharp
public async Task<Result<Order, string>> ProcessOrderAsync(Order order)
{
    return await Result<Order, string>.Success(order)
        .BindIfAsync(
            // Async database check
            async o => await IsOrderValidInDatabaseAsync(o.Id),
            // If invalid, enrich from external API
            async o => await EnrichFromExternalApiAsync(o)
        )
        .BindAsync(async o => await SaveOrderAsync(o));
}
```

### Example 4: Complete Pipeline
```csharp
public async Task<Result<ProcessedData, string>> ProcessDataAsync(string input)
{
    return await ValidateInput(input)
        .MapAsync(async data => await NormalizeDataAsync(data))
        .BindIfAsync(
            // Skip expensive operation if already cached
            async data => await IsCachedAsync(data.Id),
            async data => await ExpensiveProcessingAsync(data)
        )
        .TapAsync(async data => await LogProcessingAsync(data))
        .MapAsync(data => new ProcessedData(data));
}
```

---

## 🎯 When to Use BindIf

### ✅ Use BindIf When:
- You need conditional logic without breaking the chain
- You want to skip processing if a condition is already met
- You need short-circuit evaluation in pipelines
- You're implementing validation with optional enrichment
- You want to avoid nested `Bind` calls

### ✅ Perfect For:
- **Format detection** - "If already correct format, skip conversion"
- **Caching** - "If cached, skip expensive fetch"
- **Validation** - "If valid, skip repair logic"
- **Enrichment** - "If complete, skip additional data fetch"
- **Optimization** - "If fast path available, skip slow path"

### ❌ Don't Use BindIf When:
- Simple validation is better handled by `Ensure`
- You need to change the result type (use `Bind` instead)
- You're doing pure transformation (use `Map` instead)
- The condition doesn't affect the continuation

---

## 🔄 Comparison with Other Methods

| Method | Use Case | Changes Result Type? | Executes on Condition? |
|--------|----------|---------------------|------------------------|
| `Map` | Transform value | Yes | Always (on success) |
| `Bind` | Chain operations | Yes | Always (on success) |
| `Ensure` | Validate condition | No | Always (on success) |
| `BindIf` | Conditional continuation | No | Only if predicate false |
| `Tap` | Side effects | No | Always (on success) |

**Key Difference:**
- `Ensure` fails if condition is false
- `BindIf` continues processing if condition is false

---

## 📊 Performance Characteristics

- **Zero allocation overhead** for sync versions
- **Async versions** follow standard Task patterns
- **Short-circuit optimization** - continuation not called if predicate is true
- **Predicate evaluated only once** per invocation

---

## 🔧 Migration from 1.3.x

**No migration needed!** All changes are backwards compatible.

### What Continues to Work:
```csharp
// All existing code unchanged
var result = GetData()
    .Map(x => x * 2)
    .Bind(Validate)
    .Ensure(x => x > 0, "Must be positive");
```

### What You Can Now Do:
```csharp
// New conditional branching
var result = GetData()
    .Map(x => x * 2)
    .BindIf(x => x > 100, x => ProcessLargeValue(x))
    .Ensure(x => x > 0, "Must be positive");
```

---

## 📖 Documentation Updates

- Added `BindIf` section to README.md
- Added 7 async overloads documentation
- Added real-world examples for conditional branching
- Updated API reference

---

## 🧪 Testing

- 48 comprehensive unit tests covering all overloads
- Tests for predicate evaluation short-circuiting
- Tests for continuation non-execution when predicate is true
- Tests for error propagation
- Real-world scenario tests (JSON extraction, user enrichment)

---

## 📋 Changelog

### Added
- `BindIf<T, TError>` - Conditional continuation in functional pipelines
- `BindIfAsync<T, TError>` (7 overloads) - Full async support for conditional branching
  - Task\<Result\> + sync predicate + sync continuation
  - Result + sync predicate + async continuation
  - Task\<Result\> + sync predicate + async continuation
  - Result + async predicate + sync continuation
  - Task\<Result\> + async predicate + sync continuation
  - Result + async predicate + async continuation
  - Task\<Result\> + async predicate + async continuation
- Comprehensive XML documentation for all new methods
- Package tags: `conditional-branching`, `bindif`

### Changed
- None (no breaking changes)

### Fixed
- None

### Removed
- None

---

## 🎓 Best Practices

### DO:
✅ Use `BindIf` for conditional short-circuiting  
✅ Use async predicates when condition requires I/O (database, cache)  
✅ Combine with other Result methods for complex pipelines  
✅ Keep predicates simple and side-effect free  

### DON'T:
❌ Use `BindIf` when `Ensure` is more appropriate (validation)  
❌ Put complex logic in predicates (extract to named methods)  
❌ Modify state in predicates (keep them pure)  
❌ Use when you need to change the result type (use `Bind`)  

---

## 🔗 Related Methods

- **`Bind`** - Chain operations that can fail (always executes)
- **`Ensure`** - Validate conditions (fails if false)
- **`Map`** - Transform success values (always executes)
- **`Tap`** - Execute side effects (always executes)

---

## 🚀 Next Steps

1. **Update your package:**
```bash
   dotnet add package BindSharp --version 1.4.0
```

2. **Explore conditional branching** in your pipelines

3. **Simplify existing code** that uses nested `Bind` for conditionals

4. **Use async predicates** for database-backed conditions

---

## 🙏 Acknowledgments

Thanks to the community for feedback and suggestions that led to this feature!

Special thanks to all contributors and users who helped shape this release.

---

## 📚 Additional Resources

- **GitHub:** https://github.com/GDB-11/BindSharp/
- **Documentation:** See README.md for complete usage guide
- **Previous Releases:**
  - [1.3.0](RELEASE_NOTES_1_3_0.md) - Equality & implicit conversions
  - [1.2.0](RELEASE_NOTES_1_2_0.md) - Unit type
  - [1.1.0](RELEASE_NOTES_1_1_0.md) - ResultExtensions utilities

---

## 🎉 Conclusion

Version 1.4.0 brings powerful conditional branching to BindSharp's functional pipelines:
- ✅ Clean short-circuit logic
- ✅ Full async support (7 overloads)
- ✅ Zero breaking changes
- 🎯 More expressive Railway-Oriented Programming

Upgrade today and enjoy cleaner, more expressive functional code!

Happy coding! 🚀
