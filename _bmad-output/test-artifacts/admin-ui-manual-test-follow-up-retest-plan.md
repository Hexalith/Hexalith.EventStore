# Admin UI Manual-Test Follow-Up Cluster - Retest courant

Date de preparation: 2026-05-20

Ce fichier est maintenant limite au run manuel courant, c'est-a-dire aux
verifications qui peuvent etre refaites avec l'environnement Aspire sain et le
seed Counter deja disponible. Les scenarios qui demandent des entrees
specifiques ou une fixture supplementaire ont ete retires de ce fichier et
deplaces dans:

`_bmad-output/test-artifacts/admin-ui-manual-test-follow-up-deferred-entry-runbook.md`

Sources historiques:

- `_bmad-output/test-artifacts/admin-ui-manual-test-guide-issues.md`
- `_bmad-output/test-artifacts/admin-ui-manual-test-guide-issues-2026-05-07.md`
- `_bmad-output/implementation-artifacts/sprint-status.yaml`

## 0. Etat initial sain

Actions deja effectuees le 2026-05-20:

```powershell
aspire stop --non-interactive --apphost .\src\Hexalith.EventStore.AppHost\Hexalith.EventStore.AppHost.csproj
docker exec dapr_redis redis-cli FLUSHALL
docker exec dapr_redis redis-cli DBSIZE
```

Resultat obtenu:

```text
DBSIZE = 0
```

Aspire a ensuite ete relance avec:

```powershell
$dotnetDir = Join-Path $env:LOCALAPPDATA 'Microsoft\dotnet'
$env:PATH = "$dotnetDir;$dotnetDir\tools;$env:PATH"
$env:EnableKeycloak = 'false'
aspire run --detach --non-interactive --no-build --project .\src\Hexalith.EventStore.AppHost\Hexalith.EventStore.AppHost.csproj
```

Dashboard Aspire observe:

```text
https://localhost:17017/login?t=802b69fa7b2b452f8ffac221ae81cc29
```

## 1. Donnees de reference du run courant

Dans la Sample Blazor UI, utiliser le scenario Counter suivant si les donnees
doivent etre recreees:

| Etape | Action | Resultat attendu |
| --- | --- | --- |
| 1 | Increment x5 | Counter = 5 |
| 2 | Decrement x2 | Counter = 3 |
| 3 | Reset x1 | Counter = 0 |
| 4 | Increment x10 | Counter = 10 |
| 5 | GetCounterStatus | Reponse avec `Value = 10` |

Identifiants attendus:

| Champ | Valeur |
| --- | --- |
| Tenant | `tenant-a` |
| Domain | `counter` |
| Aggregate id | `counter-1` |
| Stream | `tenant-a/counter/counter-1` |
| Nombre d'evenements attendu | environ `18` |

## 2. Statut du retest utilisateur

| Issue | Statut courant | Decision |
| --- | --- | --- |
| 1 | OK | Conserver comme valide. |
| 2 | A corriger | `Events/sec` et `Error Rate` ne doivent pas afficher `unavailable` quand la source est disponible et que la vraie valeur est zero. |
| 3 | OK | Conserver comme valide. |
| 4 | OK | Conserver comme valide. |
| 5 | OK | Conserver comme valide. |
| 6 | OK | Conserver comme valide. |
| 7 | OK | Conserver comme valide, sauf precision Issue 2 sur les vrais zeros. |
| 8 | OK | Conserver comme valide. |
| 9 | Non testable maintenant | Deplace dans le runbook differe: aucune entree dead-letter. |
| 10 | OK | Conserver comme valide. |
| 11 | Non testable maintenant | Deplace dans le runbook differe: aucune entree projection visible. |
| 12 | Non testable maintenant | Deplace dans le runbook differe: aucune entree type catalog visible. |
| 13 | A corriger | Retirer le texte inutile a cote du dropdown: `Role: Operator` et `Development role Operator selected.` |
| 14 | OK | `/storage` est coherent avec `tenant-a` + le stream systeme bootstrap. |
| 15 | Non testable maintenant | Deplace dans le runbook differe: actions snapshots/compaction/backups demandent des entrees ou operations dediees. |
| 16 | A refaire plus tard | Deplace dans le runbook differe avec etapes exactes de Run Check. |
| 17 | A refaire plus tard | Deplace dans le runbook differe car depend de l'etat projection/consistency. |
| 18 | A refaire plus tard | Deplace dans le runbook differe car demande un tenant testable active/disabled. |
| Nouveau | A corriger | State Inspector se rouvre tout seul apres clic sur `All`, `Commands`, `Events`, ou `Queries`. |

## 3. Corrections a cadrer en story avant implementation

Les modifications produit ne doivent pas etre faites dans ce run. Les points
suivants sont candidats pour une story de correction test-first.

### Issue 2 - Vrais zeros vs unavailable

