using Hexalith.EventStore.Server.LiveSidecar.Tests.Fixtures;

using Microsoft.AspNetCore.Hosting;

using Xunit.Sdk;
using Xunit.v3;

// Live Dapr actor tests share one sidecar-backed fixture.
// The fixture sets process-wide DAPR_HTTP_PORT/DAPR_GRPC_PORT values and exposes
// mutable fake services to the in-process host, so test collections must run
// serially to avoid routing commands against reset fixture state.
[assembly: Parallelization(Mode = ParallelMode.None)]
[assembly: HostingStartup(typeof(Oq8HostingStartup))]
