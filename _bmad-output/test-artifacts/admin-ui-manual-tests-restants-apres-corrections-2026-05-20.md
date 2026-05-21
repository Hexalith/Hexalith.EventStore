# Admin UI - Tests manuels restants apres corrections DW10

Date: 2026-05-20

Ce fichier remplace le run manuel courant pour la suite. Il ne te demande pas
de refaire les checks deja valides `1/3/4/5/6/7/8/10/14`. Il couvre seulement:

- les 3 corrections faites maintenant: Issue 2, bug State Inspector, Issue 13;
- les tests restants qui demandaient des entrees specifiques: Issues 9, 11, 12,
  15, 16, 17, 18.

## 0. Redemarrer avec le code corrige

Les corrections touchent Admin.Server et Admin.UI. Il faut donc redemarrer
Aspire avant de tester dans le navigateur.

Depuis la racine du depot:

```powershell
$dotnetDir = Join-Path $env:LOCALAPPDATA 'Microsoft\dotnet'
$env:PATH = "$dotnetDir;$dotnetDir\tools;$env:PATH"
$env:EnableKeycloak = 'false'
aspire stop --non-interactive --apphost .\src\Hexalith.EventStore.AppHost\Hexalith.EventStore.AppHost.csproj
aspire run --project .\src\Hexalith.EventStore.AppHost\Hexalith.EventStore.AppHost.csproj
```

Verifier les ports DAPR utiles:

```powershell
dapr list
```

Dans le run actuel apres redemarrage, le sidecar `eventstore-admin` etait sur le
port HTTP `30166`. Si `dapr list` donne un autre port, remplace `30166` dans les
commandes de fixture ci-dessous.

## 1. Seed Counter obligatoire

Dans la Sample Blazor UI, recree ou confirme ce seed:

| Etape | Action | Resultat attendu |
| --- | --- | --- |
| 1 | Increment x5 | Counter = 5 |
| 2 | Decrement x2 | Counter = 3 |
| 3 | Reset x1 | Counter = 0 |
| 4 | Increment x10 | Counter = 10 |
| 5 | GetCounterStatus | `Value = 10` |

Identifiants a utiliser partout:

| Champ | Valeur |
| --- | --- |
| Tenant | `tenant-a` |
| Domain | `counter` |
| Aggregate id | `counter-1` |
| Stream | `tenant-a/counter/counter-1` |
| Evenements attendus | environ `18` |

## 2. Tests des corrections faites maintenant

### Issue 2 - Events/sec et Error Rate

Page:

```text
/
/health
```

Etapes:

1. Ouvrir `/`.
2. Verifier la carte `Events/sec`.
3. Verifier la carte `Error Rate`.
4. Ouvrir `/health`.
5. Verifier les memes metriques.

Resultat attendu:

- Si la source est lisible et qu'il n'y a pas d'activite instantanee:
  `Events/sec` affiche une vraie valeur zero, par exemple `0.0/s` ou `0,0/s`.
- Si la source est lisible et qu'il n'y a pas d'erreur:
  `Error Rate` affiche `0.00%`, `0,00%`, `0.0%`, ou `0,0%` selon la page.
- `unavailable` ne doit apparaitre que si la source de metrique est vraiment
  indisponible.

Evidence a noter:

```text
Issue 2:
- / Events/sec =
- / Error Rate =
- /health Events/sec =
- /health Error Rate =
- OK / KO =
```

### Nouveau bug - State Inspector ne doit pas se rouvrir

Page:

```text
/streams/tenant-a/counter/counter-1
```

Etapes:

1. Ouvrir `/streams/tenant-a/counter/counter-1`.
2. Cliquer une ligne `Event` de la timeline.
3. Dans le detail, cliquer `Inspect State`.
4. Verifier que le dialog `State Inspector` s'ouvre.
5. Fermer le dialog avec `X`.
6. Cliquer `All`.
7. Verifier que `State Inspector` ne se rouvre pas.
8. Refaire les etapes 3 a 7 avec `Commands`.
9. Refaire les etapes 3 a 7 avec `Events`.
10. Refaire les etapes 3 a 7 avec `Queries`.

Resultat attendu:

- Le filtre timeline change bien.
- Le State Inspector reste ferme apres chaque clic sur `All`, `Commands`,
  `Events`, ou `Queries`.
- L'URL ne garde pas un ancien `inspect=...` qui force la reouverture.

Evidence a noter:

```text
State Inspector:
- All: OK / KO
- Commands: OK / KO
- Events: OK / KO
- Queries: OK / KO
```

### Issue 13 - Texte inutile du role switcher

Pre-requis:

- `EnableKeycloak=false`
- role switcher visible dans le header

Etapes:

1. Ouvrir l'Admin UI.
2. Dans le header, passer le role a `Operator`.
3. Regarder a droite du dropdown.
4. Passer le role a `ReadOnly`.
5. Passer le role a `Admin`.

Resultat attendu:

- Le dropdown reste visible.
- Aucun texte visible `Role: Operator`, `Role: ReadOnly`, ou `Role: Admin`
  n'apparait a cote du dropdown.
- Aucun texte visible `Development role Operator selected.` n'apparait.
- Les guards UI continuent de changer selon le role.

Evidence a noter:

```text
Issue 13:
- Dropdown visible: OK / KO
- Texte Role: absent: OK / KO
- Texte Development role ... selected absent: OK / KO
- Guards role toujours actifs: OK / KO
```

## 3. Fixtures pour les tests qui demandent des entrees

Utilise ces fixtures seulement si les pages sont vides apres le seed Counter.
Elles creent des entrees Admin lisibles par l'UI via le state store DAPR.

Important:

- Ces fixtures servent a tester l'affichage et les chemins d'erreur
  recuperables.
- Pour tester un vrai succes backend, prefere une entree creee par une action
  produit reelle.
- Pour nettoyer, utilise la section `Nettoyage des fixtures`.

### 3.1 Installer les fixtures

Adapte `$adminDaprPort` avec la valeur `eventstore-admin` de `dapr list`.

```powershell
$adminDaprPort = 30166
$stateStore = "statestore"
$now = (Get-Date).ToUniversalTime().ToString("o")

function Save-DaprState {
    param(
        [string] $Key,
        [object] $Value
    )

    $body = @(
        @{
            key = $Key
            value = $Value
        }
    ) | ConvertTo-Json -Depth 20

    Invoke-RestMethod `
        -Method Post `
        -Uri "http://localhost:$adminDaprPort/v1.0/state/$stateStore" `
        -ContentType "application/json" `
        -Body $body
}

$deadLetters = @(
    @{
        messageId = "manual-dlq-tenant-a-001"
        tenantId = "tenant-a"
        domain = "counter"
        aggregateId = "counter-1"
        correlationId = "manual-corr-dlq-001"
        failureReason = "Manual fixture: backend should return a recoverable failure for retry/skip/archive if the real message does not exist."
        failedAtUtc = $now
        retryCount = 0
        originalCommandType = "IncrementCounter"
    }
)
Save-DaprState "admin:dead-letters:all" $deadLetters
Save-DaprState "admin:dead-letters:tenant-a" $deadLetters

$projections = @(
    @{
        name = "counter"
        tenantId = "tenant-a"
        status = 0
        lag = 0
        throughput = 0
        errorCount = 0
        lastProcessedPosition = 18
        lastProcessedUtc = $now
    }
)
Save-DaprState "admin:projections:all" $projections
Save-DaprState "admin:projections:tenant-a" $projections

$eventTypes = @(
    @{ typeName = "CounterIncremented"; domain = "counter"; isRejection = $false; schemaVersion = 1 },
    @{ typeName = "CounterDecremented"; domain = "counter"; isRejection = $false; schemaVersion = 1 },
    @{ typeName = "CounterReset"; domain = "counter"; isRejection = $false; schemaVersion = 1 }
)
$commandTypes = @(
    @{ typeName = "IncrementCounter"; domain = "counter"; targetAggregateType = "CounterAggregate" },
    @{ typeName = "DecrementCounter"; domain = "counter"; targetAggregateType = "CounterAggregate" },
    @{ typeName = "ResetCounter"; domain = "counter"; targetAggregateType = "CounterAggregate" }
)
$aggregateTypes = @(
    @{ typeName = "CounterAggregate"; domain = "counter"; eventCount = 3; commandCount = 3; hasProjections = $true }
)
Save-DaprState "admin:type-catalog:events:all" $eventTypes
Save-DaprState "admin:type-catalog:events:counter" $eventTypes
Save-DaprState "admin:type-catalog:commands:all" $commandTypes
Save-DaprState "admin:type-catalog:commands:counter" $commandTypes
Save-DaprState "admin:type-catalog:aggregates:all" $aggregateTypes
Save-DaprState "admin:type-catalog:aggregates:counter" $aggregateTypes

$snapshotPolicies = @(
    @{
        tenantId = "tenant-a"
        domain = "counter"
        aggregateType = "CounterAggregate"
        intervalEvents = 10
        createdAtUtc = $now
    }
)
Save-DaprState "admin:storage-snapshot-policies:all" $snapshotPolicies
Save-DaprState "admin:storage-snapshot-policies:tenant-a" $snapshotPolicies

$compactionJobs = @(
    @{
        operationId = "manual-compaction-tenant-a-001"
        tenantId = "tenant-a"
        domain = "counter"
        status = 2
        startedAtUtc = $now
        completedAtUtc = $now
        eventsCompacted = 18
        spaceReclaimedBytes = $null
        errorMessage = $null
    }
)
Save-DaprState "admin:storage-compaction-jobs:all" $compactionJobs
Save-DaprState "admin:storage-compaction-jobs:tenant-a" $compactionJobs

$backupJobs = @(
    @{
        backupId = "manual-backup-tenant-a-001"
        tenantId = "tenant-a"
        streamId = "tenant-a/counter/counter-1"
        description = "Manual fixture backup job for Admin UI validation"
        jobType = 0
        status = 2
        includeSnapshots = $true
        createdAtUtc = $now
        completedAtUtc = $now
        eventCount = 18
        sizeBytes = $null
        isValidated = $false
        errorMessage = $null
    }
)
Save-DaprState "admin:backup-jobs:all" $backupJobs
Save-DaprState "admin:backup-jobs:tenant-a" $backupJobs
```

