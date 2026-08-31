# Well identity cutover

This runbook moves the Well service from the legacy NORCE workload identity to the OSDC identity without changing public routes, the SQLite database, or record UUIDs. Do not run it until the new images and WebPages package have been published and reviewed.

## Identity map

| Concern | Previous | New |
| --- | --- | --- |
| Root namespace | `NORCE.Drilling.Well` | `OSDC.Drilling.Well` |
| WebPages package | `NORCE.Drilling.Well.WebPages` | `OSDC.Drilling.Well.WebPages` |
| Service image | `digiwells/norcedrillingwellservice` | `digiwells/osdcdrillingwellservice` |
| WebApp image | `digiwells/norcedrillingwellwebappclient` | `digiwells/osdcdrillingwellwebappclient` |
| Service Helm release | `norcedrillingwellservice` | `osdcdrillingwellservice` |
| WebApp Helm release | `norcedrillingwellwebappclient` | `osdcdrillingwellwebappclient` |
| Service Deployment/Service | `norcedrillingwellservice` | `osdcwellservice` |
| WebApp Deployment/Service | `norcedrillingwellwebappclient` | `osdcwellwebappclient` |
| PVC | `well-claim` | `well-claim` (unchanged) |
| Service path | `/Well/api` | unchanged |
| WebApp path | `/Well/webapp` | unchanged |

The releases are cut over separately. No compatibility workload, route, or second database is created.

## Order and prerequisites

Use the actual configured context names, in this order:

1. `dev.digiwells.no`
2. `app.digiwells.no`, only after dev verification succeeds
3. `awe.web.intra.norceresearch.no`, only after app verification succeeds

Before each environment:

- Confirm `digiwells/osdcdrillingwellservice:stable` and `digiwells/osdcdrillingwellwebappclient:stable` resolve to the reviewed build; record their immutable digest or `sha-*` tag.
- Capture the old releases before changing anything.
- Confirm which release owns `well-claim` and that the service mounts it at `/home` with `/home/Well.db` present.
- Schedule a write freeze for Well. The SQLite PVC must never have two writer pods.
- Verify dependent deployments use the current OSDC Field, Cluster, Rig, and Earth service-discovery names. Trajectory retains its currently deployed identity until that service is migrated separately.

The examples use `$context`, `$namespace`, and `$host`. Do not place credentials or backup data in Git.

## Capture current state and independent backups

```powershell
$context = "dev-context"
$namespace = "default"
$host = "dev.digiwells.no"
$stamp = Get-Date -Format "yyyyMMddTHHmmssZ"
$backupDirectory = Join-Path $PWD "deployment\backups\$context-$stamp"
New-Item -ItemType Directory -Path $backupDirectory -Force | Out-Null

helm --kube-context $context get values norcedrillingwellservice -n $namespace --all -o yaml |
  Out-File "$backupDirectory\old-service-values.yaml" -Encoding utf8
helm --kube-context $context get manifest norcedrillingwellservice -n $namespace |
  Out-File "$backupDirectory\old-service-manifest.yaml" -Encoding utf8
helm --kube-context $context get values norcedrillingwellwebappclient -n $namespace --all -o yaml |
  Out-File "$backupDirectory\old-webapp-values.yaml" -Encoding utf8
helm --kube-context $context get manifest norcedrillingwellwebappclient -n $namespace |
  Out-File "$backupDirectory\old-webapp-manifest.yaml" -Encoding utf8

kubectl --context $context get deployment,service,ingress,pvc,pod -n $namespace -o wide |
  Out-File "$backupDirectory\well-resources.txt" -Encoding utf8
kubectl --context $context get pvc well-claim -n $namespace -o yaml |
  Out-File "$backupDirectory\well-claim.yaml" -Encoding utf8

$oldPod = kubectl --context $context get pod -n $namespace `
  -l "app.kubernetes.io/instance=norcedrillingwellservice" `
  -o jsonpath='{.items[0].metadata.name}'
kubectl --context $context get pod $oldPod -n $namespace -o json |
  Out-File "$backupDirectory\old-service-pod.json" -Encoding utf8
kubectl --context $context exec $oldPod -n $namespace -- ls -l /home
```

Take an API-level JSON backup and a stable verification manifest:

```powershell
$wells = @(Invoke-RestMethod -Uri "https://$host/Well/api/Well/HeavyData" -Method Get)
$wells | ConvertTo-Json -Depth 100 |
  Out-File "$backupDirectory\wells.json" -Encoding utf8
$wells | ForEach-Object {
  [pscustomobject]@{
    ID = $_.MetaInfo.ID
    Name = $_.Name
    ClusterID = $_.ClusterID
    SlotID = $_.SlotID
    LastModificationDate = $_.LastModificationDate
  }
} | Sort-Object ID | Export-Csv "$backupDirectory\well-manifest.csv" -NoTypeInformation
@($wells).Count | Out-File "$backupDirectory\well-count.txt"
```

Copy the SQLite database independently. If SQLite WAL files exist, freeze writes and copy the database plus `-wal` and `-shm`, or take a storage-level snapshot according to the provisioner's procedure.

```powershell
kubectl --context $context cp "${namespace}/${oldPod}:/home/Well.db" "$backupDirectory\Well.db"
if (-not (Test-Path "$backupDirectory\Well.db")) { throw "Well.db backup was not copied." }
Get-FileHash "$backupDirectory\Well.db" |
  Out-File "$backupDirectory\Well.db.sha256.txt"
```

Record the PV name, storage class, capacity, access mode, reclaim policy, PVC UID, Helm ownership annotations, and `/home` mount before proceeding. Where supported, also take a CSI/volume snapshot or an offline copy of the underlying volume.

## Protect the existing PVC and stop the old writer

The original release owns `well-claim` but did not protect it. Upgrade that release once with the new chart under its old resource names so Helm records the keep policy. Then stop the old writer through Helm. This avoids the forbidden `deployments/scale` subresource required by `kubectl scale`, which restricted `developer-sa` credentials cannot use.

```powershell
$serviceChart = Join-Path $PWD "Service\charts\osdcdrillingwellservice"

helm upgrade norcedrillingwellservice $serviceChart `
  --kube-context $context -n $namespace --reuse-values `
  --set-string nameOverride=norcedrillingwellservice `
  --set-string fullnameOverride=norcedrillingwellservice `
  --set-string image.repository=docker.io/digiwells/osdcdrillingwellservice `
  --set-string image.tag=stable `
  --set-string strategy.type=Recreate `
  --set persistence.enabled=true `
  --set-string persistence.existingClaim= `
  --set-string persistence.claimName=well-claim

helm --kube-context $context get manifest norcedrillingwellservice -n $namespace |
  Select-String "helm.sh/resource-policy: keep"

helm upgrade norcedrillingwellservice $serviceChart `
  --kube-context $context -n $namespace --reuse-values `
  --set-string nameOverride=norcedrillingwellservice `
  --set-string fullnameOverride=norcedrillingwellservice `
  --set replicaCount=0

kubectl --context $context wait --for=delete pod `
  -l "app.kubernetes.io/instance=norcedrillingwellservice" `
  -n $namespace --timeout=180s
```

Do not continue unless no pod is writing the PVC, the PVC is still Bound, and the keep annotation appears in the saved Helm manifest.

## Start and verify the new service without ingress

Use the recorded immutable image tag in place of `stable` when practical.

```powershell
helm upgrade --install osdcdrillingwellservice $serviceChart `
  --kube-context $context -n $namespace `
  --set-string image.repository=docker.io/digiwells/osdcdrillingwellservice `
  --set-string image.tag=stable `
  --set-string persistence.existingClaim=well-claim `
  --set ingress.enabled=false

kubectl --context $context rollout status deployment/osdcwellservice -n $namespace --timeout=300s
kubectl --context $context get pod -n $namespace `
  -l "app.kubernetes.io/instance=osdcdrillingwellservice" -o wide
kubectl --context $context logs deployment/osdcwellservice -n $namespace --since=10m
kubectl --context $context exec deployment/osdcwellservice -n $namespace -- ls -l /home
kubectl --context $context port-forward service/osdcwellservice -n $namespace 5502:80
```

In a second PowerShell window, compare count, UUIDs, and representative complete records:

```powershell
$after = @(Invoke-RestMethod -Uri "http://localhost:5502/Well/api/Well/HeavyData" -Method Get)
$before = Get-Content "$backupDirectory\wells.json" -Raw | ConvertFrom-Json
if (@($after).Count -ne @($before).Count) { throw "Well count changed." }
Compare-Object `
  (@($before) | ForEach-Object { $_.MetaInfo.ID } | Sort-Object) `
  (@($after) | ForEach-Object { $_.MetaInfo.ID } | Sort-Object) |
  ForEach-Object { throw "Well UUID set changed: $_" }

