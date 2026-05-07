# Admin UI — Issues détectées en test manuel (session 2026-05-06)

## Issue #1 — BLOQUANT : Tous les dropdowns Tenant sont vides

### Symptôme
Sur **`/commands`**, **`/events`** et **`/streams`** (testés manuellement), le dropdown `Tenant` n'affiche que l'item « All Tenants ». Aucune valeur tenant n'est sélectionnable, **même quand des évènements/streams existent sous `tenant-a`**. Le filtrage par tenant via la UI est donc impossible.

Probablement aussi affecté (à confirmer) : `/projections`, et toute page utilisant un dropdown Tenant.

### Reproduction
1. `docker exec dapr_redis redis-cli FLUSHALL`
2. `dotnet run --project src/Hexalith.EventStore.AppHost`
3. Sample Blazor UI → Pattern 2 → Increment ×5, Decrement ×2, Reset, Increment ×10 (18 évènements créés sous `tenant-a/counter/counter-1`)
4. Vérifier dans Redis : 18 clés `eventstore||AggregateActor||tenant-a:counter:counter-1||tenant-a:counter:counter-1:events:N` sont présentes ✅
5. Ouvrir `/commands` (ou `/events`, `/streams`)
6. Cliquer sur le dropdown `Tenant`
7. **Attendu** : `tenant-a` listé. **Observé** : seul « All Tenants ».

### Cause racine
Les pages alimentent la dropdown via `GET /api/v1/admin/tenants` (service Tenants registry), **pas** via les tenants observés dans les évènements/streams.

Code concerné :
- `src/Hexalith.EventStore.Admin.UI/Pages/Commands.razor:240-251` (méthode `LoadTenantsAsync`)
- `src/Hexalith.EventStore.Admin.UI/Services/AdminStreamApiClient.cs:123-127` (`GetTenantsAsync`)
- Idem dans `Events.razor`, `Streams.razor`, `Projections.razor`

`tenant-a` n'étant pas enregistré dans le service Tenants (le seeding du sample n'écrit que des évènements), l'API `/api/v1/admin/tenants` renvoie une liste vide → dropdown vide.

### Pages affectées (fix groupé)
- `/commands` ✅ confirmé
- `/events` ✅ confirmé
- `/streams` ✅ confirmé
- `/projections` ⚠️ à confirmer
- `/storage`, `/snapshots`, `/compaction`, `/backups`, `/consistency` (text input, donc moins impactant) — à reconfirmer

### Décisions à prendre (choisir une option)
1. **Fallback automatique** : la dropdown agrège l'union de `GetTenantsAsync()` + tenants distincts observés dans les streams récents. Avantage : marche immédiatement sur un store seedé.
2. **Auto-registration** : enregistrer automatiquement le tenant dans le service Tenants à la première écriture d'évènement. Avantage : cohérence du registry.
3. **Enregistrement explicite imposé** : laisser le comportement actuel mais afficher un message d'aide UX dans la dropdown vide (« No tenants registered. Go to /tenants to create one. ») et bloquer le seeding tant qu'aucun tenant n'est enregistré.

### Workaround pendant les tests
- Laisser la dropdown sur « All Tenants ».
- Utiliser les autres filtres (`Status`, `Type`, `Domain`, search box) qui fonctionnent bien.
- Pour `/streams`, utiliser le deep-link query string : `/streams?tenant=tenant-a&domain=counter` — le filtrage côté serveur fonctionne, c'est uniquement le contrôle de saisie qui est cassé.

---

## Issue #2 — Page `/` (Home) affiche toujours 0 / 0 / 0 / 0

### Symptôme
Page d'accueil `/` :
- Active Streams : `0`
- Total Events : `0`
- Events/sec : `0,0/s`
- Error Rate : `0,00%`
- ActivityChart jamais rendu → EmptyState « 0 commands processed » à la place

… alors que le store contient des évènements (vérifiable via `/streams`, `/events`, `/commands` qui voient bien les données).

### Cause racine
`src/Hexalith.EventStore.Admin.Server/Services/DaprHealthQueryService.cs:130-136` :

```csharp
return new SystemHealthReport(
    overallStatus,
    TotalEventCount: 0,       // ← stub codé en dur
    EventsPerSecond: 0,       // ← stub codé en dur
    ErrorPercentage: 0,       // ← stub codé en dur
    components,
    links);
```

Les 3 métriques renvoyées par `GET /api/v1/admin/health` sont codées en dur à 0. La spec `_bmad-output/implementation-artifacts/15-7-health-dashboard-with-observability-deep-links.md` définissait bien ces champs au niveau DTO mais la logique d'agrégation côté service n'a jamais été implémentée.

### Conséquence côté UI
`Index.razor:50` gate l'ActivityChart sur `_healthReport.TotalEventCount > 0` → la branche n'est jamais prise → EmptyState toujours visible.

