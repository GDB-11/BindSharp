# MyLibrary

A lightweight, powerful functional programming library for .NET that makes error handling elegant and composable. Say goodbye to messy try-catch blocks and hello to Railway-Oriented Programming! 🚂

## Why MyLibrary?

Traditional error handling is messy:
```csharp
try {
    var data = await FetchDataAsync();
    var validated = ValidateData(data);
    var transformed = TransformData(validated);
    return await SaveAsync(transformed);
}
catch (Exception ex) {
    // What failed? Where? How do we recover?
    return null; // 😢
}
```

With MyLibrary, it's clean and composable:
```csharp
return await FetchDataAsync()
    .BindAsync(ValidateDataAsync)
    .MapAsync(TransformData)
    .BindAsync(SaveAsync)
    .MatchAsync(
        success => $"Saved: {success}",
        error => $"Failed: {error}"
    );
```

## Features

✅ **Result<T, TError>** - Explicit success/failure handling  
✅ **Railway-Oriented Programming** - Chain operations that can fail  
✅ **Full Async/Await Support** - Game-changing async composition  
✅ **Type-Safe** - Compiler catches your mistakes  
✅ **Lightweight** - Zero dependencies  
✅ **Compatible** - Works with .NET Framework 4.6.1+ and all modern .NET

## Installation
```bash
dotnet add package MyLibrary
```

## Quick Start

### Basic Success and Failure
```csharp
using Global.Objects.Results;
using Global.Helpers.Functional;

// Create a successful result
var success = Result<int, string>.Success(42);

// Create a failed result
var failure = Result<int, string>.Failure("Something went wrong");

// Check the result
if (success.IsSuccess)
    Console.WriteLine(success.Value); // 42

if (failure.IsFailure)
    Console.WriteLine(failure.Error); // "Something went wrong"
```

## Core Operations

### Map - Transform Success Values

Transform a value when the result is successful:
```csharp
Result<int, string> GetAge() => Result<int, string>.Success(25);

var result = GetAge()
    .Map(age => age * 2)  // 50
    .Map(age => $"Age in months: {age * 12}");  // "Age in months: 600"

// If GetAge() returned a failure, Map would skip and propagate the error
```

**Real-world example - API response transformation:**
```csharp
public Result<UserDto, string> GetUser(int id)
{
    var userResult = _database.FindUser(id); // Returns Result<User, string>
    
    return userResult.Map(user => new UserDto
    {
        Id = user.Id,
        FullName = $"{user.FirstName} {user.LastName}",
        Email = user.Email
    });
}
```

### Bind - Chain Operations That Can Fail

Chain multiple operations where each can fail:
```csharp
Result<string, string> ValidateEmail(string email) =>
    email.Contains("@")
        ? Result<string, string>.Success(email)
        : Result<string, string>.Failure("Invalid email");

Result<string, string> SendEmail(string email) =>
    /* send email logic */
    Result<string, string>.Success($"Sent to {email}");

var result = ValidateEmail("user@example.com")
    .Bind(SendEmail);  // Only runs if validation succeeds
```

**Real-world example - User registration flow:**
```csharp
public Result<User, string> RegisterUser(string email, string password)
{
    return ValidateEmail(email)
        .Bind(validEmail => ValidatePassword(password)
            .Map(_ => validEmail))  // Keep the email, discard password validation result
        .Bind(validEmail => CreateUser(validEmail, password))
        .Bind(user => SendWelcomeEmail(user));
}

// Each step only runs if the previous succeeded!
// First failure stops the chain and returns the error
```

### Match - Handle Both Cases

Extract a value by handling both success and failure:
```csharp
var result = DivideNumbers(10, 2);

var message = result.Match(
    success => $"Result: {success}",
    error => $"Error: {error}"
);

Console.WriteLine(message); // "Result: 5" or "Error: Division by zero"
```

**Real-world example - API response:**
```csharp
public IActionResult GetProduct(int id)
{
    var result = _productService.FindProduct(id);
    
    return result.Match(
        product => Ok(product),           // 200 OK with product
        error => NotFound(new { error })  // 404 Not Found with error message
    );
}
```

### MapError - Transform Error Values

Change the error type while preserving success:
```csharp
Result<int, string> result = Result<int, string>.Failure("404");

var transformed = result.MapError(errorCode => new 
{
    Code = int.Parse(errorCode),
    Message = "Resource not found"
});
// Result<int, { Code, Message }>
```

**Real-world example - Error localization:**
```csharp
public Result<Order, LocalizedError> GetOrder(int id)
{
    return _orderRepository.FindOrder(id)  // Returns Result<Order, string>
        .MapError(errorCode => new LocalizedError
        {
            Code = errorCode,
            Message = _localizer.GetString(errorCode),
            Timestamp = DateTime.UtcNow
        });
}
```

