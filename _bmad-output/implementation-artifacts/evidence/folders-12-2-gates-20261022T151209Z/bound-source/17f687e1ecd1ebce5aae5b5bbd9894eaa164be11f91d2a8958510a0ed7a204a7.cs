using Hexalith.EventStore.ProviderVerification;

if (args.Length > 0 && args[0] == "--internal-verify")
{
    return PactInteractionVerifier.RunIsolated(args);
}

return await ProviderVerificationApplication.RunAsync(args).ConfigureAwait(false);