L'UI calcule pourtant déjà l'`ActivityChart` localement via `BuildActivityBuckets(data.Streams.Items)` à `Index.razor:162`. Côté serveur uniquement, pas de bouchon de calcul.

### Fix à faire
1. Côté serveur : implémenter l'agrégation réelle dans `DaprHealthQueryService.GetSystemHealthAsync()` :
   - `TotalEventCount` = somme des `EventCount` sur tous les streams
   - `EventsPerSecond` = throughput rolling sur fenêtre glissante (1 min ?)
   - `ErrorPercentage` = ratio commands rejected/failed sur la même fenêtre
2. Alternative court terme côté UI : changer le gate ligne 50 pour `(_streams?.Items.Count ?? 0) > 0` (les buckets sont déjà calculés depuis les streams).

---

## Issue #3 — Cliquer sur l'Aggregate ID copie ET navigue (event bubbling)

### Symptôme
Sur `/streams`, cliquer sur l'identifiant tronqué dans la colonne `Aggregate ID` :
- ✅ copie bien la valeur dans le presse-papiers (toast attendu)
- ❌ **mais déclenche aussi** la navigation vers la page de détail `/streams/{tenant}/{domain}/{aggregateId}`

L'utilisateur n'a aucun moyen de simplement copier l'ID sans quitter la page.

### Cause racine
`src/Hexalith.EventStore.Admin.UI/Pages/Streams.razor:51-53`

```razor
<span class="monospace grid-cell-truncate" title="@context.AggregateId"
      style="cursor: pointer;"
      @onclick="() => CopyAggregateId(context.AggregateId)">@TruncateId(context.AggregateId)</span>
```

Le `@onclick` du `<span>` ne stoppe pas la propagation. Le DataGrid parent (ligne 42) a `OnRowClick="@(row => OnRowClick(row.Item))"` qui navigue → l'évènement remonte et déclenche les deux actions.

### Fix
Ajouter `@onclick:stopPropagation="true"` sur le `<span>` :

```razor
<span class="monospace grid-cell-truncate" title="@context.AggregateId"
      style="cursor: pointer;"
      @onclick="() => CopyAggregateId(context.AggregateId)"
      @onclick:stopPropagation="true">@TruncateId(context.AggregateId)</span>
```

À vérifier : si d'autres colonnes/cellules ont des `@onclick` dans une grid avec `OnRowClick`, leur appliquer le même fix (audit rapide à faire sur les autres pages — `Commands.razor`, `Events.razor`, etc.).

---

## Issue #4 — StreamDetail : pas de filtre par nom de type d'évènement

### Symptôme
Sur `/streams/{tenant}/{domain}/{aggregate}`, le filtre `Entry type` propose uniquement 3 boutons : `All`, `Command`, `Event`, `Query`. **Aucun moyen depuis la UI** de filtrer sur un type nommé spécifique (ex: voir uniquement les `CounterReset`, ou uniquement les `IncrementCounter`).

Pour un stream long avec des centaines d'évènements de types variés, l'utilisateur doit scroller manuellement.

### Code concerné
- `src/Hexalith.EventStore.Admin.UI/Components/TimelineFilterBar.razor` — uniquement 3 boutons par `TimelineEntryType` (enum)
- `src/Hexalith.EventStore.Admin.UI/Pages/StreamDetail.razor:114-117` — bind sur `_selectedEntryType` (TimelineEntryType?) et `_correlationFilter` (string)
- `StreamDetail.razor:436-461` — la logique de filtrage in-memory pourrait facilement supporter un prédicat par `TypeName` puisque les entrées exposent déjà cette donnée