## 🚀 Async Support - The Game Changer!

This is where MyLibrary really shines! Handle async operations with the same elegant composition.

### MapAsync - Async Transformations

Three overloads for every scenario:
```csharp
// 1. Task<Result> + sync function
Task<Result<int, string>> asyncResult = GetUserIdAsync();
var user = await asyncResult.MapAsync(id => GetUserFromCache(id));

// 2. Result + async function
Result<int, string> userId = Result<int, string>.Success(42);
var user = await userId.MapAsync(async id => await FetchUserAsync(id));

// 3. Task<Result> + async function (most common!)
Task<Result<int, string>> asyncResult = GetUserIdAsync();
var user = await asyncResult.MapAsync(async id => await FetchUserAsync(id));
```

**Real-world example - API call chain:**
```csharp
public async Task<Result<OrderSummary, string>> GetOrderSummaryAsync(int orderId)
{
    return await FetchOrderAsync(orderId)
        .MapAsync(async order => await EnrichWithCustomerDataAsync(order))
        .MapAsync(async order => await CalculateTotalsAsync(order))
        .MapAsync(order => new OrderSummary(order));
    
    // Clean, readable, and each step only runs if previous succeeded!
}
```

### BindAsync - Chain Async Operations
```csharp
public async Task<Result<Receipt, string>> ProcessPaymentAsync(PaymentRequest request)
{
    return await ValidatePaymentRequest(request)  // Result<PaymentRequest, string>
        .BindAsync(async req => await ChargeCardAsync(req))  // Task<Result<Transaction, string>>
        .BindAsync(async tx => await SaveTransactionAsync(tx))  // Task<Result<Transaction, string>>
        .MapAsync(async tx => await GenerateReceiptAsync(tx));  // Task<Result<Receipt, string>>
}

// Beautiful async composition! No nested try-catch, no ugly error handling.
```

**Real-world example - Multi-step async workflow:**
```csharp
public async Task<Result<ShipmentConfirmation, string>> FulfillOrderAsync(int orderId)
{
    return await GetOrderAsync(orderId)
        .BindAsync(async order => await ValidateInventoryAsync(order))
        .BindAsync(async order => await ReserveItemsAsync(order))
        .BindAsync(async order => await CreateShipmentAsync(order))
        .BindAsync(async shipment => await NotifyCustomerAsync(shipment))
        .MapAsync(shipment => new ShipmentConfirmation(shipment));
    
    // Each async operation runs in sequence
    // First failure stops the chain immediately
    // Error is automatically propagated
}
```

### MatchAsync - Async Result Handling

Handle async results with async handlers:
```csharp
var result = await FetchDataAsync();

var output = await result.MatchAsync(
    async data => await ProcessSuccessAsync(data),
    async error => await LogErrorAsync(error)
);
```

**Real-world example - Complete async flow:**
```csharp
public async Task<IActionResult> CreateUserAsync(CreateUserRequest request)
{
    var result = await ValidateUserRequest(request)
        .BindAsync(async req => await CheckEmailAvailabilityAsync(req))
        .BindAsync(async req => await CreateUserAccountAsync(req))
        .BindAsync(async user => await SendVerificationEmailAsync(user));
    
    return await result.MatchAsync(
        async user => {
            await _auditLog.LogUserCreatedAsync(user);
            return Created($"/users/{user.Id}", user);
        },
        async error => {
            await _auditLog.LogErrorAsync(error);
            return BadRequest(new { error });
        }
    );
}
```

## Advanced Patterns

### Error Recovery
```csharp
public async Task<Result<Data, string>> GetDataWithFallbackAsync(int id)
{
    var primaryResult = await FetchFromPrimarySourceAsync(id);
    
    if (primaryResult.IsSuccess)
        return primaryResult;
    
    // Try fallback
    return await FetchFromCacheAsync(id)
        .MapErrorAsync(async cacheError => 
            await FetchFromBackupSourceAsync(id)
                .Match(
                    success => success,
                    backupError => $"All sources failed: {primaryResult.Error}, {cacheError}, {backupError}"
                )
        );
}
```

