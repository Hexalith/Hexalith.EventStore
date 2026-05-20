# Admin UI Manual-Test Follow-Up Cluster - Runbook differe avec entrees

Date de preparation: 2026-05-20

Ce fichier contient les tests retires du run courant parce qu'ils demandent des
entrees specifiques, une fixture de donnees, ou une operation a lancer plus
tard. Le but est d'eviter de marquer un echec quand la page est vide par manque
de donnees de test.

## 0. Pre-requis commun

1. Aspire doit etre lance en mode dev sans Keycloak.
2. Redis doit etre propre si tu veux un run reproductible.
3. Le seed Counter doit exister:
   - tenant: `tenant-a`
   - domain: `counter`
   - aggregate id: `counter-1`
   - stream: `tenant-a/counter/counter-1`
   - environ `18` evenements
4. Dans l'Admin UI, utiliser le role indique par chaque section.

Commandes de verification utiles:

```powershell
aspire ps --non-interactive
docker exec dapr_redis redis-cli DBSIZE
docker exec dapr_redis redis-cli KEYS "admin:*"
```

## 1. Issue 9 - Dead-letter actions

Statut actuel: aucune entree dead-letter disponible dans le run courant.

Role requis: `Operator`.

Page:

```text
/health/dead-letters
```

Entree necessaire:

| Champ | Valeur attendue |
| --- | --- |
| Tenant | `tenant-a` |
| Domain | `counter` |
| Message id | une entree DLQ visible dans la grille |
| Failure stage | `EventsPublished`, `PublishFailed`, `TimedOut`, ou equivalent backend |
| Action a tester | Retry, Skip, Archive |

Etapes quand une entree DLQ existe:

1. Ouvrir `/health/dead-letters`.
2. Passer le role a `Operator`.
3. Verifier qu'au moins une ligne existe.
4. Selectionner une ligne `tenant-a/counter`.
5. Cliquer `Retry`.
6. Confirmer l'action.
7. Verifier que le spinner s'arrete dans tous les cas.
8. Si succes: verifier que la ligne disparait ou change d'etat selon le
   contrat backend.
9. Si echec: verifier que le dialog reste recuperable, que la selection est
   preservee, et que l'erreur inline contient action, tenant, message id, statut
   ou operation id utile.
10. Refaire les etapes 4 a 9 avec `Skip`.
11. Refaire les etapes 4 a 9 avec `Archive`.
12. Verifier qu'aucun secret, token, stack trace brute, connection string ou
    payload non borne n'est affiche.

Verification Redis optionnelle:

```powershell
docker exec dapr_redis redis-cli KEYS "*dead*"
docker exec dapr_redis redis-cli KEYS "*Dead*"
docker exec dapr_redis redis-cli KEYS "*dlq*"
```

Si aucune entree n'existe:

- Ne pas marquer le test comme echec UI.
- Ajouter l'evidence "No dead-letter entry available".
- La story de correction doit prevoir une fixture ou un scenario reproductible
  pour creer une entree DLQ avant de valider Issue 9.

## 2. Issue 11 - Projections

Statut actuel: aucune entree projection visible dans le run courant.

Role requis: `ReadOnly` suffit pour la lecture; `Operator` seulement pour les
actions.

Page:

```text
/projections
```

Entrees necessaires:

| Champ | Valeur attendue |
| --- | --- |
| Tenant | `tenant-a` |
| Projection name | projection Counter exposee par le sample, par exemple `counter` si c'est le nom publie |
| Status | `Running`, `Paused`, `Rebuilding`, `Error`, ou statut explicite |
| Lag | valeur numerique ou `unavailable` avec statut explicite |

Etapes:

1. Verifier que le seed Counter existe.
2. Ouvrir `/projections`.
3. Ouvrir le filtre Tenant.
4. Selectionner `tenant-a`.
5. Verifier qu'une projection Counter nommee apparait si le sample expose une
   projection operationnelle.
6. Cliquer la ligne projection.
7. Verifier que le panneau detail affiche tenant, projection name, statut,
   lag, throughput, erreurs, et dernier traitement si disponibles.
8. Verifier que la page n'utilise pas des clefs techniques `ProjectionActor`
   comme nom de projection utilisateur.
9. Si aucune projection n'est visible, verifier Redis:

```powershell
docker exec dapr_redis redis-cli KEYS "admin:projections:*"
docker exec dapr_redis redis-cli KEYS "*ProjectionActor*"
```