Observation utilisateur:

```text
Events/sec: unavailable
Error Rate: unavailable
```

Comportement attendu:

- Si la source de metrique est indisponible, afficher `unavailable`.
- Si la source de metrique est disponible et que le vrai resultat vaut zero,
  afficher une valeur numerique:
  - `Events/sec`: `0`, `0.0/s`, ou format equivalent deja utilise par la page.
  - `Error Rate`: `0%` ou `0.0%`.
- Ne pas transformer un vrai zero en `unavailable`.
- Ne pas transformer une source indisponible en faux zero.

Tests a prevoir avant code:

- `DaprHealthQueryServiceTests`: source stream/command disponible et vide =>
  `EventsPerSecondStatus = Available`, `EventsPerSecond = 0`,
  `ErrorPercentageStatus = Available`, `ErrorPercentage = 0`.
- `DaprHealthQueryServiceTests`: source stream/command indisponible =>
  statut `Unavailable`, valeur numerique ignoree par la UI.
- `IndexPageTests` et `HealthPageTests`: affichage distingue vrai zero et
  `unavailable`.

### Nouveau bug - State Inspector rouvre apres changement de filtre timeline

Observation utilisateur:

```text
State Inspector s'ouvre tout seul apres avoir ete ouvert quand on clique sur
All / Commands / Events / Queries.
```

Comportement attendu:

- Fermer le State Inspector doit vider l'etat d'ouverture cote page.
- Cliquer `All`, `Commands`, `Events`, ou `Queries` ne doit jamais rouvrir
  l'inspector par l'ancien etat.
- Si l'URL contient un parametre d'inspection, changer de filtre doit soit le
  supprimer, soit prouver qu'il ne reprovoque pas l'ouverture.
- Le comportement normal "clic Inspect ouvre l'inspector" doit rester intact.

Tests a prevoir avant code:

- `StreamDetailPageTests`: ouvrir puis fermer le State Inspector, cliquer
  `All`, verifier qu'aucun modal ne reapparait.
- `StreamDetailPageTests`: refaire la meme verification pour `Commands`,
  `Events`, et `Queries`.
- `StreamDetailPageTests`: apres fermeture, l'URL ou l'etat interne ne garde
  pas de sequence inspectee qui force la reouverture.

### Issue 13 - Texte role switcher inutile

Observation utilisateur:

```text
Role: Operator
Development role Operator selected.
```

Comportement attendu:

- Le header garde uniquement le dropdown de role developpement.
- Aucun texte visible redondant ne doit apparaitre a cote du dropdown.
- Le dropdown doit conserver un label accessible.
- Un texte `aria-live` ne doit pas etre visible dans la page. Si une annonce
  accessible existe, elle doit rester cachee visuellement.

Tests a prevoir avant code:

- `MainLayoutTests`: en dev sans Keycloak, le dropdown existe.
- `MainLayoutTests`: le texte visible `Role: Operator` est absent.
- `MainLayoutTests`: le texte visible `Development role Operator selected.`
  est absent.
- `MainLayoutTests`: le select conserve un label accessible
  `Development role`.

## 4. Issue 14 - Conclusion storage

Observation utilisateur sur `/storage`:

```text
Total Events: 19
Total Streams: 2
Tenants: 2
Avg Growth/Day: N/A
tenant-a: 18
system: 1
tenant-a/counter/CounterAggregate: 18
system/global-administrators/Global-administratorsAggregate: 1
```

Conclusion:

- C'est coherent pour ce run.
- Les `18` evenements `tenant-a/counter/counter-1` correspondent au seed
  Counter attendu.
- L'entree `system/global-administrators` explique le stream et l'evenement
  systeme supplementaires.
- `Avg Growth/Day`, les tailles, et les champs non mesures peuvent rester
  `N/A` tant qu'aucune source portable ne les renseigne.

Statut Issue 14: OK.

## 5. Evidence a conserver pour ce run courant

Pour chaque point encore ouvert:

- URL testee.
- Heure approximative.
- Role actif (`ReadOnly`, `Operator`, `Admin`).
- Resultat attendu / resultat observe.
- Screenshot si la verification est visuelle.
- Operation id, trace id, correlation id ou check id quand disponible.
- Message exact si le test echoue encore.

Emplacement recommande:

```text
_bmad-output/test-artifacts/admin-ui-manual-test-follow-up-rerun-YYYY-MM-DD/
```

## 6. Commandes utiles

Verifier Aspire:

```powershell
aspire ps --non-interactive
```

Arreter Aspire:

```powershell
aspire stop --non-interactive --apphost .\src\Hexalith.EventStore.AppHost\Hexalith.EventStore.AppHost.csproj
```

Vider Redis:

```powershell
docker exec dapr_redis redis-cli FLUSHALL
docker exec dapr_redis redis-cli DBSIZE
```
