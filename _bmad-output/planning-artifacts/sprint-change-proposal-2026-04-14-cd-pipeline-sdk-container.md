# Sprint Change Proposal — CD Pipeline Remise à Niveau (SDK Container Support)

- **Date** : 2026-04-14
- **Auteur** : Scrum Master (assisté)
- **Demandeur** : Jerome
- **Scope** : `Hexalith.EventStore` + submodule `Hexalith.Tenants`
- **Classification** : **Modérée** (réorganisation CD + éditions multi-projets, sans remise en cause du PRD)

---

## 1. Issue Summary

Le pipeline CD (`deploy-staging.yml`) est obsolète par rapport à l'état actuel du projet :

- Il ne construit **que 2 images** (`eventstore`, `sample`) alors que la stack Kubernetes en attend **6** (`eventstore`, `eventstore-admin`, `eventstore-admin-ui`, `sample`, `sample-blazor-ui`, `tenants`).
- Il ne redémarre que **2 deployments** (`deployment/eventstore`, `deployment/sample`) alors que 6 existent côté cluster.
- La maintenance des `Dockerfile` est manuelle et source de drift (cf. commit `f149fe8` patchant le restore layer suite à l'ajout d'`Admin.Abstractions`).
- Le submodule `Hexalith.Tenants` **ne construit aucune image Docker** — son `release.yml` ne fait que `semantic-release` (NuGet). Donc `tenants:staging-latest` n'est produit nulle part.

**Évidence** :
- Workflow : `.github/workflows/deploy-staging.yml` (lignes 30-50)
- K8s overlay : `deploy/k8s/overlays/staging/kustomization.yaml` (5 `images:` déclarées vs 2 construites)
- Deployments manquants : `eventstore-admin.yaml`, `eventstore-admin-ui.yaml`, `tenants.yaml`, `sample-blazor-ui.yaml`
- Submodule : `Hexalith.Tenants/.github/workflows/release.yml` (aucun `docker build`)

**Conséquence opérationnelle** : en staging, seuls 2 services sur 6 sont redéployés automatiquement après un push sur `main`. Les 4 autres tournent avec des images poussées manuellement à un moment indéterminé, ou sont en `ImagePullBackOff` silencieux.

---

## 2. Impact Analysis

### 2.1 Impact par artefact

| Artefact | Impact | Action |
|---|---|---|
| `.github/workflows/ci.yml` | Ajout d'une étape build container (sans push) | Édition |
| `.github/workflows/deploy-staging.yml` | Réécriture complète : SDK publish + tags immuables + rollout sur 6 deployments | Édition majeure |
| `src/Hexalith.EventStore/Dockerfile` | Remplacé par config MSBuild | Suppression |
| `samples/Hexalith.EventStore.Sample/Dockerfile` | Remplacé par config MSBuild | Suppression |
| `Directory.Build.props` | Ajout propriétés container mutualisées | Édition |
| `src/Hexalith.EventStore*.csproj` (5 services) | Opt-in container + `ContainerImageName` | Édition ciblée |
| `samples/Hexalith.EventStore.Sample.BlazorUI.csproj` | Opt-in container + `ContainerImageName` | Édition ciblée |
| `Hexalith.Tenants/src/Hexalith.Tenants.csproj` (submodule) | Opt-in container + `ContainerImageName` | Édition + commit submodule |
| `src/Hexalith.EventStore.CommandApi/` | Reliquat orphelin (bin/obj seulement) | Suppression |
| `CLAUDE.md` | Nouvelle section Container Images | Édition |
| `deploy/k8s-deployments.md` | Nouveau flow de déploiement | Édition |

### 2.2 Impact épique / stories

Aucun impact PRD ou stories fonctionnelles. Il s'agit d'une remise à niveau **technique** du pipeline livraison, sans modification du produit livré.

### 2.3 Impact technique

