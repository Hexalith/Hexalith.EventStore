// Live Dapr actor tests share one sidecar-backed fixture.
// The fixture sets process-wide DAPR_HTTP_PORT/DAPR_GRPC_PORT values and exposes
// mutable fake services to the in-process host, so test collections must run
// serially to avoid routing commands against reset fixture state.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