### Parallel Async Operations
```csharp
public async Task<Result<CombinedData, string>> FetchAllDataAsync(int userId)
{
    // Start all operations in parallel
    var userTask = GetUserAsync(userId);
    var ordersTask = GetOrdersAsync(userId);
    var preferencesTask = GetPreferencesAsync(userId);
    
    await Task.WhenAll(userTask, ordersTask, preferencesTask);
    
    // Combine results
    var user = await userTask;
    var orders = await ordersTask;
    var preferences = await preferencesTask;
    
    // If any failed, return first failure
    if (user.IsFailure) return Result<CombinedData, string>.Failure(user.Error);
    if (orders.IsFailure) return Result<CombinedData, string>.Failure(orders.Error);
    if (preferences.IsFailure) return Result<CombinedData, string>.Failure(preferences.Error);
    
    return Result<CombinedData, string>.Success(new CombinedData
    {
        User = user.Value,
        Orders = orders.Value,
        Preferences = preferences.Value
    });
}
```

### Validation Chains
```csharp
public Result<User, string> ValidateAndCreateUser(UserInput input)
{
    return ValidateNotEmpty(input.Email, "Email")
        .Bind(_ => ValidateEmail(input.Email))
        .Bind(email => ValidateNotEmpty(input.Password, "Password"))
        .Bind(_ => ValidatePasswordStrength(input.Password))
        .Bind(_ => ValidateAge(input.Age))
        .Map(_ => new User(input.Email, input.Password, input.Age));
}

// Each validation only runs if previous succeeded
// First failure stops and returns the error
```

### Exception Handling

Wrap exception-prone code in Results:
```csharp
public static Result<T, string> Try<T>(Func<T> operation)
{
    try
    {
        return Result<T, string>.Success(operation());
    }
    catch (Exception ex)
    {
        return Result<T, string>.Failure(ex.Message);
    }
}

// Usage
var result = Try(() => JsonSerializer.Deserialize<User>(json))
    .Bind(user => ValidateUser(user))
    .Map(user => ProcessUser(user));
```

## Complete Real-World Example
```csharp
public class OrderService
{
    public async Task<Result<OrderConfirmation, string>> PlaceOrderAsync(Cart cart)
    {
        return await ValidateCart(cart)
            .BindAsync(async validCart => await CheckInventoryAsync(validCart))
            .BindAsync(async validCart => await CalculatePriceAsync(validCart))
            .BindAsync(async order => await ProcessPaymentAsync(order))
            .BindAsync(async order => await CreateOrderRecordAsync(order))
            .BindAsync(async order => await ReserveInventoryAsync(order))
            .BindAsync(async order => await SendConfirmationEmailAsync(order))
            .MapAsync(order => new OrderConfirmation(order))
            .MatchAsync(
                async confirmation => {
                    await _analytics.TrackOrderAsync(confirmation);
                    return confirmation;
                },
                async error => {
                    await _logger.LogErrorAsync(error);
                    return new OrderConfirmation { Error = error };
                }
            );
    }
    
    // Each method returns Result<T, string> or Task<Result<T, string>>
    // Clean separation of concerns
    // Easy to test
    // Easy to understand
    // Errors flow naturally
}
```

## Tips & Best Practices

1. **Use descriptive error types** - `Result<User, UserError>` is better than `Result<User, string>`
2. **Keep operations small** - Each function should do one thing
3. **Use BindAsync for chaining** - When next operation depends on previous result
4. **Use MapAsync for transforming** - When just changing the value
5. **Match at boundaries** - Convert Result to concrete types at API/UI boundaries
6. **Async all the way** - If any operation is async, make the whole chain async

## Why Async Support is a Game-Changer

Traditional async error handling gets messy fast:
```csharp
// 😢 The old way
try {
    var user = await GetUserAsync(id);
    try {
        var orders = await GetOrdersAsync(user.Id);
        try {
            var enriched = await EnrichOrdersAsync(orders);
            return await FormatResponseAsync(enriched);
        } catch (Exception ex3) { /* handle */ }
    } catch (Exception ex2) { /* handle */ }
} catch (Exception ex1) { /* handle */ }
```

With MyLibrary:
```csharp
// 😊 The new way
return await GetUserAsync(id)
    .BindAsync(user => GetOrdersAsync(user.Id))
    .BindAsync(orders => EnrichOrdersAsync(orders))
    .MapAsync(enriched => FormatResponseAsync(enriched))
    .MatchAsync(
        success => Ok(success),
        error => BadRequest(error)
    );
```

**Clean. Composable. Elegant. Powerful.** 🚀

## Acknowledgments

Special thanks to **[Zoran Horvat](https://www.youtube.com/@zoran-horvat)** from the YouTube channel **"Zoran on C#"** for his excellent tutorials on functional programming in C#. His teaching on Railway-Oriented Programming and Result patterns made this library possible. 

If you want to learn advanced C# techniques and functional programming concepts, check out his channel - he deserves way more views! We can all learn a thing or two from him. 🙏

## License

LGPL-3.0-or-later

## Contributing

Contributions are welcome! Feel free to open issues or submit pull requests.

---

Built with ❤️ for the .NET community