### 3.2 Verifier les fixtures

```powershell
docker exec dapr_redis redis-cli KEYS "admin:dead-letters:*"
docker exec dapr_redis redis-cli KEYS "admin:projections:*"
docker exec dapr_redis redis-cli KEYS "admin:type-catalog:*"
docker exec dapr_redis redis-cli KEYS "admin:storage-snapshot-policies:*"
docker exec dapr_redis redis-cli KEYS "admin:storage-compaction-jobs:*"
docker exec dapr_redis redis-cli KEYS "admin:backup-jobs:*"
```

## 4. Tests restants avec entrees

### Issue 9 - Dead-letter Retry / Skip / Archive

Page:

```text
/health/dead-letters
```

Entree necessaire:

- fixture `manual-dlq-tenant-a-001`, ou une vraie entree DLQ backend.

Etapes:

1. Passer en role `Operator`.
2. Ouvrir `/health/dead-letters`.
3. Filtrer ou verifier `tenant-a`.
4. Selectionner `manual-dlq-tenant-a-001`.
5. Cliquer `Retry`, confirmer.
6. Verifier que le spinner s'arrete.
7. Verifier que l'erreur reste dans un etat recuperable si le backend repond
   que le message n'existe pas.
8. Refaire avec `Skip`.
9. Refaire avec `Archive`.

Resultat attendu:

- Aucun bouton ne reste en chargement infini.
- Le dialog reste recuperable apres erreur.
- L'erreur affiche action, tenant, message id, statut ou operation id utile.
- Aucun secret, stack trace brute ou connection string n'est affiche.

### Issue 11 - Projections

Page:

```text
/projections
```

Entree necessaire:

- projection `counter` pour `tenant-a`.

Etapes:

1. Ouvrir `/projections`.
2. Selectionner le tenant `tenant-a`.
3. Verifier la ligne `counter`.
4. Ouvrir le detail de la projection.

Resultat attendu:

- La projection est visible.
- Le statut est lisible.
- Le lag vaut `0` ou une valeur numerique coherente.
- La page n'utilise pas une cle technique `ProjectionActor` comme nom de
  projection utilisateur.

### Issue 12 - Type Catalog

Page:

```text
/types
```

Entrees necessaires:

- Events: `CounterIncremented`, `CounterDecremented`, `CounterReset`
- Commands: `IncrementCounter`, `DecrementCounter`, `ResetCounter`
- Aggregate: `CounterAggregate`

Etapes:

1. Ouvrir `/types`.
2. Onglet `Events`, chercher `Counter`, verifier les 3 events.
3. Onglet `Commands`, chercher `Counter`, verifier les 3 commands.
4. Onglet `Aggregates`, chercher `Counter`, verifier `CounterAggregate`.
5. Tester les liens profonds si presents:
   - `/types?tab=events`
   - `/types?tab=commands`
   - `/types?tab=aggregates`

Resultat attendu:

- Les types Counter sont visibles et filtrables.
- Les onglets restent stables apres refresh.

### Issue 15 - Snapshots, compaction et backups

Pages:

```text
/snapshots
/compaction
/backups
```

Entrees necessaires:

- Snapshot policy `tenant-a / counter / CounterAggregate`.
- Compaction job `manual-compaction-tenant-a-001`.
- Backup job `manual-backup-tenant-a-001`.

Etapes snapshots:

1. Passer en role `Operator`.
2. Ouvrir `/snapshots`.
3. Verifier la policy `tenant-a / counter / CounterAggregate`.
4. Tester `Create Snapshot` avec:
   - tenant: `tenant-a`
   - domain: `counter`
   - aggregate id: `counter-1`
5. Verifier que la reponse est soit succes structure, soit echec explicite et
   recuperable.

Etapes compaction:

1. Ouvrir `/compaction`.
2. Verifier le job `manual-compaction-tenant-a-001`.
3. Lancer une compaction sur:
   - tenant: `tenant-a`
   - domain: `counter`
