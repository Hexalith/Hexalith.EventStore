using System.Net;
using System.Net.Sockets;
using System.Runtime.ExceptionServices;

namespace Hexalith.EventStore.Server.LiveSidecar.Tests.Fixtures;

/// <summary>Keeps this fixture's six loopback ports owned until their respective consumers start.</summary>
internal sealed class RouteCatalogPortReservations : IDisposable
{
    private readonly int[] _ports = new int[6];
    private readonly Action?[] _release = new Action?[6];

    internal RouteCatalogPortReservations() : this(ReserveLoopback) { }

    internal RouteCatalogPortReservations(Func<(int Port, Action Release)> reserve)
    {
        try
        {
            for (int index = 0; index < _ports.Length; index++)
                (_ports[index], _release[index]) = reserve();
        }
        catch
        {
            ReleaseAfterFailure();
            throw;
        }
    }

    internal int this[int index] => _ports[index];

    internal async Task StartApplicationAsync(Func<Task> start)
    {
        try
        {
            Release(5);
            await start().ConfigureAwait(false);
        }
        catch
        {
            ReleaseAfterFailure();
            throw;
        }
    }

    internal void StartSidecar(Action start)
    {
        try
        {
            for (int index = 0; index < 5; index++) Release(index);
            start();
        }
        catch
        {
            ReleaseAfterFailure();
            throw;
        }
    }

    public void Dispose()
    {
        Exception? first = null;
        for (int index = 0; index < _release.Length; index++)
        {
            try { Release(index); }
            catch (Exception exception) { first ??= exception; }
        }
        if (first is not null) ExceptionDispatchInfo.Capture(first).Throw();
    }

    private static (int Port, Action Release) ReserveLoopback()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0) { ExclusiveAddressUse = true };
        try
        {
            listener.Start();
            return (((IPEndPoint)listener.LocalEndpoint).Port, listener.Stop);
        }
        catch
        {
            try { listener.Stop(); } catch { /* Preserve the original bind failure. */ }
            throw;
        }
    }

    private void Release(int index) => Interlocked.Exchange(ref _release[index], null)?.Invoke();

    private void ReleaseAfterFailure()
    {
        try { Dispose(); }
        catch { /* The original acquisition/start failure remains the first outcome. */ }
    }
}