### Suggestion
Ajouter à `TimelineFilterBar.razor` :
- Un `FluentSearch` libre alimenté par les types observés dans la timeline courante (`IncrementCounter`, `CounterReset`, `CounterIncremented`…)
- Combinable avec le toggle `All/Command/Event/Query` (les deux filtres s'additionnent : kind ET nom de type)
- Persistance via query string (`?typeName=CounterReset`) comme déjà fait pour `?type=event` et `?correlation=...`

### Sévérité
Pas un bug — limitation fonctionnelle. À tracker comme enhancement.

---

## Issue #5 — BLOQUANT : State replay ne rejoue pas les `Apply()` (deep-merge naïf des payloads)

### Symptôme
Sur la page `/streams/{tenant}/{domain}/{aggregate}`, **trois outils renvoient toujours `{}` comme état d'agrégat**, quel que soit la séquence demandée :
- **Blame** : « Aggregate state has no fields at this position » à #18
- **Step Through** : `{}` à chaque sequence (1, 2, … 18)
- **Sandbox** : « Command accepted — 1 event(s) would be produced » mais « Resulting State (after applying 1 events) » = `{}`

Pourtant, le scénario de seeding crée 18 évènements qui devraient amener `Count = 10` dans le `CounterState`.

### Cause racine
`src/Hexalith.EventStore/Controllers/AdminStreamQueryController.cs:1477-1497`

```csharp
private static JsonObject ReconstructState(ServerEventEnvelope[] allEvents, long upToSequence) {
    var state = new JsonObject();
    foreach (ServerEventEnvelope evt in allEvents) {
        if (evt.SequenceNumber > upToSequence) break;
        var eventPayload = JsonNode.Parse(evt.Payload, ...);
        if (eventPayload is JsonObject payloadObj) {
            DeepMerge(state, payloadObj);   // ← merge naïf des payloads bruts
        }
    }
    return state;
}
```

L'implémentation **bypasse complètement** la méthode `Apply()` de l'aggregate. Elle ne fait que fusionner les payloads JSON bruts des évènements. Cela ne fonctionne **que pour les évènements porteurs de l'état complet dans leur payload** (anti-pattern dans la plupart des modélisations DDD).

Les évènements du sample Counter sont des **marker records** sans champs :

```csharp
public sealed record CounterIncremented : IEventPayload;   // payload sérialisé = {}
public sealed record CounterDecremented : IEventPayload;   // payload sérialisé = {}
public sealed record CounterReset      : IEventPayload;    // payload sérialisé = {}
public sealed record CounterClosed     : IEventPayload;    // payload sérialisé = {}
```

L'information métier est **dans le type d'évènement**, pas dans son payload — c'est l'`Apply()` côté aggregate qui transforme « j'ai reçu un CounterIncremented » en `Count++`. Avec le deep-merge naïf, 18 `{}` mergés = `{}`.

### Pages affectées (toutes par le même bug serveur)
- `/streams/{...}/state?at=N` (Step Through) → `{}` permanent
- `/streams/{...}/blame` (Blame Viewer) → "no fields at this position"
- `/streams/{...}/diff` (StateDiffViewer) → diff toujours vide
- `/streams/{...}/bisect` (Bisect) → la binary search compare des `{}` entre eux → ne trouve jamais de divergence
- `/streams/{...}/sandbox` (Sandbox dry-run) → la commande passe mais l'état résultant est faux
- `/streams/{...}/causation` (CausationChain) — à vérifier

### Fix
**Refactor profond** : `ReconstructState` doit utiliser le moteur de l'aggregate (réflexion sur les méthodes `Apply(EventType)` exposées par la classe d'état, comme la convention fluente du framework) plutôt que de merger des payloads JSON.

Pseudo-code attendu :
```csharp
// 1. Résoudre le type d'aggregate state pour ce domain/aggregateType
Type stateType = AggregateRegistry.Resolve(domain, aggregateType);
object state = Activator.CreateInstance(stateType)!;

// 2. Pour chaque évènement, désérialiser vers le type concret puis Apply
foreach (var evt in allEvents.TakeWhile(e => e.SequenceNumber <= upToSequence)) {
    Type eventType = EventTypeRegistry.Resolve(evt.TypeName);
    object payload = JsonSerializer.Deserialize(evt.Payload, eventType);
    MethodInfo apply = stateType.GetMethod("Apply", new[] { eventType });
    apply.Invoke(state, new[] { payload });
}

// 3. Sérialiser l'état réel
return JsonSerializer.SerializeToNode(state)!.AsObject();
```

Il y a déjà un mécanisme de discovery par convention dans le projet (« Fluent Convention » mentionnée dans CLAUDE.md). Le réutiliser ici.

### Sévérité
🛑 **Bloquant** : la fonctionnalité phare de la page de détail (« debug stream comme un debugger »), vendue comme highlight de l'Admin UI, est **inutilisable en pratique** sur n'importe quelle modélisation DDD propre. Les seuls aggregates qui « marchent » seraient ceux qui mettent toute leur state dans chaque payload (anti-pattern).

Conséquence : Blame, Bisect, Step Through, StateDiff et Sandbox renvoient des résultats **systématiquement faux mais sans erreur** — danger maximum côté UX (l'utilisateur peut croire que son aggregate est cassé alors que c'est l'inspection qui ment).

---

## Récapitulatif

| # | Sévérité | Page(s) | Description courte |
|---|---|---|---|
| 1 | 🛑 Bloquant | `/commands`, `/events`, `/streams` (+ probablement autres) | Dropdown Tenant systématiquement vide |
| 2 | ⚠️ Régression UX | `/` (Home) | Stats hardcodées à 0 → EmptyState permanent |
| 3 | ⚠️ UX | `/streams` (probable régression sur autres grids) | Clic sur Aggregate ID copie ET navigue (manque `stopPropagation`) |
| 4 | 💡 Enhancement | `/streams/{...}` (StreamDetail) | Pas de filtre sur le nom du type d'évènement, seulement sur le kind (Command/Event/Query) |
| 5 | 🛑 Bloquant | `/streams/{...}` (Blame, Step Through, Sandbox, Bisect, StateDiff) | State replay = deep-merge des payloads, ne rejoue pas `Apply()` → état toujours `{}` pour les marker events (cas DDD courant) |
