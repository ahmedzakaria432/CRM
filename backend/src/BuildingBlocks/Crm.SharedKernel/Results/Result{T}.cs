namespace Crm.SharedKernel.Results;

public sealed class Result<TValue> : Result
{
    private readonly TValue? _value;

    internal Result(TValue? value, bool isSuccess, Error error) : base(isSuccess, error)
    {
        _value = value;
    }

    /// <summary>
    /// Throws on a failed result. Accessing the value of a failure is a programming error, not a
    /// scenario to model with a fallback default - a default!/null flowing through here would let
    /// a failure reach a response body unnoticed.
    /// </summary>
    public TValue Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException(
            $"Cannot access the value of a failed result. Error: {Error.Code} - {Error.Description}");

    public static implicit operator Result<TValue>(TValue value) => Success(value);
}