4. Verifier que l'operation ne simule pas un faux succes si le backend ne la
   supporte pas.

Etapes backups:

1. Passer en role `Admin`.
2. Ouvrir `/backups`.
3. Verifier le job `manual-backup-tenant-a-001`.
4. Tester `Create Backup` avec tenant `tenant-a`.
5. Si un job `Completed` est disponible, tester `Validate`.
6. Tester `Export Stream` avec `tenant-a/counter/counter-1`.
7. Ne tester `Restore` ou `Import` que si la page affiche clairement le risque
   et demande confirmation.

Resultat attendu:

- Aucune action ne reste en chargement infini.
- Une operation non supportee est affichee explicitement comme differee,
  bloquee, unsupported, ou erreur recuperable.
- Aucun faux succes pour une operation backend absente.
- Truth-before-submit (DW14): chaque action concernee (Create Snapshot,
  Trigger Compaction, Create Backup, Validate, Export Stream) affiche un
  badge `Deferred by backend` visible avant l'ouverture de la dialog, la
  dialog repete le message de deferred dans son corps, et le bouton primaire
  final s'appelle `Submit Deferred Request` (pas `Start`, `Create`,
  `Validate`, ni `Export`). Le toast de retour est en intent `warning`,
  jamais `success`, meme si le backend renvoie `Success=true` avec un
  message contenant `deferred`.

Outcome a noter pour Issue 15 (post-DW14):

- `OK - deferred explicite`: l'UI affiche le badge `Deferred by backend`
  avant la soumission, la dialog porte le message exact, le bouton final est
  `Submit Deferred Request`, le toast est `warning`, et aucun
  faux succes n'apparait. Conforme a la story
  `post-epic-deferred-dw14-admin-deferred-operations-ux-policy`.
- `Action-needed`: l'AC exige un backend reel; lever une story d'engine
  backend dedie (snapshot job model, compaction non-destructif, backup
  manifest/engine, validation backup, export borne).
- `KO`: l'UI affiche un toast `success` pour une operation differee, ou
  manque l'un des elements du pattern Truth-before-submit. Bloquant
  jusqu'a correction.

### Issue 16 - Consistency subtitles

Page:

```text
/consistency
```

Entree/action necessaire:

- un `Run Check` sur `tenant-a / counter`.

Etapes:

1. Passer en role `Operator`.
2. Ouvrir `/consistency`.
3. Renseigner:
   - tenant: `tenant-a`
   - domain: `counter`
4. Cliquer `Run Check`.
5. Attendre la fin.
6. Noter l'operation id ou check id.
7. Verifier les valeurs principales.
8. Verifier les subtitles visibles.
9. Cliquer `Refresh`.

Resultat attendu:

- Les subtitles ne restent pas a `Total Checks: 0`, `Last Check: Never`, ou
  `Total Anomalies: 0` si les valeurs principales ont change.

### Issue 17 - Consistency faux positifs

Pre-requis:

- Issue 11 validee ou projection fixture installee.

Etapes:

1. Passer en role `Operator`.
2. Ouvrir `/consistency`.
3. Lancer `Run Check` sur:
   - tenant: `tenant-a`
   - domain: `counter`
4. Noter le nombre d'anomalies.
5. Ouvrir le detail des anomalies s'il y en a.

Resultat attendu:

- Pas de cluster de faux positifs cause par un index projection vide.
- Toute anomalie restante doit etre explicable.

### Issue 18 - Tenants lifecycle / pas de Delete

Page:

```text
/tenants
```

Entree necessaire:

| Champ | Valeur |
| --- | --- |
| Tenant ID | `manual-test-tenant-a` |
| Name | `Manual Test Tenant A` |
| Description | `Tenant created for Admin UI manual lifecycle test` |

Etapes:

1. Passer en role `Admin`.
2. Ouvrir `/tenants`.
3. Creer le tenant `manual-test-tenant-a` si le formulaire existe.
4. Verifier qu'il apparait actif.
5. Verifier qu'aucun bouton `Delete` n'apparait comme action fantome.
6. Cliquer `Disable`.
7. Confirmer.
8. Verifier le statut disabled/suspended.
9. Cliquer `Enable`.
10. Confirmer.
11. Verifier le statut actif.
12. Filtrer sur `manual-test-tenant-a`, puis vider le filtre.

Resultat attendu:

- La page explique le cycle de vie tenant.
- Disable/Enable remplacent la suppression physique.
- Pas de bouton Delete trompeur.

## 5. Correct Course handoff - anomalies observees le 2026-05-20

Cette section est autonome: elle peut etre copiee dans une nouvelle conversation
`bmad-correct-course` sans reprendre tout le contexte de cette conversation.

Contexte du run:

- Aspire demarre depuis `src/Hexalith.EventStore.AppHost/Hexalith.EventStore.AppHost.csproj`.
- Admin UI: `http://localhost:8092`.
- Admin Server: `https://localhost:8091`.
- EventStore: `http://localhost:8080`.
- DAPR sidecars observes par `dapr list`: `eventstore-admin` HTTP `30166`, `eventstore` HTTP `30155`.
- Fixtures installees dans Redis pour `tenant-a / counter / counter-1`.
- Le seed Counter a ete effectue manuellement dans la Sample UI.
- Dumps bruts Aspire conserves dans:
  `_bmad-output/test-artifacts/admin-ui-manual-follow-up-trace-dumps-2026-05-20/`.

### CC-1 / Issue 9 - Dead-letter actions fail en 500

Symptome utilisateur:

- `Retry failed`, status `500 InternalServerError`.
- `Skip failed`, status `500 InternalServerError`.
- `Archive failed`, status `500 InternalServerError`.
- Tenant: `tenant-a`.
- Message id: `manual-dlq-tenant-a-001`.

Trace IDs fournis:

- Retry: `fa3fc5fd10b041f1162f2944d18953d9`.
- Skip: `b0a9110c494774fec42be2369b39da9f`.
- Archive: `54257a9188c24dd9a4ca6791cdf02b82`.

Evidence Aspire:

- Les trois actions atteignent bien `eventstore-admin`.
- Les endpoints sont:
  - `POST /api/v1/admin/dead-letters/tenant-a/retry`
  - `POST /api/v1/admin/dead-letters/tenant-a/skip`
  - `POST /api/v1/admin/dead-letters/tenant-a/archive`
- L'exception est levee avant la logique metier:
  `InvalidOperationException: Record type 'Hexalith.EventStore.Admin.Server.Models.DeadLetterActionRequest' has validation metadata defined on property 'MessageIds' that will be ignored. 'MessageIds' is a parameter in the record primary constructor and validation metadata must be associated with the constructor parameter.`

Code suspect:

- `src/Hexalith.EventStore.Admin.Server/Models/DeadLetterActionRequest.cs`
- Ligne observee: `[property: Required] IReadOnlyList<string> MessageIds`

Analyse:

- Ce n'est pas un probleme de fixture DLQ.
- MVC refuse la validation metadata sur la propriete generee du record primaire.
- Correct Course doit creer une story de correction backend + tests.

Proposition de changement:

- Remplacer l'attribut record par une validation attachee au parametre primaire,
  ou transformer le request model en classe explicite.
- Ajouter des tests controller/API couvrant Retry, Skip et Archive avec un body
  valide `{ "messageIds": ["manual-dlq-tenant-a-001"] }`.
- Apres correction, refaire le test avec une vraie DLQ si possible. Si seule la
  fixture visuelle existe, l'action peut ensuite renvoyer un 404/422 metier, mais
  plus un 500 de model binding.

Retest DW11 du 2026-05-20 apres restart Aspire, `FLUSHALL` Redis, et reapplication
des fixtures:

- Retry: OK. `Retry failed` avec status `404 NotFound`, message
  `Response status code does not indicate success: 404 (Not Found).`
- Skip: OK. `Skip failed` avec status `404 NotFound`, message
  `Response status code does not indicate success: 404 (Not Found).`
- Archive: OK. `Archive failed` avec status `404 NotFound`, message
  `Response status code does not indicate success: 404 (Not Found).`
- Tenant: `tenant-a`.
- Message id: `manual-dlq-tenant-a-001`.
- Conclusion: OK pour DW11. Les actions ne produisent plus le 500 de model
  binding; la fixture visuelle retourne un 404 metier recuperable, conforme au
  comportement accepte.

### CC-2 / Issue 11 - Projection detail renvoie "Projection not found"

Symptome utilisateur:

- Sur `/projections`, la liste affiche la projection fixture.
- Au clic sur les details: `Projection not found - The projection could not be loaded.`
- Trace ID UI: `7ac735d7c7c4ef15fb7ba5791eb5fc46`.

Evidence Aspire:

- Admin UI appelle `GET https://localhost:8091/api/v1/admin/projections/tenant-a/counter`.
- Admin Server tente ensuite une invocation DAPR vers EventStore:
  `http://localhost:43206/v1.0/invoke/eventstore/method/api/v1/admin/projections/tenant-a/counter`.
- EventStore repond `404 Not Found`.
- Admin Server logue `Admin service unavailable: GetProjectionDetail`.
- Exception:
  `HttpRequestException: Response status code does not indicate success: 404 (Not Found).`

Code suspect:

- `src/Hexalith.EventStore.Admin.Server/Services/DaprProjectionQueryService.cs`
  - construit `api/v1/admin/projections/{tenantId}/{projectionName}`;
  - appelle `EnsureSuccessStatusCode()`;
  - le fallback `CreateEmptyProjectionDetail(...)` n'est pas atteint en cas de 404.
