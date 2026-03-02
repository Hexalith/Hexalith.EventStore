# Kubernetes Supplementary Manifests

Reference files supplementary to the Aspire publisher output (`aspire publish`).
These are **not** standalone deployment manifests — see
[docs/guides/deployment-kubernetes.md](../../../docs/guides/deployment-kubernetes.md)
for the full step-by-step deployment guide.

**Primary deployment path:** `PUBLISH_TARGET=k8s aspire publish`

| File | Purpose |
|------|---------|
| `namespace.yaml` | Target namespace definition |
| `dapr-annotations-example.yaml` | DAPR annotation and K8s probe reference snippet |
| `secrets-template.yaml` | Secret structure template + DAPR secret store component |
