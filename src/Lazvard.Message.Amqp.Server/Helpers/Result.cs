using System.Diagnostics.CodeAnalysis;

namespace Lazvard.Message.Amqp.Server.Helpers;

public readonly struct Result
{
    private static readonly Result success = new(true);
    private static readonly Result fail = new(false);

    public readonly string Error { get; }

    internal Result(bool isSuccess, string error = "")
    {
        IsSuccess = isSuccess;
        Error = error;
    }

    public readonly bool IsSuccess { get; }

    public static Result Fail()
    {
        return fail;
    }

    public static Result Fail(string error)
    {
        return new(false, error);
    }

    public static Result Success()
    {
        return success;
    }

    public static Result<T> Success<T>(T value)
    {
        return new Result<T>(true, value);
    }

    public Result<T> ToResult<T>(T? value, string error)
    {
        return new Result<T>(IsSuccess, value, error);
    }

    public static implicit operator Result(bool isSuccess)
    {
        return isSuccess ? success : fail;
    }
}

public readonly struct Result<TValue>
{
    [MemberNotNullWhen(returnValue: true, member: nameof(Value))]
    public readonly bool IsSuccess { get; }

    public readonly TValue? Value { get; }

    public readonly string Error { get; }

    internal Result(bool isSuccess, TValue? value, string error = "") : this()
    {
        IsSuccess = isSuccess;
        Value = value;
        Error = error;
    }

    public static Result<TValue> Fail()
    {
        return new Result<TValue>(false, default);
    }

    public static Result<TValue> Fail(string error)
    {
        return new Result<TValue>(false, default, error);
    }

    public static Result<TValue> Success(TValue value)
    {
        return new Result<TValue>(true, value);
    }

    public static implicit operator Result<TValue>(Result result)
    {
        return result.ToResult<TValue>(default, result.Error);
    }

    public static implicit operator Result<TValue>(TValue value)
    {
        return new Result<TValue>(true, value);
    }

    public static implicit operator Result(Result<TValue> value)
    {
        return value.IsSuccess ? Result.Success() : Result.Fail(value.Error);
    }

    public void Deconstruct(out bool isSuccess, out TValue value)
    {
        isSuccess = IsSuccess;
        value = Value ?? default!;
    }
}