- `src/Hexalith.EventStore/Controllers/AdminProjectionRebuildController.cs`
  - expose `GET /api/v1/admin/projections/{tenantId}/{projectionName}/rebuild-status`;
  - expose pause/resume/reset/replay/cancel/retry;
  - ne semble pas exposer `GET /api/v1/admin/projections/{tenantId}/{projectionName}`.

Analyse:

- La fixture `admin:projections:tenant-a` suffit pour la liste, mais pas pour le
  detail car le detail est delegue a un endpoint EventStore absent.
- Correct Course doit decider le contrat du detail projection:
  - soit ajouter un endpoint EventStore detail;
  - soit construire le detail depuis l'index Admin.Server;
  - soit changer l'UI pour ne pas proposer un detail tant que le backend ne le supporte pas.

Retest DW11 du 2026-05-20 apres restart Aspire, `FLUSHALL` Redis, et reapplication
des fixtures:

- Issue 11: OK.
- Projection detail ouverte: `counter` / `tenant-a`.
- Status: `Running`.
- Lag: `0`.
- Throughput: `0,0 /s`.
- Errors: `0`.
- Last Position: `18`.
- Last Processed: `4m ago` au moment du test.
- Subscribed Event Types: `0`, `No subscribed event types`.
- Configuration affichee: `{}`.
- Errors detail: `No errors recorded`.
- Conclusion: OK pour DW11. Le detail ne renvoie plus `Projection not found` pour
  la fixture `tenant-a` / `counter`; le detail fallback limite est affichable et
  coherent avec les donnees de liste.
- Observation hors perimetre DW11: les boutons Pause / Reset / Replay sont
  visibles sur le detail fallback. Pause a produit la trace
  `a696b60d04dd91c7c8c7c4e57530f98a`. Replay affiche `Replay initiated
  (Operation:...)` puis reste lance. Reset affiche `Reset initiated
  (Operation: 01KS2VTP9JHT2CCX5KPOPAY91H)` puis `Operation submitted-status may
  take a moment to update`. A surveiller dans un suivi projection-lifecycle si le
  comportement operationnel attendu n'est pas celui-ci.

### CC-3 / Issue 15 - Snapshot / Compaction / Backup / Export sont "deferred"

Symptomes utilisateur:

- Snapshot creation:
  `Manual snapshot creation is deferred. EventStore does not yet have an approved snapshot job model for operator-triggered snapshots.`
- Compaction:
  `Compaction is deferred. EventStore write-once event keys require an approved non-destructive compaction model before this operation can run.`
- Backup:
  `Backup creation is deferred. EventStore does not yet have an approved backup engine and manifest model.`
- Backup validation:
  `Backup validation is deferred. EventStore does not yet have an approved backup manifest and validation model.`
- Stream export:
  `Stream export is deferred. EventStore needs an approved bounded export contract, format, and event limit before this operation can run.`

Analyse:

- Ce n'est pas un crash runtime.
- C'est un ecart de scope/UX: les boutons sont testables, mais ils menent a des
  operations explicitement non implementees.
- Correct Course doit trancher:
  - accepter ces messages comme comportement attendu pour cette iteration;
  - ou creer une story UX pour afficher ces actions comme disabled/deferred avant clic;
  - ou creer des stories backend separees pour snapshot job model, compaction model,
    backup manifest/engine, validation et export borne.

Recommendation test (post-DW14):

- Marquer Issue 15 comme `OK - deferred explicite` quand le pattern
  Truth-before-submit est respecte: badge `Deferred by backend` visible
  avant la dialog, message exact dans le corps de la dialog, bouton final
  `Submit Deferred Request`, toast `warning` (jamais `success`).
- Marquer `Action-needed` si l'acceptance criteria exige une operation reelle
  (necessite alors une story backend separee pour snapshot job model,
  compaction non-destructif, backup manifest/engine, validation backup ou
  export borne).
- Marquer `KO` si un toast `success` apparait sur reponse deferree ou si l'un
  des elements Truth-before-submit manque.

### CC-4 / Issues 16 et 17 - Consistency check produit de faux positifs

Symptome utilisateur apres `Run Check`:

- Check completed pour `tenant-a / counter`.
- `Streams Checked = 1`.
- `Anomalies Found = 20`.
- 18 anomalies `sequencecontinuity`: `Missing event at sequence 1..18`.
- 1 anomalie `metadataconsistency`: `Aggregate metadata is missing.`
- 1 warning `projectionpositions`: `Domain-specific projection position validation is not granular.`

Verification Redis effectuee:

- `admin:stream-activity:all` contient bien:
  `tenant-a / counter / counter-1`, `eventCount = 18`, `lastEventSequence = 18`.
