# Guide de test manuel — Hexalith.EventStore Admin UI

Ce guide te permet de valider chaque page de l'Admin UI après un démarrage à froid (`flush Redis → build → aspire run`). Suis les sections dans l'ordre : la première partie peuple le store avec des données réelles, puis chaque page s'appuie sur ces données pour les vérifications.

---

## 0. Prérequis & démarrage

1. **Flush Redis** (état propre) :
   ```powershell
   docker exec -it dapr_redis redis-cli FLUSHALL
   ```
2. **Build & lancement Aspire** :
   ```powershell
   dotnet build Hexalith.EventStore.slnx --configuration Release
   dotnet run --project src/Hexalith.EventStore.AppHost
   ```
3. **URLs principales** (à confirmer depuis le dashboard Aspire si les ports sont remappés) :
   - Admin UI : `https://localhost:8093`
   - Sample Blazor UI : voir resource `sample-blazor-ui` dans le dashboard
   - EventStore API : `https://localhost:7141`
4. **Compte de test** : démarre par défaut en rôle `ReadOnly`. Pour les actions qui exigent `Operator` ou `Admin`, utilise le toggle de rôle dans l'Admin UI (header) ou le claim dans la config.

---

## 1. Données de référence à utiliser dans tous les filtres

| Champ | Valeur attendue |
|---|---|
| Tenant ID | `tenant-a` |
| Domain | `counter` |
| Aggregate Type | `CounterAggregate` |
| Aggregate ID | `counter-1` |
| Command Types | `IncrementCounter`, `DecrementCounter`, `ResetCounter`, `CloseCounter` |
| Event Types | `CounterIncremented`, `CounterDecremented`, `CounterReset`, `CounterClosed` |
| Query Type | `get-counter-status` |

---

## 2. Peupler l'EventStore (étape obligatoire avant les tests UI)

Ouvre la **Sample Blazor UI** et exécute cette séquence — elle crée un stream avec une vingtaine d'évènements répartis sur plusieurs catégories :

| Action UI | Bouton à cliquer | Effet attendu |
|---|---|---|
| 1. Incrémenter ×5 | `Increment` ×5 | Counter = 5, 5 évènements `CounterIncremented` |
| 2. Décrémenter ×2 | `Decrement` ×2 | Counter = 3, 2 évènements `CounterDecremented` |
| 3. Reset | `Reset` ×1 | Counter = 0, 1 évènement `CounterReset` |
| 4. Incrémenter ×10 | `Increment` ×10 | Counter = 10 |
| 5. Vérifier query | `GetCounterStatus` | Renvoie `Value=10`, sans erreur |

Total : 1 stream `tenant-a/counter/counter-1`, 18 évènements environ, 18 commandes acceptées.

---

## 3. Tests page par page