- **.NET SDK Container Support** (.NET 7+) remplace les `Dockerfile`. Commandes `dotnet publish -t:PublishContainer` produisent des images OCI directement.
- **Hardening préservé** : base `mcr.microsoft.com/dotnet/aspnet:10.0-alpine`, user non-root (UID 1654 par défaut du SDK), port 8080.
- **Tags immuables** : `staging-<git-sha>` + alias mutable `staging-latest`. Permet rollback K8s natif via `kubectl rollout undo`.
- **AppHost Aspire** : non impacté. Il orchestre via `builder.AddProject<Projects.X>()` (pas d'images Docker). Le dev local reste inchangé.
- **CI impact** : +~2 min (build containers). Compensé par suppression des 2 `docker build` en Deploy.

---

## 3. Recommended Approach

**Direct Adjustment — Moderate scope**

Modification d'infrastructure technique sans impact sur le produit. Approche retenue (cf. discussion du 2026-04-14) :

- **D1 — Tags immuables** : `:staging-<sha>` + alias `:staging-latest`
- **D2 — Centralisation** : propriétés communes dans `Directory.Build.props` (opt-in via `<EnableContainer>true</EnableContainer>`)
- **D3 — Base image** : `mcr.microsoft.com/dotnet/aspnet:10.0-alpine`
- **D4 — CI/CD split** : build container en CI (sans push, validation PR incluse), push + rollout en Deploy Staging
- **Tenants (option β)** : conteneurisation depuis ce repo (submodule checkouté), commit séparé sur le submodule

### Effort estimé

| Phase | Effort | Risque |
|---|---|---|
| Phase 1 — POC eventstore | 2h | Faible |
| Phase 2 — Extension 5 services | 3h | Faible |
| Phase 3 — Refonte CI/CD | 3h | Moyen (YAML + secrets registry) |
| Phase 4 — Validation staging | 2h | Moyen (premier déploiement complet) |
| Phase 5 — Cleanup + docs | 1h | Faible |
| **Total** | **~11h** | **Moyen** |

### Risques résiduels

1. **UID hardening** — SDK container utilise UID 1654, Dockerfiles actuels un UID alpine dynamique. Les manifests K8s n'imposent pas de `runAsUser` → a priori pas de conflit, à valider en smoke.
2. **Architecture du node K8s** — hypothèse x64 non confirmée. Sera détecté au premier déploiement.
3. **Submodule Tenants** — PR séparée à faire sur `Hexalith.Tenants`. Requiert bump submodule dans ce repo.
4. **Blazor SSR assets** — vérifier que `wwwroot` et bundles sont correctement embarqués par le SDK container sur `Admin.UI` et `Sample.BlazorUI`.

---

## 4. Detailed Change Proposals

### 4.1 `Directory.Build.props` — propriétés container mutualisées

**Ajout** à la racine `Directory.Build.props` :

```xml
<PropertyGroup Condition="'$(EnableContainer)' == 'true'">
  <IsPublishable>true</IsPublishable>
  <ContainerBaseImage>mcr.microsoft.com/dotnet/aspnet:10.0-alpine</ContainerBaseImage>
  <ContainerFamily>alpine</ContainerFamily>
  <ContainerRegistry Condition="'$(ContainerRegistry)' == ''">registry.hexalith.com</ContainerRegistry>
  <ContainerImageTag Condition="'$(ContainerImageTag)' == ''">staging-latest</ContainerImageTag>
  <ContainerUser>app</ContainerUser>
  <ContainerPort>8080</ContainerPort>
</PropertyGroup>

<ItemGroup Condition="'$(EnableContainer)' == 'true'">
  <ContainerLabel Include="org.opencontainers.image.source" Value="https://github.com/Hexalith/Hexalith.EventStore" />
  <ContainerLabel Include="org.opencontainers.image.licenses" Value="MIT" />
  <ContainerPort Include="8080" Type="tcp" />
</ItemGroup>
```

**Rationale** : factorise 95% de la config container. Chaque projet déployable n'a plus qu'à déclarer `<EnableContainer>true</EnableContainer>` et `<ContainerImageName>`.

### 4.2 `src/Hexalith.EventStore/Hexalith.EventStore.csproj` — opt-in container

```xml
<PropertyGroup>
  <EnableContainer>true</EnableContainer>
  <ContainerImageName>eventstore</ContainerImageName>
</PropertyGroup>
```

**Rationale** : déclaration minimale, le reste vient de `Directory.Build.props`. Pattern identique pour les 4 autres services et le submodule Tenants.

### 4.3 Suppression des Dockerfiles

- `src/Hexalith.EventStore/Dockerfile` → supprimé
- `samples/Hexalith.EventStore.Sample/Dockerfile` → supprimé

**Rationale** : remplacés par le SDK container support. Plus besoin de les maintenir (fin du bug "Admin.Abstractions manquant").

### 4.4 `.github/workflows/ci.yml` — build containers (sans push)

**Ajout** après l'étape "Integration Tests (Tier 2)" :

```yaml
- name: Build container images (validation only, no push)
  run: |
    for project in \
      src/Hexalith.EventStore \
      src/Hexalith.EventStore.Admin.Server.Host \
      src/Hexalith.EventStore.Admin.UI \
      samples/Hexalith.EventStore.Sample \
      samples/Hexalith.EventStore.Sample.BlazorUI \
      Hexalith.Tenants/src/Hexalith.Tenants; do
      dotnet publish "$project" \
        --configuration Release \
        -t:PublishContainer \
        -p:ContainerArchiveOutputPath=/tmp/images/$(basename $project).tar.gz
    done
```

**Rationale** : `ContainerArchiveOutputPath` évite d'avoir un démon Docker disponible, produit un tar compatible `docker load`. Sert uniquement à valider que les images se construisent — pas de push en CI.

### 4.5 `.github/workflows/deploy-staging.yml` — refonte complète

Remplace **intégralement** le job `deploy`. Voir brouillon complet en annexe (section 4.7). Points clés :

- Plus de `docker login` + `docker build` + `docker push`
- `dotnet publish -t:PublishContainer` avec auth registry via env vars `SDK_CONTAINER_REGISTRY_UNAME` / `SDK_CONTAINER_REGISTRY_PWORD`
- Tag double : `:staging-<github.sha>` (immuable) + `:staging-latest` (alias via `-p:ContainerImageTags="staging-latest;staging-${{ github.sha }}"`)
- `kubectl rollout restart` étendu aux 6 deployments
- Ou alternative : `kubectl apply -k deploy/k8s/overlays/staging` puis rollout status

### 4.6 Submodule Tenants — PR séparée

Dans `Hexalith.Tenants/src/Hexalith.Tenants/Hexalith.Tenants.csproj`, ajouter :

```xml
<PropertyGroup>
  <EnableContainer>true</EnableContainer>
  <ContainerImageName>tenants</ContainerImageName>
</PropertyGroup>
```

Plus centraliser les propriétés MSBuild dans `Hexalith.Tenants/Directory.Build.props` (pattern miroir de ce repo).

**Rationale** : même pattern partout, cohérence cross-repo. Nécessite une PR sur `github.com/Hexalith/Hexalith.Tenants`, puis bump submodule ici.

### 4.7 Brouillon complet du nouveau `deploy-staging.yml`

```yaml
name: Deploy Staging

on:
  workflow_run:
    workflows: ["CI"]
    types: [completed]
    branches: [main]

concurrency:
  group: deploy-staging
  cancel-in-progress: true

permissions:
  contents: read

jobs:
  deploy:
    runs-on: ubuntu-latest
    if: ${{ github.event.workflow_run.conclusion == 'success' }}
    timeout-minutes: 20
    env:
      SDK_CONTAINER_REGISTRY_UNAME: ${{ secrets.REGISTRY_USERNAME }}
      SDK_CONTAINER_REGISTRY_PWORD: ${{ secrets.REGISTRY_PASSWORD }}
      IMAGE_TAGS: "staging-latest;staging-${{ github.event.workflow_run.head_sha }}"

    steps:
      - uses: actions/checkout@34e114876b0b11c390a56381ad16ebd13914f8d5 # v4.3.1
        with:
          submodules: true
          ref: ${{ github.event.workflow_run.head_sha }}

      - uses: actions/setup-dotnet@67a3573c9a986a3f9c594539f4ab511d57bb3ce9 # v4.3.1

      - name: Cache NuGet packages
        uses: actions/cache@0057852bfaa89a56745cba8c7296529d2fc39830 # v4.3.0
        with:
          path: ~/.nuget/packages
          key: nuget-${{ hashFiles('Directory.Packages.props') }}
          restore-keys: nuget-

      - name: Publish container images
        run: |
          for project in \
            src/Hexalith.EventStore \
            src/Hexalith.EventStore.Admin.Server.Host \
            src/Hexalith.EventStore.Admin.UI \
            samples/Hexalith.EventStore.Sample \
            samples/Hexalith.EventStore.Sample.BlazorUI \
            Hexalith.Tenants/src/Hexalith.Tenants; do
            dotnet publish "$project" \
              --configuration Release \
              -t:PublishContainer \
              -p:ContainerImageTags="$IMAGE_TAGS"
          done

      - name: Deploy to staging
        uses: appleboy/ssh-action@0ff4204d59e8e51228ff73bce53f80d53301dee2 # v1.2.5
        with:
          host: ${{ secrets.STAGING_SSH_HOST }}
          username: ${{ secrets.STAGING_SSH_USER }}
          key: ${{ secrets.STAGING_SSH_KEY }}
          script: |
            for deployment in eventstore eventstore-admin eventstore-admin-ui sample sample-blazor-ui tenants; do
              kubectl rollout restart deployment/$deployment -n eventstore-staging
            done
            for deployment in eventstore eventstore-admin eventstore-admin-ui sample sample-blazor-ui tenants; do
              kubectl rollout status deployment/$deployment -n eventstore-staging --timeout=120s
            done
```

**Rationale** : simple, déclaratif, idempotent. Plus de duplication `docker build`/`docker push`. Les 6 services sont traités en boucle.

---

## 5. Implementation Handoff

### Scope classification : **Moderate**

Ni minor (pas juste un fix), ni major (pas de replan PRD). Les modifications touchent CI/CD + multi-projets MSBuild mais suivent un pattern éprouvé.

### Séquencement

1. **Phase 0** — Validation du présent document (bloquant)
2. **Phase 1** — POC eventstore (validation technique du pattern)
3. **Phase 2** — Extension aux 4 autres services locaux
4. **Phase 2bis** — PR submodule Tenants (parallélisable Phase 3)
5. **Phase 3** — Refonte CI/CD workflows
6. **Phase 4** — Validation end-to-end staging
7. **Phase 5** — Cleanup (CommandApi orphelin) + docs

### Success criteria

- [ ] `dotnet publish -t:PublishContainer` produit une image fonctionnelle pour chacun des 6 services
- [ ] Les images respectent : base alpine, user non-root, port 8080, labels OCI
- [ ] CI sur PR construit les 6 images (sans push) et échoue si une image ne se construit pas
- [ ] Push sur `main` déclenche Deploy Staging qui publie les 6 images avec tags `staging-<sha>` + `staging-latest`
- [ ] Les 6 deployments staging rolling-restart correctement (pods Ready ≤ 120s chacun)
- [ ] Aucun `Dockerfile` ne subsiste dans le repo
- [ ] `CLAUDE.md` documente la nouvelle commande de build container locale
- [ ] Le submodule `Hexalith.Tenants` est bumpé vers un commit incluant sa conteneurisation

### Rollback plan

Le commit de suppression des Dockerfiles est atomique par projet. En cas de régression :
- `git revert` du commit pipeline suffit pour revenir à l'ancien workflow
- Les Dockerfiles peuvent être restaurés depuis l'historique

### Handoff

Recipient : **Development team (Amelia/BMad-dev agent)** pour exécution sous supervision Scrum Master.

---

## Annexe — État actuel

### Mapping images K8s → projets .NET

| Image K8s | Projet .NET | Deployment K8s |
|---|---|---|
| `registry.hexalith.com/eventstore:latest` | `src/Hexalith.EventStore` | `deployment/eventstore` |
| `registry.hexalith.com/eventstore-admin:latest` | `src/Hexalith.EventStore.Admin.Server.Host` | `deployment/eventstore-admin` |
| `registry.hexalith.com/eventstore-admin-ui:latest` | `src/Hexalith.EventStore.Admin.UI` | `deployment/eventstore-admin-ui` |
| `registry.hexalith.com/sample:latest` | `samples/Hexalith.EventStore.Sample` | `deployment/sample` |
| `registry.hexalith.com/sample-blazor-ui:latest` | `samples/Hexalith.EventStore.Sample.BlazorUI` | `deployment/sample-blazor-ui` |
| `registry.hexalith.com/tenants:latest` | `Hexalith.Tenants/src/Hexalith.Tenants` (submodule) | `deployment/tenants` |

### Secrets requis (inchangés)

- `REGISTRY_USERNAME`, `REGISTRY_PASSWORD` — Zot registry `registry.hexalith.com`
- `STAGING_SSH_HOST`, `STAGING_SSH_USER`, `STAGING_SSH_KEY` — bastion vers node1

### Namespace K8s cible

`eventstore-staging` (cohérent avec `deploy/k8s/overlays/staging/kustomization.yaml`)