- Les cles brutes attendues par le consistency checker sont absentes:
  `tenant-a:counter:counter-1:events:*` retourne vide.
- La metadata brute est absente:
  `tenant-a:counter:counter-1:metadata` retourne `0`.
- Mais les vraies cles actor-state existent:
  `eventstore||AggregateActor||tenant-a:counter:counter-1||tenant-a:counter:counter-1:events:1`
  jusqu'a `events:18`, ainsi que
  `eventstore||AggregateActor||tenant-a:counter:counter-1||tenant-a:counter:counter-1:metadata`.

Code suspect:

- `src/Hexalith.EventStore.Admin.Server/Services/DaprConsistencyCommandService.cs`
  - ligne observee pour les events:
    `string eventKey = $"{stream.TenantId}:{stream.Domain}:{stream.AggregateId}:events:{sequence}";`
  - ligne observee pour metadata:
    `string metadataKey = $"{stream.TenantId}:{stream.Domain}:{stream.AggregateId}:metadata";`

Analyse:

- Les events ne sont pas absents; ils sont stockes sous la cle DAPR actor-state
  complete.
- Les anomalies SequenceContinuity et MetadataConsistency sont donc tres
  probablement de faux positifs dus a un mismatch de namespace/cle.
- Correct Course doit creer une story pour que le consistency checker lise via
  le meme contrat que l'aggregate actor, ou via un index admin fiable, au lieu
  de reconstruire des cles brutes.

Trace additionnelle UI pendant Consistency:

- Recent Aspire error logs montrent aussi:
  `InvalidOperationException: The current thread is not associated with the Dispatcher. Use InvokeAsync() to switch execution to the Dispatcher when triggering rendering or component state.`
- Source:
  `src/Hexalith.EventStore.Admin.UI/Pages/Consistency.razor`, ligne observee `StateHasChanged()` dans `finally`.
- A traiter dans la meme story UI ou une sous-story separee:
  utiliser `await InvokeAsync(StateHasChanged)` apres les operations async de confirmation.

### CC-5 / Issue 18 - Enable tenant timeout

Symptome utilisateur:

- Apres `Enable` sur `manual-test-tenant-a`:
  `Failed to enable tenant.: The request was canceled due to the configured HttpClient.Timeout of 30 seconds elapsing.`

Evidence Aspire:

- Admin UI observe des timeouts Polly vers:
  `https://eventstore-admin/api/v1/admin/tenants/manual-test-tenant-a/enable`.
- Admin Server invoque EventStore via:
  `http://localhost:43206/v1.0/invoke/eventstore/method/api/v1/commands`.
- EventStore logue `ActorInvocationFailed` pour:
  - `TenantId=system`
  - `Domain=tenants`
  - `AggregateId=manual-test-tenant-a`
  - `CommandType=EnableTenant`
  - `ActorId=system:tenants:manual-test-tenant-a`
- Exception EventStore:
  `TaskCanceledException: The request was canceled due to the configured HttpClient.Timeout of 100 seconds elapsing.`
- Des timeouts de lecture tenants apparaissent aussi sur la query pipeline:
  `GET /api/v1/admin/tenants`, `TimeoutRejectedException: The operation didn't complete within the allowed timeout of '00:00:10'.`
- Des spans montrent des appels acteur vers:
  `TenantsProjectionActor/tenants:system:index/method/QueryAsync`
  qui se terminent en `TaskCanceledException`.

Code suspect:

- `src/Hexalith.EventStore.Admin.Server/Services/DaprTenantCommandService.cs`
  - `EnableTenantAsync(...)` route vers `SubmitCommandAsync(...)`.
  - `SubmitCommandAsync(...)` poste vers `api/v1/commands` via EventStore.
- EventStore `CommandRouter` echoue lors de l'invocation actor.

Analyse:

- Ce n'est pas seulement un timeout UI; le backend EventStore attend un acteur
  tenant/projection qui ne repond pas dans le run.
- Correct Course doit investiguer le routage actor tenant en mode Aspire/DAPR:
  placement, actor registration, app-id cible, access control DAPR, ou projection
  tenant bloquee.
- La story devrait aussi clarifier le comportement attendu UI pour une operation
  tenant longue: operation async avec status/correlation id, ou timeout court et
  message d'attente explicite.

### CC-6 / Trace noise utile - TypeCatalog navigation pendant disconnect

Observation Aspire hors checklist principale:

- Logs `eventstore-admin-ui`:
  `Navigation failed when changing the location to /types?type=CounterAggregate`
  et `/types?tab=aggregates`.
- Exception:
  `JSDisconnectedException: JavaScript interop calls cannot be issued at this time. This is because the circuit has disconnected and is being disposed.`