> 🛑 **BUG TRANSVERSE BLOQUANT — tous les dropdowns Tenant sont cassés** (vérifié manuellement sur `/commands`, `/events`, `/streams`).
>
> **Symptôme :** chaque dropdown Tenant n'affiche que l'item « All Tenants », même quand des évènements/commands/streams existent sous `tenant-a`. Aucune valeur n'est sélectionnable, donc le filtrage par tenant est **impossible** depuis la UI.
>
> **Cause racine :** les pages alimentent la dropdown via `GET /api/v1/admin/tenants` (service Tenants), pas via la liste des tenants observés dans les évènements. Comme `tenant-a` n'est pas enregistré explicitement dans le service Tenants, l'API renvoie une liste vide → dropdown vide.
>
> **Pages affectées (à corriger en bloc) :**
> - `/commands` (champ `Tenant`)
> - `/events` (champ `Tenant`)
> - `/streams` (champ `Tenant`)
> - `/projections` (filtre `Tenant`) — à confirmer
> - Toute autre page utilisant `AdminStreamApiClient.GetTenantsAsync()` ou `AdminTenantApiClient.GetTenantsAsync()`
>
> **Workaround pendant les tests :** laisser le dropdown sur « All Tenants ». Utiliser les autres filtres (status, type, search) et la query string (`?tenant=tenant-a` sur `/streams`) pour valider que le filtrage côté serveur fonctionne.
>
> **Décision à prendre côté implémentation (à tracker comme une seule issue, fix groupé) :**
> 1. Soit fallback automatique sur les tenants observés dans les évènements (`SELECT DISTINCT TenantId FROM events`).
> 2. Soit auto-enregistrer le tenant lors de la première écriture d'évènement.
> 3. Soit imposer l'enregistrement préalable et l'expliciter dans la UI (message d'aide quand la dropdown est vide).

### 3.1 `/` — Home (Index)

> ⚠️ **Bug connu** : `DaprHealthQueryService.GetSystemHealthAsync()` (ligne 130-136) renvoie `TotalEventCount = 0`, `EventsPerSecond = 0`, `ErrorPercentage = 0` codés en dur. Tant que ce stub n'est pas implémenté, la page Home affiche **toujours** l'EmptyState « 0 commands processed », même avec des évènements présents. L'ActivityChart n'est donc jamais rendu (gate `TotalEventCount > 0` dans `Index.razor:50`). À tracker comme issue séparée.

| Étape | Action | Résultat attendu (état actuel du code) |
|---|---|---|
| 1 | Ouvrir `/` | 4 stat cards toutes à 0 (`Active Streams: 0`, `Total Events: 0`, `Events/sec: 0,0/s`, `Error Rate: 0,00%`) — **comportement buggé attendu** |
| 2 | EmptyState rendu | « EventStore Admin is running. 0 commands processed. Send your first command via the Admin API. » |
| 3 | Cliquer sur la card "Active Streams" | Navigue vers `/streams?status=active` (le clic est câblé même si le compteur est faux) |
| 4 | Cliquer sur la card "Error Rate" | Navigue vers `/health` |
| 5 | Stopper l'AppHost et recharger | `IssueBanner` visible avec bouton **Retry** (test du chemin d'erreur) |
| 6 | Issue à reporter | « Home dashboard hardcodes TotalEventCount/EventsPerSecond/ErrorPercentage to 0 — DaprHealthQueryService.cs:130-136 stub jamais finalisé (cf. spec 15-7) » |

---

### 3.2 `/commands`

