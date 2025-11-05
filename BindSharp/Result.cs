using System;

namespace BindSharp;

/// <summary>
/// Represents the result of an operation that can either succeed with a value or fail with an error.
/// This type enables railway-oriented programming by explicitly handling success and failure cases.
/// </summary>
/// <typeparam name="T">The type of the success value.</typeparam>
/// <typeparam name="TError">The type of the error value.</typeparam>
/// <example>
/// <code>
/// // Success case
/// var success = Result&lt;int, string&gt;.Success(42);
/// 
/// // Failure case
/// var failure = Result&lt;int, string&gt;.Failure("Something went wrong");
/// </code>
/// </example>
public sealed class Result<T, TError>
{
    private T? _value;
    private TError? _error;
    
    /// <summary>
    /// Gets a value indicating whether the operation was successful.
    /// </summary>
    public bool IsSuccess { get; }
    
    /// <summary>
    /// Gets a value indicating whether the operation failed.
    /// </summary>
    public bool IsFailure => !IsSuccess;
    
    /// <summary>
    /// Gets the success value if the operation was successful.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown when attempting to access the value of a failed result.
    /// Use <see cref="IsSuccess"/> to check before accessing.
    /// </exception>
    public T Value
    {
        get => IsSuccess ? _value!: throw new InvalidOperationException("Result is not successful");
        private set => _value = value;
    }
    
    /// <summary>
    /// Gets the error value if the operation failed.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown when attempting to access the error of a successful result.
    /// Use <see cref="IsFailure"/> to check before accessing.
    /// </exception>
    public TError Error
    {
        get => !IsSuccess ? _error! : throw new InvalidOperationException("Result is successful");
        private set => _error = value;
    }
    
    private Result(bool isSuccess, T? value, TError? error) => 
        (IsSuccess, Value, Error) = (isSuccess, value, error);
    
    /// <summary>
    /// Creates a successful result with the specified value.
    /// </summary>
    /// <param name="value">The success value.</param>
    /// <returns>A successful <see cref="Result{T, TError}"/> containing the value.</returns>
    public static Result<T, TError> Success(T value) => new(true, value, default);
    
    /// <summary>
    /// Creates a failed result with the specified error.
    /// </summary>
    /// <param name="error">The error value.</param>
    /// <returns>A failed <see cref="Result{T, TError}"/> containing the error.</returns>
    public static Result<T, TError> Failure(TError error) => new(false, default, error);
}