- Stack:
  `TypeCatalog.UpdateUrl(...)` puis `TypeCatalog.OnTabChanged(...)`.

Analyse:

- A classer plus bas que les bugs ci-dessus, sauf si l'utilisateur reproduit un
  crash visible sur `/types`.
- Peut faire partie d'une story hygiene Blazor Server: eviter `NavigateTo` pendant
  disposal/disconnect ou proteger les callbacks de tabs.

### Statut propose pour le prochain Correct Course

| ID | Zone | Classification | Priorite proposee |
| --- | --- | --- | --- |
| CC-1 | Dead letters | Bug backend bloquant | Haute |
| CC-2 | Projection detail | Contrat backend/API incomplet | Haute |
| CC-3 | Snapshot/Compaction/Backup/Export | Scope deferred a clarifier | Moyenne |
| CC-4 | Consistency | Faux positifs par mismatch de cles DAPR actor-state | Haute |
| CC-5 | Tenant enable/list | Timeout actor/projection tenant | Haute |
| CC-6 | TypeCatalog navigation | Hygiene UI Blazor Server | Basse/Moyenne |

## 6. Nettoyage des fixtures

Si tu veux supprimer seulement les fixtures de ce fichier sans vider Redis:

```powershell
$adminDaprPort = 30166
$stateStore = "statestore"

$keys = @(
    "admin:dead-letters:all",
    "admin:dead-letters:tenant-a",
    "admin:projections:all",
    "admin:projections:tenant-a",
    "admin:type-catalog:events:all",
    "admin:type-catalog:events:counter",
    "admin:type-catalog:commands:all",
    "admin:type-catalog:commands:counter",
    "admin:type-catalog:aggregates:all",
    "admin:type-catalog:aggregates:counter",
    "admin:storage-snapshot-policies:all",
    "admin:storage-snapshot-policies:tenant-a",
    "admin:storage-compaction-jobs:all",
    "admin:storage-compaction-jobs:tenant-a",
    "admin:backup-jobs:all",
    "admin:backup-jobs:tenant-a"
)

foreach ($key in $keys) {
    Invoke-RestMethod `
        -Method Delete `
        -Uri "http://localhost:$adminDaprPort/v1.0/state/$stateStore/$([uri]::EscapeDataString($key))"
}
```

Si tu veux repartir completement de zero:

```powershell
aspire stop --non-interactive --apphost .\src\Hexalith.EventStore.AppHost\Hexalith.EventStore.AppHost.csproj
docker exec dapr_redis redis-cli FLUSHALL
```

## 7. Evidence finale a me retourner

Copie-colle ce bloc apres ton run:

```text
Issue 2: OK / KO
State Inspector: OK / KO
Issue 13: OK / KO
Issue 9: OK
Issue 11: OK
Issue 12: OK / KO / non teste
Issue 15: OK - deferred explicite / Action-needed / KO / non teste
Issue 16: OK (2026-05-20, DW12 retest)
Issue 17: OK (2026-05-20, DW12 retest)
Issue 18: OK / KO / non teste

Notes / messages exacts:
- Issue 9 Retry/Skip/Archive: 404 NotFound recuperable pour `manual-dlq-tenant-a-001`; plus de 500 model-binding.
- Issue 11 Projection `counter` (`tenant-a`): detail affiche Running, Lag 0, Last Position 18, `{}`, pas d'erreurs.
- Observation: boutons Pause/Reset/Replay visibles sur le detail fallback; Reset operation `01KS2VTP9JHT2CCX5KPOPAY91H`, Pause trace `a696b60d04dd91c7c8c7c4e57530f98a`.
- Issue 16 (DW12 retest, 2026-05-20): `/consistency Run Check` sur `tenant-a / counter` -> `Check ID 01KS2ZFRC4H500HCC7B49ADDR3`, `Completed`, `Streams Checked = 1`, `Anomalies Found = 1`. Apres le run, les stat cards affichent `Total Checks = 1`, `Last Check = 9s ago`, `Total Anomalies = 1`, `Running Now = 0` (plus de subtitle fige a 0/Never).
- Issue 17 (DW12 retest, 2026-05-20): aucune anomalie `SequenceContinuity` "Missing event at sequence N" pour `counter-1`, aucune anomalie `MetadataConsistency` "Aggregate metadata is missing.". Seule anomalie restante = `Warning / projectionpositions / tenant-a / counter / all` libellee "Projection diagnostic limitation: domain-scoped projection positions are not granular." (limitation classifiee, pas data-loss).
- AC10 dispatcher (DW12 retest, 2026-05-20): exercice trigger/cancel rapide sur `/consistency` sans aucune exception `Consistency.OnTriggerConfirm` ou `Consistency.OnCancelConfirm` dans la console navigateur ni dans les logs serveur Aspire.
```