$sampleIds = @($before | Select-Object -First 3 | ForEach-Object { $_.MetaInfo.ID })
foreach ($id in $sampleIds) {
  Invoke-RestMethod -Uri "http://localhost:5502/Well/api/Well/$id" -Method Get |
    ConvertTo-Json -Depth 100
}
```

Stop the port-forward with Ctrl+C. Verify the MCP endpoint and Swagger schema while the internal service is isolated.

## Transfer PVC ownership and switch ingress

Only after data verification, remove the stopped old release. The keep annotation must preserve `well-claim`.

```powershell
helm uninstall norcedrillingwellservice --kube-context $context -n $namespace --wait
kubectl --context $context get pvc well-claim -n $namespace
```

Transfer Helm ownership metadata to the new service release, then let its chart adopt the stable claim name. This is metadata-only; do not delete or recreate the PVC.

```powershell
kubectl --context $context annotate pvc well-claim -n $namespace `
  meta.helm.sh/release-name=osdcdrillingwellservice `
  meta.helm.sh/release-namespace=$namespace --overwrite
kubectl --context $context label pvc well-claim -n $namespace `
  app.kubernetes.io/managed-by=Helm --overwrite

helm upgrade osdcdrillingwellservice $serviceChart `
  --kube-context $context -n $namespace --reuse-values `
  --set-string persistence.existingClaim= `
  --set-string persistence.claimName=well-claim `
  --set ingress.enabled=true
```

Confirm the PVC UID and PV name are unchanged, the service rollout is healthy, and the external API returns the same records.

## Cut over the WebApp separately

```powershell
$webChart = Join-Path $PWD "WebApp\charts\osdcdrillingwellwebappclient"
helm upgrade --install osdcdrillingwellwebappclient $webChart `
  --kube-context $context -n $namespace `
  --set-string image.repository=docker.io/digiwells/osdcdrillingwellwebappclient `
  --set-string image.tag=stable `
  --set ingress.enabled=false
kubectl --context $context rollout status deployment/osdcwellwebappclient -n $namespace --timeout=300s

helm uninstall norcedrillingwellwebappclient --kube-context $context -n $namespace --wait
helm upgrade osdcdrillingwellwebappclient $webChart `
  --kube-context $context -n $namespace --reuse-values --set ingress.enabled=true
```

Verify direct and hosted page routes, especially Well, edit, survey-runs, trajectories, Field, Cluster, Rig, projection definitions, geodetic datum, and statistics pages.

## Final verification

- `https://<host>/Well/api/Well/HeavyData`
- `https://<host>/Well/api/swagger`
- `https://<host>/Well/api/mcp`
- `https://<host>/Well/webapp/Well`
- Record count, sorted UUID set, cluster/slot references, timestamps, and at least three representative detailed records match the backup.
- `well-claim` has the original UID/PV and new Helm ownership metadata.
- Only the two new OSDC releases and workloads remain; no legacy ingress, service, deployment, or pod remains.

Complete all dev checks and observe it before repeating the procedure for app, then awe.

## Rollback

Before uninstalling the old releases, uninstall the new releases and restore the old release replica count through Helm:

```powershell
helm uninstall osdcdrillingwellwebappclient --kube-context $context -n $namespace --wait
helm uninstall osdcdrillingwellservice --kube-context $context -n $namespace --wait
helm upgrade norcedrillingwellservice $serviceChart `
  --kube-context $context -n $namespace --reuse-values `
  --set-string nameOverride=norcedrillingwellservice `
  --set-string fullnameOverride=norcedrillingwellservice `
  --set replicaCount=1 --set ingress.enabled=true
```

After the old release has been removed, restore its Helm ownership metadata on `well-claim`, reinstall it from the captured values/manifest with the old names, and keep the new writer stopped. If the database itself is damaged, restore only after preserving the failed volume and use the independent `Well.db`/volume snapshot plus `wells.json` as recovery sources. Never run old and new service pods against `well-claim` simultaneously.