| Étape | Action | Résultat attendu |
|---|---|---|
| 1 | Ouvrir `/commands` | Stat cards : Total Commands ≈ 18, Success Rate ≈ 100 %, Failed = 0, In-Flight = 0 |
| 2 | Filtrer Status = `Completed` | Toutes les lignes visibles |
| 3 | Filtrer Status = `Failed` | Empty state |
| 4 | ~~Tenant dropdown = `tenant-a`~~ | ❌ **Bug** : dropdown vide (n'affiche que « All Tenants »). Voir note transverse en haut de §3. Laisse sur « All Tenants ». |
| 5 | Command Type = `Increment` | 15 lignes (5 + 10) |
| 6 | Command Type = `xxx` (inexistant) | Empty state propre |
| 7 | Pagination Next/Previous | Boutons grisés si <= 1 page |

---

### 3.3 `/events`

| Étape | Action | Résultat attendu |
|---|---|---|
| 1 | Ouvrir `/events` | Liste des évènements récents (~18) |
| 2 | ~~Tenant = `tenant-a`~~ | ❌ **Bug transverse confirmé** : dropdown vide. Voir bloc en haut de §3. |
| 3 | Event Type = `CounterIncremented` | 15 lignes |
| 4 | Event Type = `CounterReset` | 1 ligne |
| 5 | Event Type = `CounterClosed` | Empty state (non émis dans le scénario) |
| 6 | Pagination Next | Si > page size, charge la suite ; sinon désactivé |

---

### 3.4 `/streams`

| Étape | Action | Résultat attendu |
|---|---|---|
| 1 | Ouvrir `/streams` | 1 ligne : `tenant-a / counter / counter-1`, status `Active`, EventCount ≈ 18, snapshot ❌ |
| 2 | Filtrer Status = `Tombstoned` | Empty state |
| 3 | Filtrer Status = `Active` | 1 ligne |
| 4 | ~~Tenant dropdown = `tenant-a`~~, Domain = `counter` | ❌ **Bug transverse confirmé** sur Tenant. Tester Domain seul → 1 ligne. |
| 5 | Cliquer sur l'aggregate ID `counter-1` | ⚠️ **Bug** : le clic copie bien dans le presse-papiers **mais déclenche aussi** la navigation vers `/streams/tenant-a/counter/counter-1` (event bubbling vers `OnRowClick`). Comportement attendu : copie seulement. Voir Issue #3. |
| 6 | Cliquer sur la ligne (en dehors de l'ID) | Navigation vers `/streams/tenant-a/counter/counter-1` |
| 7 | Tester deep-link : `/streams?status=active&tenant=tenant-a&domain=counter` | Filtres pré-remplis |

---

### 3.5 `/streams/tenant-a/counter/counter-1` — StreamDetail

| Étape | Action | Résultat attendu |
|---|---|---|
| 1 | Charger l'URL | Header avec status, eventCount, lastActivity, snapshot=false ; breadcrumb fonctionnel |
| 2 | Timeline | ~18 évènements ordonnés par sequenceNumber croissant. Si tu vois 36 entrées (18 commands + 18 events), c'est normal — la timeline mêle commands et events. |
| 3 | Filtrer Entry type | ⚠️ **Précision** : le filtre n'est PAS un dropdown de types nommés (ex: `CounterReset`). C'est un set de **boutons** : `All / Command / Event / Query`. Cliquer `Event` → ~18 lignes (les CounterIncremented/Decremented/Reset). Cliquer `Command` → ~18 lignes (les Increment/Decrement/Reset). Cliquer `Query` → 0 dans ce scénario. |
| 3b | (limitation) Filtrer sur un type précis (ex: `CounterReset`) | ❌ **Pas possible depuis la UI**. Cf. Issue #4 (enhancement). |
| 4 | Filtrer Correlation ID = un ID existant (copie depuis une ligne) | Filtre la timeline sur les entrées partageant le même CorrelationId |
| 5 | Bouton **Blame** | Ouvre BlameViewer ; chaque champ d'état lié à un évènement source |
| 6 | Bouton **Bisect** | Ouvre BisectTool ; navigation binaire entre évènements |
| 7 | Bouton **Step Through** | Affiche l'état after-event pour chaque step |
| 8 | Bouton **Sandbox** | Ouvre CommandSandbox ; permet de soumettre un dry-run `IncrementCounter` |
| 9 | Bouton **Trace Map** | Ouvre CorrelationTraceMap |
| 10 | Bouton **Copy stream key** | Toast confirmation, presse-papiers = `tenant-a/counter/counter-1` |
| 11 | URL invalide `/streams/tenant-a/counter/inexistant` | Empty state ou message "stream not found" |

---

### 3.6 `/health`

| Étape | Action | Résultat attendu |
|---|---|---|
| 1 | Ouvrir `/health` | Stat cards : Total Events, Events/sec, Error Rate, Healthy components count |
| 2 | Liste DAPR components | Tous statut `Healthy` (sauf si tu as coupé un service exprès) |
| 3 | Bouton **Refresh** | Compteurs se rafraîchissent (timestamp change) |
| 4 | Liens externes Traces / Metrics / Logs | Ouvrent des onglets vers le dashboard Aspire / OpenTelemetry |
| 5 | Stopper Redis (`docker stop dapr_redis`) puis Refresh | Au moins un component bascule en `Unhealthy` |

---

### 3.7 `/health/dead-letters`

| Étape | Action | Résultat attendu |
|---|---|---|
| 1 | Ouvrir la page (scénario nominal) | Empty state "No dead-letter entries" |
| 2 | Pour générer du DLQ : envoie une commande malformée via API REST `POST /commands` avec un payload invalide | Apparaît dans la liste après refresh |
| 3 | Filtrer Tenant = `tenant-a` | Filtré |
| 4 | Filtrer Failure category = `Deserialization` | Filtré |
| 5 | Search = `Increment` | Filtré sur command type |
| 6 | Sélectionner une ligne (checkbox) | Boutons d'action s'activent (si rôle `Operator`) |
| 7 | Cliquer **Retry Selected** (Operator) | Toast succès, ligne disparaît ou status = retried |
| 8 | Bouton **Skip Selected** / **Archive Selected** | Idem, ligne sort de la liste |
| 9 | En `ReadOnly` | Boutons grisés / cachés |

---

### 3.8 `/dapr` (Components)

| Étape | Action | Résultat attendu |
|---|---|---|
| 1 | Ouvrir `/dapr` | Stat cards : App ID, Runtime Version, Component count > 0, Subscription count |
| 2 | Search = `redis` | Filtre sur les composants Redis (state-store + pubsub) |
| 3 | Category = `StateStore` | Affiche le state-store DAPR |
| 4 | Category = `PubSub` | Affiche le pubsub |
| 5 | Boutons header (Actor Inspector, Pub/Sub Metrics, Resiliency, Health History) | Naviguent vers les sous-pages |
| 6 | Bouton **Refresh** | Mise à jour timestamp |

---

### 3.9 `/dapr/actors`

| Étape | Action | Résultat attendu |
|---|---|---|
| 1 | Ouvrir | Stat cards (registered types, total active, inspected size). Si aucun actor n'est utilisé, "No actor types" empty state |
| 2 | Sélectionner un Actor Type (dropdown ou clic dans la grid) | Champ Actor ID activé |
| 3 | Saisir un Actor ID (ex: `counter-1`) puis **Inspect** | Affiche l'état JSON ou message "actor not found" |
| 4 | Bouton **Refresh** | Re-inspecte |

---

### 3.10 `/dapr/pubsub`

| Étape | Action | Résultat attendu |
|---|---|---|
| 1 | Ouvrir | Stat cards (pubsub components ≥ 1, active subscriptions ≥ 1, unique topics, dead-letter count) |
| 2 | Liste des subscriptions | Au moins une (commands ou events publishing) |
| 3 | Bouton **Refresh** | Mise à jour |
| 4 | Lien **Back to /dapr** | Retour à `/dapr` |

---

### 3.11 `/dapr/resiliency`

| Étape | Action | Résultat attendu |
|---|---|---|
| 1 | Ouvrir | Stat cards : retry policies, timeout policies, circuit breakers, target bindings |
| 2 | Sections Retry / Timeout / CircuitBreaker | Cards avec MaxRetries, Strategy, etc., conformes au resiliency.yaml |
| 3 | Si pas de fichier resiliency configuré | Empty state explicite, pas de crash |
| 4 | Bouton **Reload** | Re-lit le fichier |

---

### 3.12 `/dapr/health-history`

| Étape | Action | Résultat attendu |
|---|---|---|
| 1 | Ouvrir | Si activé : heatmap timeline, stat cards (component count, healthy now, status changes, avg uptime %) |
| 2 | Sélectionner Time range = `Last Hour` / `Last 24 Hours` | Heatmap se redessine |
| 3 | Cliquer une cellule | Filtre les snapshots à ce timestamp |
| 4 | Si désactivé en config | Empty state "Health history collection disabled" |

---

### 3.13 `/projections`

| Étape | Action | Résultat attendu |
|---|---|---|
| 1 | Ouvrir | Stat cards : total projections, running, unhealthy, max lag |
| 2 | Grid | Au minimum la projection du sample (CounterStatus) avec status `Running` |
| 3 | Filtrer Status = `Running` | Mêmes lignes |
| 4 | Filtrer Status = `Unhealthy` | Empty state |
| 5 | Tenant filter = `tenant-a` | Filtré |
| 6 | Cliquer sur une ligne | ProjectionDetailPanel s'ouvre à droite |
| 7 | Bouton **Refresh** | Lag/throughput mis à jour |

---

### 3.14 `/types` — TypeCatalog

| Étape | Action | Résultat attendu |
|---|---|---|
| 1 | Ouvrir | Stat cards (event types, command types, aggregate types). Counter du sample : ≥ 4 events, ≥ 4 commands, 1 aggregate |
| 2 | Onglet **Events** | Liste contient `CounterIncremented`, `CounterDecremented`, `CounterReset`, `CounterClosed` |
| 3 | Onglet **Commands** | Liste contient `IncrementCounter`, `DecrementCounter`, `ResetCounter`, `CloseCounter` |
| 4 | Onglet **Aggregates** | Liste contient `CounterAggregate` |
| 5 | Filtre Domain = `counter` | Filtré |
| 6 | Search = `Counter` | Filtré sur le nom |
| 7 | Cliquer sur une ligne | TypeDetailPanel s'ouvre avec schéma + version |
| 8 | Vérifier que cliquer sur "Aggregates" ne casse pas la navigation et garde les filtres (cf. fix `feat(admin-ui): types page redirect-back`) |

---

### 3.15 `/services`

| Étape | Action | Résultat attendu |
|---|---|---|
| 1 | Ouvrir | Empty state "0 domain services connected" + lien vers le guide |
| 2 | Cliquer le lien | Ouvre la doc externe |

> Cette page est un placeholder en l'état actuel ; le test consiste à vérifier qu'elle ne crash pas.

---

### 3.16 `/tenants`

| Étape | Action | Résultat attendu |
|---|---|---|
| 1 | Ouvrir (rôle `Admin`) | Stat cards : total ≥ 1, active ≥ 1, disabled = 0. Grid contient `tenant-a` |
| 2 | Filtrer Status = `Active` | Grid inchangée |
| 3 | Filtrer Status = `Disabled` | Empty state |
| 4 | Search = `tenant-a` | Filtré |
| 5 | Bouton **Create Tenant** → saisir `tenant-b` | Apparait dans la grid |
| 6 | Bouton **Disable** sur `tenant-b` | Status passe à `Disabled` |
| 7 | Bouton **Enable** sur `tenant-b` | Status repasse à `Active` |
| 8 | Repasser en rôle `ReadOnly` et recharger | Boutons Create/Disable/Enable cachés ou désactivés |

---

### 3.17 `/storage`

| Étape | Action | Résultat attendu |
|---|---|---|
| 1 | Ouvrir | Stat cards : total events ≈ 18, total size > 0, tenant count ≥ 1, avg growth/day > 0 |
| 2 | Grid | 1 ligne pour `tenant-a` avec event count = 18 |
| 3 | Filtrer Tenant = `tenant-a` | Grid inchangée |
| 4 | Filtrer Tenant = `xxx` | Empty state |
| 5 | Si event count > 50 000 (peu probable en sample) | Lien **Run Compaction** visible |

---

### 3.18 `/snapshots`

| Étape | Action | Résultat attendu |
|---|---|---|
| 1 | Ouvrir (initial) | Stat cards à 0, empty state |
| 2 | Bouton **Add Policy** (Operator) → Tenant=`tenant-a`, Domain=`counter`, AggregateType=`CounterAggregate`, Interval=`10` | Policy apparaît dans la grid |
| 3 | Bouton **Create Snapshot** sur la ligne | Toast "snapshot created", la stream `counter-1` aura snapshot=✅ dans `/streams` |
| 4 | Retour `/streams` | Indicator snapshot actif sur la ligne |
| 5 | Bouton **Delete** (Operator) sur la policy | Disparaît |
| 6 | En `ReadOnly` | Boutons grisés |

---

### 3.19 `/compaction`

| Étape | Action | Résultat attendu |
|---|---|---|
| 1 | Ouvrir | Stat cards à 0, jobs vides |
| 2 | Bouton **Trigger Compaction** (Operator) → Tenant=`tenant-a`, Domain=`counter` | Job apparaît avec status `Running` puis `Completed` |
| 3 | Job grid | Started, Duration, Events Compacted, Space Reclaimed renseignés |
| 4 | Cliquer une ligne en erreur (si applicable) | Détail de l'erreur visible |
| 5 | Deep-link `/compaction?tenant=tenant-a` | Pré-remplit le filtre |

---

### 3.20 `/backups`

| Étape | Action | Résultat attendu |
|---|---|---|
| 1 | Ouvrir (Admin) | Stat cards à 0 |
| 2 | Bouton **Create Backup** → Tenant=`tenant-a`, Scope=`Full` | Job apparaît, status passe à `Completed` |
| 3 | Bouton **Export Stream** → `tenant-a/counter/counter-1` | Téléchargement d'un fichier .json/.zip |
| 4 | Bouton **Import Stream** → uploader le fichier exporté | Nouveau job d'import, succès |
| 5 | Cliquer une ligne | Détail backup ID, taille, durée |
| 6 | En `Operator` ou `ReadOnly` | Boutons Create/Export/Import cachés |

---

### 3.21 `/consistency`

| Étape | Action | Résultat attendu |
|---|---|---|
| 1 | Ouvrir | Stat cards à 0 (jamais lancé) |
| 2 | Bouton **Run Check** (Operator) → Tenant=`tenant-a`, Domain=`counter` | Check apparaît, passe à `Completed`, anomalies = 0 |
| 3 | Filtrer Tenant=`tenant-a` | Filtré |
| 4 | Cliquer une ligne | Détail des anomalies (vide ici) |

---

### 3.22 `/settings`

| Étape | Action | Résultat attendu |
|---|---|---|
| 1 | Connecté en `Admin` | Le lien "Settings" apparaît dans le NavMenu |
| 2 | Ouvrir `/settings` | Empty state placeholder "Configure admin dashboard preferences" |
| 3 | Repasser en `ReadOnly` ou `Operator` | Lien Settings disparaît du NavMenu |

---

## 4. Tests transverses (à faire à la fin)

| Test | Comment | Attendu |
|---|---|---|
| Toggle thème (clair/sombre) | Header → ThemeToggle | Le thème change instantanément, persiste après reload |
| Sidebar responsive (cf. fix `feat(admin-ui)` récent) | Réduire la fenêtre < 768px | Sidebar bascule en mode collapsed, hamburger fonctionnel |
| Command Palette | Ctrl+K (ou Cmd+K) | Ouvre le palette ; taper "streams" navigue |
| Topology dynamique dans NavMenu | Tenants → créer `tenant-b` | Apparaît sous Topology après refresh |
| Comportement réseau dégradé | DevTools → throttle Slow 3G + reload | Skeletons pendant le chargement, pas de blank screen |
| Erreurs API | Stopper EventStore service via Aspire dashboard | IssueBanner sur Home + cards en empty state, pas de crash |
| 403 / Auth | Forcer rôle `ReadOnly` puis aller sur `/tenants` boutons d'action | Boutons cachés ou message "Access denied" |
| Reload "froid" | F5 sur chaque page | Pas d'erreur console, données rechargées |

---

## ST11 Operator Sign-off - 2026-05-07

- Operator: Jerome
- Environment reset: pass. Aspire stopped, Redis flushed with `docker exec dapr_redis redis-cli FLUSHALL`, then Aspire relaunched with `EnableKeycloak=false`.
- Aspire resources checked: pass. `eventstore`, `eventstore-admin`, `eventstore-admin-ui`, `sample`, `sample-blazor-ui`, `statestore`, `pubsub`, and `tenants` were `Running` / `Healthy`.
- Seeded fixture: pass. `tenant-a/counter/counter-1` seeded from Sample Blazor UI with Pattern 2: Increment x5, Decrement x2, Reset, Increment x10.
- Dashboard `/`: pass. Active Streams = 2, Total Events = 19, Events/sec = `unavailable`, Error Rate = `unavailable`, Recent Streams includes `tenant-a/counter-...` with 18 events plus one system stream with 1 event. `unavailable` is expected for metrics without a wired source.
- Tenant dropdowns: pass. `/commands`, `/events`, `/streams`, and `/projections` all expose `tenant-a`.
- Projections `/projections`: pass. Empty projection state is accepted for this story; no projections are required by the B/C/D manual smoke.
- Copy isolation `/streams`: pass. Clicking the truncated Aggregate ID copies without navigation; clicking elsewhere on the row navigates to stream detail.
- Group A replay/state-inspection surface: excluded from this story per the 2026-05-07 carve-out to `admin-ui-aggregate-state-replay-correctness`.
- Result: pass for ST11 B/C/D manual smoke.

---

## 5. Checklist finale

- [ ] Toutes les pages se chargent sans erreur console (F12)
- [ ] Aucune 500 dans le journal Aspire pendant la session
- [ ] Les filtres déterministes (`tenant-a`, `counter`) renvoient toujours le même résultat
- [ ] Les actions Operator/Admin sont bien gardées par le rôle
- [ ] Les empty states sont propres (pas de spinners infinis)
- [ ] Les deep-links (query params sur `/streams`, `/compaction`) fonctionnent
- [ ] Le toggle thème + sidebar collapse persistent au reload

> Reporte tout écart dans `_bmad-output/test-artifacts/admin-ui-manual-test-guide-results.md` pour traçabilité.
