# Admin UI — Issues détectées en test manuel (session 2026-05-07, suite)

> Suite du fichier `admin-ui-manual-test-guide-issues.md` (session 2026-05-06).
> Issues #1, #2, #3 fixées dans `af8c227c`. Issue #5 fixée dans `dd2d4447`. Issue #4 deferred (enhancement).
>
> La présente session reprend le guide à partir de §3.6 après un démarrage à froid (`flush Redis → build → aspire run`) et le seeding canonique `tenant-a/counter/counter-1` (Pattern 2 : 5 inc, 2 dec, reset, 10 inc — 18 events / 19 totaux avec system stream).

---

## Issue #6 — BLOQUANT : `/health` ne charge plus quand Redis est down

### Symptôme
Quand Redis est arrêté pendant qu'Aspire tourne :
1. La page `/health` ne se charge plus du tout (page blanche / spinner infini, à confirmer du logs).
2. Au redémarrage de Redis, la page repart dans l'état nominal — ce qui prouve que ce n'est pas une corruption d'état mais une dépendance dure de la page elle-même sur Redis.

### Reproduction
1. Aspire running, `/health` accessible normalement.
2. Dans un autre terminal : `docker stop dapr_redis`.
3. Refresh `/health`.
4. **Attendu** : `state.redis` bascule en `Unhealthy` mais la page reste fonctionnelle (le but d'une page de monitoring est précisément d'afficher l'état dégradé).
5. **Observé** : la page ne charge plus.
6. `docker start dapr_redis` → la page repart, retour à l'état nominal.

### Cause racine probable
La query qui alimente `/health` (`DaprHealthQueryService.GetSystemHealthAsync` côté `Hexalith.EventStore.Admin.Server`) dépend elle-même du state-store DAPR (qui pointe sur Redis) pour récupérer les composants ou un cache de health. Quand Redis tombe, la query timeout / throw au lieu de retourner un rapport partiel marquant `state.redis` comme `Unhealthy`.

À auditer aussi :
- Le pipeline DAPR sidecar Admin.Server : si le sidecar lui-même ne peut plus joindre Redis (state store), tous les `DaprClient.*Async()` peuvent rester bloqués jusqu'à timeout.
- Le rendering Blazor : si la query throw une `RpcException` non catchée côté UI, on tombe sur un blank screen au lieu d'un IssueBanner.

### Fix attendu
1. Côté serveur : `DaprHealthQueryService` doit considérer un timeout / une exception de requête comme un signal `Unhealthy` du composant concerné, **pas** comme un échec de la query globale. Retourner un `SystemHealthReport` partiel avec `state.redis` en `Unhealthy` et les autres composants non-Redis dans leur état réel.
2. Côté UI : `Health.razor` doit afficher l'IssueBanner / EmptyState quand le rapport global est null ou en erreur, jamais une page blanche. Pattern déjà appliqué sur `/` (Home) → réutiliser.

### Sévérité
🛑 **Bloquant** : c'est précisément la situation où `/health` doit fonctionner (l'opérateur cherche à diagnostiquer une panne d'infra). Une page de monitoring qui crash quand l'infra qu'elle surveille tombe est inutilisable.

---

## Issue #7 — Incohérence d'affichage métriques `0` vs `unavailable` entre `/` et `/health`

### Symptôme
Pour les mêmes 3 métriques (`TotalEventCount`, `EventsPerSecond`, `ErrorPercentage`) issues du même DTO `SystemHealthReport` :

| Métrique | Affichage `/` (Home) | Affichage `/health` |
|---|---|---|
| Total Events | `19` | `19` ✅ cohérent |
| Events/sec | `unavailable` | `0,0` |
| Error Rate | `unavailable` | `0,0%` |

L'utilisateur voit deux pages contradictoires pour la même donnée à la même seconde.

### Comportement attendu
Le fix `af8c227c` a introduit `SystemHealthMetricStatus` (enum) sur chaque métrique pour distinguer :
- `Available` + value : la source est wired et renvoie une vraie valeur (afficher `0,0` ou `0,0%` quand la valeur est vraiment 0).
- `Unavailable` : la source n'est pas wired (afficher `unavailable`).

L'affichage doit être **cohérent partout** :
- Si la source est wired et renvoie 0 → afficher `0,0` / `0,0%` sur les deux pages.
- Si la source est non wired → afficher `unavailable` sur les deux pages.

Aujourd'hui `/health` semble traiter ces métriques comme toujours `Available` (affiche la valeur), tandis que `/` (Home) semble les traiter comme toujours `Unavailable` (affiche le label) — l'un des deux est faux.

### Code concerné
- `src/Hexalith.EventStore.Admin.UI/Pages/Index.razor` (rendering Home, gate ligne ~50 sur les métriques)
- `src/Hexalith.EventStore.Admin.UI/Pages/Health.razor` (rendering /health)
- `src/Hexalith.EventStore.Admin.Server/Services/DaprHealthQueryService.cs` (source des `*Status` champs)

### Fix attendu
1. Décider la source de vérité : si `EventsPerSecond` et `ErrorPercentage` ne sont **pas** wirés à une vraie source de throughput (cas actuel d'après le commit `af8c227c`), alors `Status = Unavailable` doit être renvoyé par le service et **les deux pages** doivent afficher `unavailable`.
2. Si l'intention est que ces métriques soient bien wirées et que la valeur réelle est 0 (cas du store fraîchement seedé sans activité récente), alors `Status = Available` + value = 0 et **les deux pages** doivent afficher `0,0` / `0,0%`.
3. Auditer chaque card sur les deux pages pour s'assurer qu'elles consomment bien les `*Status` champs et appliquent la même logique de rendu (probablement extraire un helper `RenderMetric(value, status, format)`).

### Sévérité
⚠️ **UX** : pas un bug de données, mais une incohérence visuelle qui sape la confiance dans le dashboard. Le user a explicitement demandé : « j'aimerais que ce soit `0.0%` si c'est `0%`, pas `unavailable`, et que ce soit partout le cas ».

---

## Issue #8 — `/health` ET `/dapr` n'affichent qu'un seul composant DAPR (`state.redis`)

### Symptôme
Sur **deux pages distinctes** alimentées par la même métadata DAPR du sidecar `eventstore-admin` :

**`/health`** : la card « DAPR Components » affiche `1/1` et la liste contient uniquement `state.redis`.

**`/dapr`** (confirmé en session 2026-05-07) :
- Stat cards : Components = `1`, **Subscriptions = `1`** (sic), HTTP endpoints = `0`
- Filtre Category : seules les options `All Categories` et `statestore` sont peuplées (la catégorie `PubSub` existe dans le filtre mais ne renvoie aucune ligne).
- Filtre Category = `PubSub` → empty state.
- Filtre Category = `All` → uniquement `statestore / state.redis / v1 / Healthy`.

Le projet utilise au minimum :
- **State store** : `state.redis` ✅ visible
- **Pub/Sub** : Redis pubsub (cf. `CLAUDE.md` : « DAPR State store, **pub/sub**, and config abstracted via DAPR sidecars ») ❌ absent
- Eventuellement un configstore si configuré dans Aspire ❌ absent

### Incohérence Subscriptions vs Components
`Subscriptions = 1` mais `Components` ne contient aucun pubsub. Une subscription DAPR **requiert** un component pubsub — il est donc impossible que le sidecar voie 1 subscription mais 0 component pubsub. Deux explications possibles :
1. Le compteur `Subscriptions` est lu d'une source différente (configuration statique / fichier YAML en cache) que la liste `Components` (metadata live du sidecar).
2. Le sidecar **a bien** le component pubsub chargé, mais la query qui peuple la liste UI le filtre out (filtre par type, par scope, ou par autorisation).

### Cause racine confirmée par session 2026-05-07
**Le scoping `pubsub.yaml` est bien la cause.** Confirmé par observation croisée des trois pages :

| Page | Source query | Pubsub component visible ? | Active subscriptions |
|---|---|---|---|
| `/health` | sidecar `eventstore-admin` | ❌ non | n/a |
| `/dapr` | sidecar `eventstore-admin` | ❌ non | `1` (suspect — voir ci-dessous) |
| `/dapr/pubsub` | sidecar **`eventstore`** (mention explicite « EventStore server sidecar ») | ✅ oui (`pubsub` healthy, `pubsub.redis` v1) | `0` |

Le sidecar `eventstore` a accès au component pubsub (il est scopé), donc `/dapr/pubsub` voit l'inventaire correctement. Le sidecar `eventstore-admin` n'a pas accès, donc `/health` et `/dapr` ne voient pas le component.

**Bug additionnel découvert** : `/dapr` annonce `Subscriptions = 1` mais `/dapr/pubsub` annonce `Active Subscriptions = 0`. Les deux pages devraient s'accorder.
- Si `/dapr` lit une config statique (YAML / appsettings) et `/dapr/pubsub` une metadata live du sidecar, c'est un piège UX classique : la valeur statique peut prétendre qu'il y a 1 subscription configurée alors qu'aucune n'est effectivement active. Préciser dans l'UI quelle est la nature de chaque compteur (configured vs active), ou unifier la source.
- Soit l'un des deux est faux. Sans audit du code, l'hypothèse la plus simple est que `/dapr` somme 1 entrée présente dans la config DAPR (ex: subscriber app-id listé en `subscriptionScopes`) alors qu'aucune app subscriber n'est réellement running.

### Hypothèse principale (inchangée)
Le sidecar de `eventstore-admin` n'a pas accès au component `pubsub` parce que `eventstore-admin` n'est pas listé dans le champ `scopes` de `src/Hexalith.EventStore.AppHost/DaprComponents/pubsub.yaml` (lignes 145-152) :

```yaml
scopes:
  - eventstore
  - eventstore-test-subscriber
  - example-subscriber
  - ops-monitor
# eventstore-admin n'est PAS listé
```

Le scoping component-level (Layer 1) interdit alors au sidecar `eventstore-admin` de charger le component pubsub, ce qui explique son absence de la metadata renvoyée.

Hypothèses alternatives à éliminer si la première ne tient pas :
- `DaprHealthQueryService.GetSystemHealthAsync` filtre par `ComponentType == "state"` côté serveur (à vérifier dans `src/Hexalith.EventStore.Admin.Server/Services/DaprHealthQueryService.cs`).
- `AdminDaprController` (la source de `/dapr`) applique un filtre similaire dans `DaprMetadataQueryService` ou équivalent.

### Fix attendu
1. **Décider l'intention** : un dashboard d'observability doit-il avoir besoin du droit de **publish** pour pouvoir simplement **lister** un component ? Non, normalement c'est de la metadata read-only.
   - Option A : ajouter `eventstore-admin` au champ `scopes` de `pubsub.yaml` (read-only via metadata API, pas de publish/subscribe scoping ajouté). Vérifier que cela ne crée pas un risque sécurité (DAPR scope ≠ permissions, le scope autorise l'attachement du component).
   - Option B : exposer une nouvelle source de metadata « inventory » côté EventStore (qui voit tous les components) que l'Admin consomme via service-to-service invoke, sans toucher au sidecar Admin.
2. **Audit code** côté Admin Server : si après le fix scope, la liste reste tronquée, vérifier qu'aucun filtre `ComponentType == "state"` ou équivalent ne traîne dans `DaprHealthQueryService` / `DaprMetadataQueryService`.
3. **Cohérence des deux pages** : `/health` et `/dapr` doivent afficher exactement le même inventaire. Une seule source partagée derrière, pas deux requêtes parallèles qui peuvent diverger.
4. **Cohérence Subscriptions vs Components** : si `Subscriptions = N` alors **les N components pubsub correspondants doivent être listés** dans Components. Sinon afficher un avertissement explicite (« 1 subscription detected but pubsub component metadata is unavailable — check sidecar scoping »).

### Sévérité
⚠️ **Observability** : un opérateur qui regarde `/health` ou `/dapr` pour diagnostiquer un incident pubsub ne verra rien — la composante n'est même pas listée. La page de monitoring ment par omission.

---

## Étapes du guide validées sans issue (§3.6)

| Étape | Statut |
|---|---|
| 1 — Ouvrir `/health`, 4 stat cards visibles | ✅ |
| 2 — Total Events = 19 | ✅ |
| 3 — DAPR components liste affichée | ⚠️ Partiel (Issue #8 — composants manquants) |
| 4 — Bouton **Refresh** | ✅ (rechargement OK ; l'erreur console `content.js Extension context invalidated` est une extension navigateur, pas l'app) |
| 5 — Liens externes Traces / Metrics / Logs | ✅ |
| 6 — Stopper Redis + Refresh | ❌ Issue #6 — page ne charge plus |
| 7 — Relancer Redis + Refresh | ✅ retour nominal |

---

## Issue #9 — `/health/dead-letters` : action button reste en chargement infini après erreur

### Symptôme
Sur `/health/dead-letters`, après avoir coché une ligne et cliqué sur l'un des trois boutons d'action (`Retry Selected` / `Skip Selected` / `Archive Selected`) :
1. Une popup de confirmation apparaît avec un bouton d'action.
2. Le clic sur ce bouton de confirmation déclenche bien l'appel backend.
3. Le backend renvoie une erreur (HTTP non-200), affichée en toast / message UI : `Failed to retry entries: Unable to retry dead-letter entries.` (idem pour Skip / Archive).
4. **Mais** le bouton de la popup reste **en état chargement infini** (spinner qui tourne) au lieu de revenir à un état idle ou afficher l'erreur dans la popup.
5. L'opérateur doit fermer manuellement la popup pour recommencer.

### Reproduction
1. Seeder des entrées DLQ via `scripts/seed-dead-letters.ps1`.
2. Refresh `/health/dead-letters`, cocher une ligne.
3. Cliquer **Retry Selected** → popup apparaît → cliquer le bouton de confirmation.
4. Toast d'erreur s'affiche, **mais** le spinner du bouton popup ne s'arrête pas.

### Trace IDs (sessions 2026-05-07)
- Retry : `f877ff74cd141497e6848ade047a58bb`
- Skip : `b7c8f6acb762fd578e59dd92ae51219b`
- Archive : `1a90d6faa0b2c7d0b95df1ad29edae7e`

### Cause racine probable
Côté UI (`DeadLetters.razor` ou la popup component associée), la branche d'erreur du `try { await Action() } catch { ... }` ne réinitialise pas l'état `IsBusy` / `_isOperating` du bouton de la popup. Le spinner est probablement bindé sur cet état, qui ne repasse à `false` que sur le chemin de succès.

### Note sur le message d'erreur
Le message `Unable to retry dead-letter entries.` est trop générique. Un opérateur qui voit ce toast n'a aucun moyen de comprendre :
- Est-ce un problème d'auth ?
- Le message a-t-il déjà été retried ailleurs ?
- Le pubsub topic est-il accessible ?

Ces erreurs serveur surviennent aussi dans des scénarios réels (rate-limit, topic offline, message expiré). L'UX devrait au minimum surfacer :
- Le code HTTP / catégorie d'erreur retourné par le backend.
- Le ou les `messageId` qui ont échoué (si plusieurs sélectionnés).
- Le trace ID, **cliquable** pour ouvrir la trace dans l'observability backend.

### Fix attendu
1. **Spinner reset (priorité haute)** : la popup d'action doit gérer `try/catch/finally` et toujours réinitialiser l'état `IsBusy = false` dans le `finally`. Idem pour fermer la popup automatiquement après affichage de l'erreur, OU laisser l'opérateur la fermer mais avec le bouton revenu à l'idle.
2. **Détail d'erreur** : afficher le code HTTP, la catégorie d'échec et le trace ID dans la popup elle-même (pas seulement un toast). Idéalement le trace ID est un lien vers l'observability backend.
3. **Audit** : appliquer le même pattern à toutes les popups d'action Operator (Retry/Skip/Archive ici, mais aussi Snapshot/Compaction/Backups si même bug).

### Sévérité
⚠️ **UX** : l'action elle-même n'a pas marché (échec backend prévisible dans les tests, mais aussi possible en prod), mais l'UI donne une impression de "ça tourne encore" qui pousse à recliquer ou à attendre indéfiniment.

---

## Issue #10 — `/dapr/actors` : compteurs d'actors actifs incohérents et Inspect échoue sur un actor pourtant actif

### Symptôme

**A. Compteurs d'instances actives cassés (Total Active = 1 au lieu de ≥ 5)**

Sur `/dapr/actors`, la stat card « Total Active Actors » affiche `1` et le tableau « Actor Types » affiche :

| Actor Type | Active count UI | Réalité (clés Redis) |
|---|---|---|
| `AggregateActor` | `N/A` | **2 actifs** (`tenant-a:counter:counter-1`, `system:global-administrators:global-administrators`) |
| `ProjectionActor` | `N/A` | **2 actifs** (`counter:tenant-a:counter-1`, `global-administrators:system:global-administrators`) |
| `ETagActor` | `1` | **1 actif** (`counter:tenant-a`) |

Seul `ETagActor` renvoie un compteur, les deux autres affichent `N/A`. Le total agrégé `1` reflète uniquement l'ETag, alors qu'il y a au minimum 5 instances actives toutes types confondus.

**B. Inspect d'un actor actif réel renvoie « not found »**

Sélection : `AggregateActor` + Actor ID = `tenant-a:counter:counter-1` (la valeur exacte du seed canonical, présente dans Redis sous les clés `eventstore||AggregateActor||tenant-a:counter:counter-1||...`).

Cliquer **Inspect** :
- Bandeau : `Actor instance not found - The actor may be inactive or the ID may be incorrect.`
- Panneau « Actor State » : `Total State Size: 0.0 B`, toutes les key families (`{actorId}:metadata`, `{actorId}:events:{N}`, `{actorId}:snapshot`, `idempotency:{causationId}`, `{actorId}:pipeline:{correlationId}`, `drain:{correlationId}`, `pending_command_count`) renvoient `0.0 B / No data`.

Pourtant l'actor est manifestement actif : 18 clés `eventstore||AggregateActor||tenant-a:counter:counter-1||tenant-a:counter:counter-1:events:N` (N = 1..18) plus la clé `:metadata` sont présentes dans Redis.

**C. Empty state graceful sur un actor inexistant** (étape 5) — comportement OK : sur Actor ID = `fake-999`, même message « not found » sans crash. Bonne nouvelle : c'est juste que le « not found » est faussement déclenché aussi sur les vrais actors.

### Reproduction
1. Démarrage à froid + seed canonical `tenant-a/counter/counter-1` (Pattern 2).
2. Vérifier dans Redis : `docker exec dapr_redis redis-cli KEYS 'eventstore||AggregateActor||tenant-a:counter:counter-1||*'` retourne ≥ 19 clés (18 events + metadata).
3. Ouvrir `/dapr/actors`.
4. **Bug A** : noter Total Active Actors = `1`, AggregateActor = `N/A`, ProjectionActor = `N/A`.
5. **Bug B** : sélectionner `AggregateActor`, saisir `tenant-a:counter:counter-1`, cliquer **Inspect**.
6. Observer : « Actor instance not found ».

### Cause racine probable

DAPR n'expose pas d'API publique d'introspection de la table d'actors actifs (le placement est consistent-hash, le mapping est interne au cluster). La page mentionne ça : « Placement: DAPR consistent-hashing (not queryable via API) ».

L'inspector tente donc probablement de **deviner** l'état via des reads spéculatifs sur le state-store. Deux problèmes possibles :

1. **Format de clé incorrect** : DAPR Redis store nomme les clés actor `<app-id>||<actor-type>||<actor-id>||<state-key>`. Si le service backend (`AdminDaprController` ou son query service) utilise un autre format (ex: `<actor-type>:<actor-id>`, sans le double-pipe), tous les reads renvoient null → « not found » + 0 octet.
2. **App-id différent** : les actors AggregateActor/ProjectionActor/ETagActor sont enregistrés sur le sidecar `eventstore`, pas `eventstore-admin`. Si le query service interroge SON propre state-store (préfixe `eventstore-admin||...`), il ne trouvera évidemment rien. Il faut interroger l'inventaire avec l'app-id du **publisher** (`eventstore`).

Le fait que `ETagActor` affiche bien `1` est intéressant : peut-être que ce type d'actor a un compteur exposé différemment (ex: index admin maintenu par projection, comme `admin:stream-activity:all`), tandis qu'AggregateActor/ProjectionActor n'en ont pas → d'où le `N/A`.

### Code concerné (à auditer)
- `src/Hexalith.EventStore.Admin.Server/Controllers/AdminDaprController.cs` (endpoint d'introspection actor)
- Le service derrière (probablement `IDaprActorIntrospectionService` ou similaire)
- `src/Hexalith.EventStore.Admin.UI/Pages/DaprActors.razor` ou équivalent

À comparer au pattern qui fonctionne sur `admin:stream-activity:all` : c'est une projection admin alimentée à l'écriture des events. Probablement la même approche est nécessaire ici (admin maintient un index `admin:active-actors:all` peuplé par hooks).

### Fix attendu

Choisir une stratégie cohérente :

1. **Option pragmatique (recommandée)** : ajouter un index admin `admin:active-actors:{actor-type}` maintenu par la pipeline (à la création/désactivation d'un actor, écrire/supprimer dans cet index). Le service Admin lit ces index. C'est le pattern déjà utilisé pour l'activité de streams.
2. **Option DAPR-API** : si le sidecar expose un endpoint pour lister les actors registered par type/app, l'utiliser. Mais comme le mentionne la page elle-même, le placement n'est pas queryable.
3. **Option fallback honnête** : si on ne peut pas avoir le compteur exact, afficher `unavailable` (cohérent avec le pattern de `SystemHealthMetricStatus.Unavailable`) au lieu de `N/A` ambigu, et désactiver le bouton Inspect avec un tooltip expliquant la limitation.

Pour le bug B (Inspect échoue), corriger la clé de lookup côté backend pour qu'elle utilise bien le format `<app-id>||<actor-type>||<actor-id>||...` avec l'app-id du **propriétaire** des actors (pas de l'admin).

### Sévérité
🛑 **Bloquant pour le diagnostic actor** : la principale valeur de `/dapr/actors` est de permettre d'inspecter l'état d'un actor en cours d'incident. Si tous les actors actifs apparaissent comme « not found », la page est inutilisable pour son cas d'usage principal.

⚠️ **Honnêteté du dashboard** : `Total Active Actors = 1` quand la réalité est `≥ 5` est un mensonge silencieux du dashboard. Cohérent avec la philosophie d'Issue #2 (préférer `unavailable` à un faux 0) : ici il faut préférer `unavailable` à un compteur partiel trompeur.

---

## Issue #11 — `/projections` : page entière non-fonctionnelle, le populateur d'index n'existe pas

### Symptôme
Sur `/projections`, après seed canonical `tenant-a/counter/counter-1` :
- Total Projections = `0`
- Running = `0`
- Unhealthy = `0`
- Max Lag = `—`
- Empty state « No projections found »

Le dropdown Tenant fonctionne (`system`, `tenant-a` listés ✅ — Issue #1 non régressée). Aucune erreur console. Mais la page n'a **jamais aucune donnée à afficher**.

### Investigation
1. **Lecture côté admin** : `src/Hexalith.EventStore.Admin.Server/Services/DaprProjectionQueryService.cs:56-67` lit la clé state-store `admin:projections:{tenantId ?? "all"}` :
   ```csharp
   string indexKey = $"admin:projections:{tenantId ?? "all"}";
   List<ProjectionStatus>? result = await _daprClient
       .GetStateAsync<List<ProjectionStatus>>(_options.StateStoreName, indexKey, ...)
   if (result is null) {
       _logger.LogWarning("Admin index '{IndexKey}' not found. Index population requires admin projection setup.", indexKey);
       return [];
   }
   ```
   Le code lui-même prévient : « *Index population requires admin projection setup* ».

2. **Vérif Redis** : `docker exec dapr_redis redis-cli KEYS 'admin:projections:*'` → **aucune clé**. L'index n'existe pas.

3. **Recherche du populateur** dans tout `src/` :
   ```
   grep -r "admin:projections" src/
   ```
   Renvoie **uniquement deux readers** :
   - `DaprProjectionQueryService.cs:56` (lecture pour `/projections`)
   - `DaprConsistencyCommandService.cs:381,402` (lecture pour `/consistency`)
   
   **Aucun writer.** Il n'existe nulle part dans le code de mécanisme qui peuple `admin:projections:*` quand une projection se registre, avance, ou échoue.

4. **Sample Counter** : aucune projection nommée n'est enregistrée. `CounterStatusResult` est un DTO de query-side, pas une projection enregistrée comme service. Donc même si le populateur existait, il n'y aurait rien à indexer pour le sample actuel.

### Conclusion
La page `/projections` est **non-fonctionnelle par construction** dans le build actuel :
- Le **populateur d'index** n'existe pas — il faudrait soit un projection-side hook qui écrit sur enregistrement/avancement/échec, soit un service admin qui scrute périodiquement la config DAPR ou une autre source de vérité.
- Le **sample Counter n'enregistre pas de projection nommée** — donc même avec le populateur, le manuel test sur le seed canonique ne montrerait rien (à moins de poser un sample plus riche).

L'attente du `admin-ui-manual-test-guide.md` §3.13 (« Au minimum la projection du sample (CounterStatus) avec status `Running` ») est donc **incorrecte** — elle suppose à la fois que le populateur existe et que le sample enregistre une projection, deux choses qui ne sont pas vraies aujourd'hui.

### À noter : le `ProjectionActor` n'est PAS la même chose
On voit en Redis `eventstore||ProjectionActor||counter:tenant-a:counter-1||projection-state` et `eventstore||ProjectionActor||global-administrators:system:global-administrators||projection-state`. Ces ProjectionActor sont les **caches de queries** (`CachingProjectionActor`, voir commit `b7d1e24a` du 2026-05-06 sur l'isolation per-user du cache). **Ce ne sont pas des projections nommées au sens admin** — ce sont des caches de query côté serveur. Ils n'apparaissent pas dans `/projections` parce que ce n'est pas leur rôle.

Ne pas confondre :
- `ProjectionActor` (interne, infrastructure de cache de queries) → invisible côté admin, c'est attendu.
- **Named projections** (concept produit, projections enregistrées par les domain services pour matérialiser des read-models) → devrait apparaître dans `/projections`, mais le mécanisme est incomplet.

### Fix attendu (par ordre de priorité)
1. **Décider la source de vérité** :
   - Option A : registry statique (les domain services déclarent leurs projections au démarrage via une config DAPR / RegisterProjection appelée pendant le boot, et un populateur initialise l'index `admin:projections:*` au boot du eventstore).
   - Option B : registry dynamique (chaque ProjectionHandler est instrumenté pour écrire ses checkpoints + status dans l'index admin à chaque advancement, comme `admin:stream-activity:all` est mis à jour à chaque event).
   - Option C : pas de registry, le service admin scrute périodiquement DAPR / les domain services et reconstruit l'index. Coûteux mais zéro intrusion dans le domain.
2. **Implémenter le populateur** une fois l'option choisie. Sans cela, `/projections` ne pourra jamais afficher autre chose que l'empty state.
3. **Ajouter au sample Counter** une projection nommée (ex: `CounterTotalsProjection` qui matérialise un read-model par tenant) pour que le seed canonique exerce le code populateur dans le manual test.
4. **Mettre à jour `admin-ui-manual-test-guide.md` §3.13** pour refléter la réalité du sample actuel (jusqu'à ce que la projection nommée soit ajoutée). Aujourd'hui le résultat attendu correct est : « empty state honnête `No projections found` ».

### Sévérité
🛑 **Feature incomplete** : la page `/projections` est l'une des fonctionnalités annoncées de l'Admin UI (cf. NavMenu) et son backend a été câblé end-to-end (controller, service, DTO, UI), **mais le data flow n'est jamais alimenté**. Tant qu'un populateur n'est pas livré, `/projections` est un placeholder.

⚠️ **Impact transverse sur `/consistency`** : `DaprConsistencyCommandService.cs:381-402` lit aussi `admin:projections:*` pour valider les positions de projection. Avec un index vide, les checks de consistency sur les projections ne fonctionnent pas non plus — à investiguer côté §3.21.

---

## Issue #12 — `/types` (TypeCatalog) : page entière non-fonctionnelle, populateur d'index manquant (même pattern qu'#11)

### Symptôme
Sur `/types` après seed canonical `tenant-a/counter/counter-1` :
- Event Types = `0` (« 0 domains »)
- Command Types = `0` (« 0 domains »)
- Aggregate Types = `0` (« 0 with projections »)
- Onglets Events / Commands / Aggregates : tous vides (« No event types found / No event types registered »)
- Dropdown **Domain** : ne contient que « All Domains » — aucun domain peuplé
- Aucune erreur console

Pourtant le sample Counter expose au moins 4 events (`CounterIncremented`, `CounterDecremented`, `CounterReset`, `CounterClosed`), 4 commands (`IncrementCounter`, `DecrementCounter`, `ResetCounter`, `CloseCounter`), et 1 aggregate (`CounterAggregate`). Tous ces types sont en mémoire au runtime et la convention fluente les a forcément discoverés (sinon le seed n'aurait pas fonctionné).

### Investigation
1. **Lecture côté admin** : `src/Hexalith.EventStore.Admin.Server/Services/DaprTypeCatalogService.cs:43-66` lit trois clés state-store :
   - `admin:type-catalog:events:{domain ?? "all"}`
   - `admin:type-catalog:commands:{domain ?? "all"}`
   - `admin:type-catalog:aggregates:{domain ?? "all"}`
   
   Avec le même message de log que pour `admin:projections` : « *Admin index '{IndexKey}' not found. Index population requires admin projection setup.* »

2. **Promesse de classe (ligne 14)** : « *Indexes are populated by the event publication pipeline.* » — non implémenté.

3. **Vérif Redis** : `docker exec dapr_redis redis-cli KEYS 'admin:type-catalog:*'` → **aucune clé**.

4. **Recherche du populateur** dans `src/` :
   ```
   grep -r "admin:type-catalog" src/
   ```
   Renvoie **uniquement le reader** (`DaprTypeCatalogService.cs`). **Aucun writer.**

### Comparaison avec ce qui marche : `admin:stream-activity:all`
La clé `admin:stream-activity:all` **est** peuplée correctement (visible avec event count, last activity, etc. — preuve : on l'a inspectée pour valider le format hash `data` + `version`). Cela démontre que le pattern « index admin alimenté par le pipeline d'écriture » fonctionne quand il est implémenté. Trois indexes admin sont incomplets sur le même pattern :

| Index | Reader | Writer | État |
|---|---|---|---|
| `admin:stream-activity:all` | OK | OK | ✅ Fonctionne |
| `admin:dead-letters:*` | OK | ⚠️ Le publisher écrit sur le pubsub, mais qui lit le pubsub pour peupler l'index ? À auditer | Partiel |
| `admin:projections:*` | OK | ❌ Aucun writer | Cassé (Issue #11) |
| `admin:type-catalog:*` | OK | ❌ Aucun writer | Cassé (Issue #12, présente issue) |

### Particularité du type-catalog : devrait être trivial
Contrairement à `admin:projections` (data dynamique), les types sont **statiques** (définis au build time). Le populateur le plus simple est :
1. Au boot du service `eventstore`, enumerer via reflection / Fluent Convention tous les types `IEventPayload`, `ICommandPayload`, `IAggregateState` chargés.
2. Écrire trois clés `admin:type-catalog:events:all`, `:commands:all`, `:aggregates:all` une seule fois.
3. Optionnel : par domain, écrire `admin:type-catalog:events:counter`, etc.

Pas de hook continu nécessaire. C'est du « write-once at boot ».

### Fix attendu
1. Implémenter le populateur de catalog au boot du service `eventstore` (ou `eventstore-admin`, à décider). Le placement le plus naturel est côté `eventstore` parce que c'est lui qui charge les domain assemblies via `EventStoreDomainActivation`.
2. Si possible, peupler aussi par domain (`:counter`, `:greeting`, etc.) pour que le filtre Domain de la UI marche.
3. Mettre à jour `admin-ui-manual-test-guide.md` §3.14 — pour l'instant le résultat attendu correct est l'empty state + dropdown Domain vide.

### Lien avec Issue #11
Issues #11 et #12 sont structurellement identiques : deux indexes admin câblés en lecture/UI mais sans aucun writer. Possible **cluster fix** : implémenter les deux populateurs ensemble (et auditer aussi `admin:dead-letters:*` dans la foulée pour vérifier que le populateur DLQ existe bien).

### Sévérité
🛑 **Feature incomplete** : la page `/types` est une fonctionnalité phare de l'Admin UI (introspection des types, schémas, versions). Sans populateur, elle reste un placeholder vide.

---

## Issue #13 — Pas de mécanisme UI pour basculer entre rôles `ReadOnly` / `Operator` / `Admin` en dev

### Symptôme
L'opérateur ne peut **pas changer son rôle** depuis l'Admin UI pendant la session. Le manual test guide §0 dit pourtant :

> **Compte de test** : démarre par défaut en rôle `ReadOnly`. Pour les actions qui exigent `Operator` ou `Admin`, utilise **le toggle de rôle dans l'Admin UI (header)** ou le claim dans la config.

Or aucun toggle visible/découvrable n'existe dans le header. La session 2026-05-07 tourne avec `EnableKeycloak=false` (symmetric key JWT, cf. evidence ST11 du sprint-status). Dans ce mode :
- Le user reçoit un JWT généré par le service avec un set de claims fixe.
- Il n'y a pas de UI Keycloak où switch d'utilisateur.
- Aucun bouton dans le header de l'Admin UI ne permet de simuler un autre rôle.

### Pages affectées (impact transverse)
Les sections du manual test guide qui dépendent du toggle de rôle ne peuvent pas être validées sans intervention infra :

- §3.7 `/health/dead-letters` — étapes 18-19 (boutons Retry/Skip/Archive grisés en ReadOnly)
- §3.16 `/tenants` — étapes 10-11 (Create/Disable/Enable cachés en ReadOnly)
- §3.18 `/snapshots` — Add Policy / Create Snapshot / Delete Policy (Operator)
- §3.19 `/compaction` — Trigger Compaction (Operator)
- §3.20 `/backups` — Create Backup / Export / Import (Admin)
- §3.21 `/consistency` — Run Check (Operator)
- §3.22 `/settings` — visibilité conditionnelle du lien NavMenu (Admin only)

Soit **7 sections complètes** dont les guards d'auth ne peuvent pas être validés visuellement.

### Cause racine
Trois hypothèses, à éliminer dans cet ordre :
1. **Le toggle de rôle n'a jamais été implémenté dans l'Admin UI** — le guide promet une fonctionnalité qui n'existe pas.
2. **Le toggle existe mais est conditionné à un environnement spécifique** (mode dev avec Keycloak ON, ou variable d'env non documentée) que la session courante n'a pas activé.
3. **Le toggle existe mais est mal placé / non découvrable** (caché dans un menu, ou nécessite un raccourci clavier non documenté).

### Fix attendu
1. **Audit** : trouver dans `src/Hexalith.EventStore.Admin.UI/Layout/MainLayout.razor` ou `Header.razor` si un dev-only role-switcher existe. Vérifier les conditions de rendu (claim, env var, feature flag).
2. **Si le toggle n'existe pas** : implémenter un simple dropdown dev-only (visible uniquement si `ASPNETCORE_ENVIRONMENT=Development`) qui injecte temporairement un claim `eventstore:role` dans le `ClaimsPrincipal` côté UI/middleware. Pas de production, juste pour faciliter les tests manuels.
3. **Si le toggle existe** : le rendre découvrable (tooltip explicite, position visible dans le header) et le documenter dans le manual test guide §0.
4. **Documentation** : préciser dans le manual test guide §0 le mode d'emploi exact (URL, etc.) et indiquer une alternative (envoyer une requête à `/api/v1/admin/whoami` en CURL avec un JWT custom-forgé) pour les sections où le toggle n'est pas disponible.

### Workaround court terme (sans fix)
Pour valider les guards d'auth des 7 sections affectées sans toggle UI, l'opérateur peut :
1. Forger un JWT avec un rôle différent en utilisant la signing key dev (`DevOnlySigningKey-AtLeast32Chars!` dans `appsettings.Development.json`).
2. Le passer en cookie ou Authorization header via DevTools.
3. Recharger la page.

C'est lourd pour des tests manuels rapides — d'où la nécessité d'un toggle UI.

### Sévérité
⚠️ **Testabilité** : les guards d'auth sont une partie critique de la sécurité de l'Admin UI. Les laisser non-testables manuellement augmente le risque qu'une régression passe inaperçue. Pas un bug fonctionnel direct, mais un trou de processus QA significatif.

---

## Récapitulatif

| # | Sévérité | Page(s) | Description courte |
|---|---|---|---|
| 6 | 🛑 Bloquant | `/health` | Page ne charge plus quand Redis est down (au lieu d'afficher state.redis Unhealthy) |
| 7 | ⚠️ UX | `/`, `/health` | Affichage métriques incohérent (`0,0`/`0,0%` sur `/health` vs `unavailable` sur `/`) |
| 8 | ⚠️ Observability | `/health`, `/dapr`, `/dapr/pubsub`, `/dapr/health-history` | (a) Pubsub absent de `/health`, `/dapr`, `/dapr/health-history` car `eventstore-admin` non scopé sur `pubsub.yaml` (cause racine confirmée par `/dapr/pubsub` qui interroge le sidecar `eventstore` et voit bien le component). Sur `/dapr/health-history` la heatmap n'affiche qu'une ligne `statestore` au lieu de 2+. (b) `/dapr` annonce Subscriptions=1 mais `/dapr/pubsub` annonce Active=0 — incohérence UX, sources à unifier. |
| 9 | ⚠️ UX | `/health/dead-letters` | Spinner du bouton de la popup d'action reste infini après erreur backend (Retry/Skip/Archive) ; message d'erreur trop générique |
| 10 | 🛑 Diagnostic | `/dapr/actors` | Total Active Actors = 1 (réalité ≥ 5) ; Inspect d'un AggregateActor pourtant actif renvoie « not found » → page inutilisable pour son cas d'usage |
| 11 | 🛑 Feature incomplete | `/projections` (impact `/consistency`) | Index admin `admin:projections:*` jamais peuplé (aucun writer en code), sample Counter n'enregistre pas de projection nommée. Page non-fonctionnelle par construction. |
| 12 | 🛑 Feature incomplete | `/types` | Index admin `admin:type-catalog:*` jamais peuplé (aucun writer en code). Même pattern qu'#11. Devrait être trivial à fixer (write-once au boot via reflection). |
| 13 | ⚠️ Testabilité | UI globale (impact 7 pages) | Pas de toggle de rôle visible dans le header en mode `EnableKeycloak=false`. Les guards d'auth ReadOnly/Operator/Admin sont non-testables manuellement → 7 sections du guide bloquées sur leurs étapes auth. |
| 14 | 🛑 Feature incomplete | `/storage` | Indexes admin `admin:storage-overview:*`, `admin:storage-hot-streams:*`, `admin:storage-stream-count:*` jamais peuplés (aucun writer en code). Page affiche `0/0/0/N/A` malgré 19 events seedés. Même cluster qu'#11/#12. |
| 15 | 🛑 Feature incomplete | `/snapshots`, `/compaction`, `/backups` | Endpoints upstream EventStore manquants : `PUT/DELETE api/v1/admin/storage/snapshot-policy`, `POST api/v1/admin/storage/snapshot`, `POST api/v1/admin/storage/compact`, et **toute la famille `api/v1/admin/backups/*`** (trigger, validate, restore, export-stream, import-stream). L'Admin Server forwarde vers le néant, retourne « Admin service unavailable » sur toutes les actions. Traces 2026-05-07 : `bb37ea57...` (Add Policy), `5d3ec854...` (Trigger Compaction), `687c75a9...` (Create Backup). Export Stream → 404 sur les deux formats (JSON, CloudEvents). Indexes admin associés (`admin:storage-snapshot-policies:*`, `admin:storage-compaction-jobs:*`, `admin:backup-jobs:*`) aussi non peuplés. |
| 16 | ⚠️ UX | `/consistency` | Stat cards affichent la valeur principale correctement (Total=1, Last Check=39s ago, Anomalies=20) mais le subtitle (texte aria/visible secondaire) montre encore les **valeurs initiales stales** (« Total Checks: 0 », « Last Check: Never », « Total Anomalies: 0 »). Probable binding Razor sur une copie de modèle non rafraîchie. À fixer avant d'être visible des screen readers / non-voyants. |
| 17 | 🟡 Investigation | `/consistency` | Run Check sur `tenant-a/counter` complète en 0s mais détecte **20 anomalies** (job `01KR1FCH`). Vu qu'on a 18 events + 1 system + 0 projection enregistrée, c'est probablement des **faux positifs** liés à l'index `admin:projections:*` vide (cf. Issue #11 — `DaprConsistencyCommandService.cs:381-402` lit cet index pour valider les positions de projection). Erreur secondaire reportée avec trace `f6bfe22f...`. À investiguer une fois Issue #11 résolue. |
| 18 | 💡 UX / clarification | `/tenants` | Aucun bouton « Delete » visible côté UI ni endpoint `DELETE` côté `AdminTenantsController` (vérifié par grep). Probablement intentionnel (modèle event-sourcing → on disable, on ne delete pas), mais l'UX laisse penser que c'est une feature manquante. Soit ajouter un Delete (avec garde-fous d'audit), soit afficher un texte d'aide « Tenants cannot be deleted by design — disable them to suspend access ». |