Resultat attendu:

- Si l'index projection est peuple: la page affiche les projections reelles.
- Si l'index projection n'est pas peuple: la page affiche un etat vide honnete.
- Le run ne doit pas inventer une projection depuis les clefs d'acteur.

## 3. Issue 12 - Type Catalog

Statut actuel: aucune entree type catalog visible dans le run courant.

Role requis: `ReadOnly`.

Page:

```text
/types
```

Entrees necessaires:

| Onglet | Types attendus |
| --- | --- |
| Events | `CounterIncremented`, `CounterDecremented`, `CounterReset` |
| Commands | `IncrementCounter`, `DecrementCounter`, `ResetCounter` |
| Aggregates | `CounterAggregate` |

Etapes:

1. Verifier que le service sample est `Running` / `Healthy` dans Aspire.
2. Verifier que le seed Counter existe.
3. Ouvrir `/types`.
4. Ouvrir l'onglet `Events`.
5. Chercher `Counter`.
6. Verifier les trois events Counter.
7. Ouvrir l'onglet `Commands`.
8. Chercher `Counter`.
9. Verifier les trois commands Counter.
10. Ouvrir l'onglet `Aggregates`.
11. Chercher `Counter`.
12. Verifier `CounterAggregate`.
13. Tester les liens profonds si presents:
    - `/types?tab=events`
    - `/types?tab=commands`
    - `/types?tab=aggregates`
14. Si rien n'apparait, verifier Redis:

```powershell
docker exec dapr_redis redis-cli KEYS "admin:type-catalog:*"
```

Resultat attendu:

- Le catalogue est peuple depuis la metadata de domaine ou affiche un empty
  state explicite si la metadata n'est pas disponible.
- Un type manquant avec metadata disponible doit devenir une correction
  produit, pas une variation de test manuel.

## 4. Issue 15 - Snapshots, compaction et backups

Statut actuel: a refaire plus tard avec entrees et roles dedies.

Pages:

```text
/snapshots
/compaction
/backups
```

Roles:

| Page | Role minimum |
| --- | --- |
| `/snapshots` | `Operator` |
| `/compaction` | `Operator` |
| `/backups` | `Admin` pour restore/import; `Operator` ou `Admin` selon guards visibles pour backup/export |

Donnees necessaires:

| Champ | Valeur |
| --- | --- |
| Tenant | `tenant-a` |
| Domain | `counter` |
| Aggregate id | `counter-1` |
| Stream | `tenant-a/counter/counter-1` |

### Snapshots

1. Passer en role `Operator`.
2. Ouvrir `/snapshots`.
3. Creer ou modifier une policy:
   - tenant: `tenant-a`
   - domain: `counter`
   - aggregate type: `CounterAggregate`
   - threshold: valeur basse de test, par exemple `10`, si le champ existe.
4. Verifier que l'action retourne un `AdminOperationResult` structure.
5. Lancer un snapshot manuel pour `tenant-a/counter/counter-1` si l'action est
   visible.
6. Verifier que l'operation affiche succes, differe, bloque ou backend non
   supporte de maniere explicite.
7. Supprimer la policy de test si l'action existe.

### Compaction

1. Passer en role `Operator`.
2. Ouvrir `/compaction`.
3. Declencher une compaction avec:
   - tenant: `tenant-a`
   - domain: `counter`
   - aggregate id: `counter-1` si le formulaire le demande.
4. Verifier qu'aucune compaction destructive n'est presentee comme succes si le
   backend ne la supporte pas.
5. Verifier que le job affiche un statut explicite: `Deferred`, `Blocked`,
   `UnsupportedBackend`, `Running`, `Completed`, ou equivalent.

### Backups

1. Passer en role `Admin`.
2. Ouvrir `/backups`.
3. Creer un backup:
   - tenant: `tenant-a`
   - include snapshots: valeur par defaut.
4. Verifier que le job apparait dans la grille.
5. Si le job passe `Completed`, lancer `Validate`.
6. Tester `Export Stream` avec:
   - tenant: `tenant-a`
   - domain: `counter`
   - aggregate id: `counter-1`.
7. Ne tester `Restore` ou `Import Stream` que si le produit affiche clairement
   le risque et demande une confirmation.

Verification Redis optionnelle:

```powershell
docker exec dapr_redis redis-cli KEYS "admin:backup-jobs:*"
docker exec dapr_redis redis-cli KEYS "admin:storage-snapshot-policies:*"
docker exec dapr_redis redis-cli KEYS "admin:storage-compaction-jobs:*"
```

## 5. Issue 16 - Consistency subtitles stales

Role requis: `Operator`.

Page:

```text
/consistency
```

Etapes:

1. Verifier que le seed Counter existe.
2. Passer en role `Operator`.
3. Ouvrir `/consistency`.
4. Renseigner ou selectionner:
   - tenant: `tenant-a`
   - domain: `counter`
5. Lancer `Run Check`.
6. Attendre la fin de l'operation.
7. Noter l'operation id ou check id.
8. Verifier les valeurs principales:
   - Total Checks
   - Last Check
   - Total Anomalies
9. Verifier les subtitles visibles et labels accessibles.
10. Cliquer `Refresh`.
11. Verifier que les subtitles suivent les valeurs courantes.

Resultat attendu:

- Aucun subtitle ne reste a `Total Checks: 0`, `Last Check: Never`, ou
  `Total Anomalies: 0` si la valeur principale a change.

## 6. Issue 17 - Consistency faux positifs

Role requis: `Operator`.

Dependance:

- Executer d'abord Issue 11.
- Si Issue 11 confirme que l'index projection est absent, noter ce fait avant
  de lancer le check.

Etapes:

1. Passer en role `Operator`.
2. Ouvrir `/consistency`.
3. Renseigner:
   - tenant: `tenant-a`
   - domain: `counter`
4. Lancer `Run Check`.
5. Noter l'operation id ou check id.
6. Verifier le nombre d'anomalies.
7. Ouvrir le detail des anomalies s'il en reste.
8. Classer chaque anomalie:
   - explicable par une projection absente ou stale;
   - vraie incoherence;
   - faux positif.

Resultat attendu:

- Le cluster historique de faux positifs lie a un index projection vide ne doit
  pas reapparaitre comme une erreur non expliquee.
- Toute anomalie restante doit etre explicable dans l'evidence.

## 7. Issue 18 - Tenants lifecycle / pas de Delete

Role requis: `Admin` pour creer/desactiver/reactiver.

Page:

```text
/tenants
```

Tenant de test recommande:

| Champ | Valeur |
| --- | --- |
| Tenant ID | `manual-test-tenant-a` |
| Name | `Manual Test Tenant A` |
| Description | `Tenant created for Admin UI manual lifecycle test` |

Etapes:

1. Passer en role `Admin`.
2. Ouvrir `/tenants`.
3. Creer le tenant `manual-test-tenant-a` si le formulaire existe.
4. Verifier qu'il apparait avec statut actif.
5. Verifier que la page explique le cycle de vie tenant.
6. Verifier que le bouton `Delete` n'apparait pas comme action fantome.
7. Cliquer `Disable`.
8. Confirmer l'action.
9. Verifier que le statut devient disabled/suspended ou equivalent.
10. Verifier que la copie explique que la desactivation remplace la suppression
    physique.
11. Cliquer `Enable`.
12. Confirmer l'action.
13. Verifier que le statut redevient actif.
14. Filtrer la liste sur `manual-test-tenant-a`.
15. Vider le filtre.
16. Verifier que la copie de cycle de vie reste visible la ou elle est
    attendue.

Si la creation de tenant n'est pas disponible:

- Ne pas marquer le test UI comme echec immediat.
- Noter "tenant fixture unavailable".
- Rejouer avec un tenant existant qui peut etre desactive sans risque.

Verification Redis optionnelle:

```powershell
docker exec dapr_redis redis-cli KEYS "*manual-test-tenant-a*"
docker exec dapr_redis redis-cli KEYS "*tenants*"
```

## 8. Evidence a conserver

Pour chaque scenario differe:

- URL.
- Role actif.
- Entree creee ou raison exacte de l'absence d'entree.
- Heure approximative.
- Operation id / check id / job id / trace id si disponible.
- Screenshot avant et apres action.
- Message exact si la page affiche `Deferred`, `Blocked`,
  `UnsupportedBackend`, `unavailable`, ou une erreur.

Emplacement recommande:

```text
_bmad-output/test-artifacts/admin-ui-manual-test-follow-up-deferred-YYYY-MM-DD/
```
