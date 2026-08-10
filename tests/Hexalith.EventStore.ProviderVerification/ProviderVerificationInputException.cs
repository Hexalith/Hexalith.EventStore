namespace Hexalith.EventStore.ProviderVerification;

internal sealed class ProviderVerificationInputException : Exception
{
    public ProviderVerificationInputException(string code)
        : base(code)
    {
        Code = code;
    }

    public ProviderVerificationInputException(string code, Exception innerException)
        : base(code, innerException)
    {
        Code = code;
    }

    public string Code { get; }
}
