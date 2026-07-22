# Deploying the IIoT Fleet Telemetry System with Helm

This guide covers the Kubernetes deployment path via the `helm/iiot-fleet-app` chart. For what
each service actually does (data flow, env vars, per-service behavior), see
[DOCKER_README.md](DOCKER_README.md) — this document only covers the k8s-specific delta:
StatefulSet-backed storage, Services, probes, the optional Ingress, and the Docker Compose
behaviors that don't have a direct Kubernetes equivalent.

---

## Prerequisites

- **Helm 3.x** — `helm version` (this guide assumes Helm 3; the chart uses `apiVersion: v2`, a
  Helm 3-only feature)
- **A Kubernetes cluster** — any conformant cluster works. For local testing, a
  [kind](https://kind.sigs.k8s.io/) or [minikube](https://minikube.sigs.k8s.io/) cluster is
  enough; the [worked kind example](#worked-example-kind-end-to-end) below uses kind.
- **kubectl**, configured to point at that cluster (`kubectl cluster-info` should succeed)
- **Docker**, to build the three custom images this chart deploys (`db` pulls the stock public
  `postgres:16-alpine` image — nothing to build for it)

### Three images are not on any public registry

`backend`, `frontend`, and `emitter` are built from Dockerfiles under `containers/` in this repo
(build context is each service's source dir — `backend/`, `frontend/`, `emitter/`) — none are
published anywhere. **This is the single most likely first-run failure** (`ImagePullBackOff` on
the backend/frontend/emitter pods). Build each image, then either push it to a registry your
cluster can pull from, or load it directly into a local kind/minikube cluster:

```bash
docker build -f containers/backend/Dockerfile -t iiot-fleet-backend ./backend
docker build -f containers/frontend/Dockerfile -t iiot-fleet-frontend ./frontend
docker build -f containers/emitter/Dockerfile -t iiot-fleet-emitter ./emitter
```

For a local **kind** cluster:

```bash
kind load docker-image iiot-fleet-backend iiot-fleet-frontend iiot-fleet-emitter
```

For a local **minikube** cluster, point your shell's Docker client at minikube's daemon before
building instead:

```bash
eval $(minikube docker-env)
docker build -f containers/backend/Dockerfile -t iiot-fleet-backend ./backend   # repeat for frontend, emitter
```

For any remote cluster, tag and push each image to a registry the cluster can reach, then set
`*.image.repository`/`*.image.tag` accordingly at install time (see
[values.yaml reference](#valuesyaml-reference) below).

---

## Quick start

```bash
helm install iiot-fleet-app ./helm/iiot-fleet-app \
  --set db.password=<a-real-password>
```

Watch the rollout:

```bash
kubectl get pods -l app.kubernetes.io/instance=iiot-fleet-app -w
```

You should see four workloads come up: `iiot-fleet-app-db-0` (StatefulSet pod), and one pod each
for `iiot-fleet-app-backend`, `iiot-fleet-app-frontend`, `iiot-fleet-app-emitter`. The emitter pod
stays `Init:0/1` until its `wait-for-backend` initContainer sees the backend respond — this is
expected, not a failure (see [Kubernetes has no `depends_on: service_healthy`](#kubernetes-has-no-depends_on-service_healthy) below).

`helm install` prints post-install `NOTES.txt` with the exact `docker build`/`kind load` commands
and a `kubectl port-forward` reminder — re-read it any time with:

```bash
helm get notes iiot-fleet-app
```

---

## values.yaml reference

Override any of these with `--set key=value` (dot-path, e.g. `--set backend.replicaCount=2`) or a
`-f my-values.yaml` override file passed to `helm install`/`helm upgrade`.

| Key | Default | Description |
|-----|---------|--------------|
| `db.image.repository` | `postgres` | Stock public image — no Dockerfile, no build step |
| `db.image.tag` | `16-alpine` | Image tag |
| `db.image.pullPolicy` | `IfNotPresent` | Pod image pull policy |
| `db.storage.size` | `1Gi` | Size of the PVC created per-pod by the StatefulSet's `volumeClaimTemplates` |
| `db.password` | `"changeme"` | **Placeholder — always override at install time.** Becomes `POSTGRES_PASSWORD` (via a Secret) and is also baked into the backend's assembled `ConnectionStrings__Fleet` |
| `db.port` | `5432` | PostgreSQL port, used by both the headless Service and the StatefulSet container |
| `backend.image.repository` | `iiot-fleet-backend` | Image built from `containers/backend/Dockerfile` (context `backend/`) |
| `backend.image.tag` | `latest` | Image tag |
| `backend.image.pullPolicy` | `IfNotPresent` | Pod image pull policy |
| `backend.replicaCount` | `1` | Backend Deployment replica count — safe to scale up, the backend is stateless |
| `backend.service.port` | `8080` | Backend ClusterIP Service port, and the container's Kestrel port |
| `backend.aspnetEnvironment` | `"Production"` | `ASPNETCORE_ENVIRONMENT`, via the shared ConfigMap |
| `backend.additionalFrontendOrigins` | `"http://localhost:3000"` | `ADDITIONAL_FRONTEND_ORIGINS` CORS allow-list entry, via the shared ConfigMap |
| `backend.resources` | `requests: 250m/256Mi`, `limits: 1/512Mi` | Backend container resource requests/limits |
| `frontend.image.repository` | `iiot-fleet-frontend` | Image built from `containers/frontend/Dockerfile` (context `frontend/`) |
| `frontend.image.tag` | `latest` | Image tag |
| `frontend.image.pullPolicy` | `IfNotPresent` | Pod image pull policy |
| `frontend.replicaCount` | `1` | Frontend Deployment replica count |
| `frontend.service.port` | `3000` | Frontend ClusterIP Service port |
| `frontend.resources` | `requests: 100m/128Mi`, `limits: 500m/256Mi` | Frontend container resource requests/limits |
| `emitter.image.repository` | `iiot-fleet-emitter` | Image built from `containers/emitter/Dockerfile` (context `emitter/`) |
| `emitter.image.tag` | `latest` | Image tag |
| `emitter.image.pullPolicy` | `IfNotPresent` | Pod image pull policy |
| `emitter.replicaCount` | `1` | **Fixed — not a tunable.** Each replica independently simulates the entire fleet; N replicas would multiply telemetry N-fold instead of sharding it |
| `emitter.vehicleCount` | `10000` | `VEHICLE_COUNT` — size of the simulated fleet |
| `emitter.tickIntervalSeconds` | `3` | `TICK_INTERVAL_SECONDS` — per-vehicle tick period |
| `emitter.maxConcurrency` | `300` | `MAX_CONCURRENCY` — concurrent in-flight POSTs to the backend |
| `emitter.resources` | `requests: 250m/256Mi`, `limits: 1/512Mi` | Emitter container resource requests/limits |
| `ingress.enabled` | `false` | Set `true` to create an Ingress; off by default since not every cluster has a controller installed |
| `ingress.className` | `""` | `ingressClassName`, e.g. `nginx` — leave empty to use the cluster's default IngressClass |
| `ingress.host` | `""` | **Required when `ingress.enabled=true`.** Hostname routed to the frontend (`/`) and backend (`/api`, `/swagger`, `/fleethub`) |

---

## Upgrading and uninstalling

```bash
# After changing values.yaml, a template, or the chart version:
helm upgrade iiot-fleet-app ./helm/iiot-fleet-app --set db.password=<a-real-password>

# Preview what a change would do without applying it:
helm upgrade iiot-fleet-app ./helm/iiot-fleet-app --dry-run --debug

# Remove the release (the db StatefulSet's PVC is NOT deleted automatically —
# see below):
helm uninstall iiot-fleet-app
```

`helm uninstall` does not delete PersistentVolumeClaims created by the StatefulSet's
`volumeClaimTemplates` — this is standard Kubernetes/Helm behavior, meant to prevent accidental
data loss. To fully wipe the database volume after uninstalling:

```bash
kubectl delete pvc -l app.kubernetes.io/instance=iiot-fleet-app
```

---

## Connecting to the cluster

With no Ingress enabled (the default), reach each service with `kubectl port-forward`:

```bash
# Frontend dashboard
kubectl port-forward svc/iiot-fleet-app-frontend 3000:3000
# → http://localhost:3000

# Backend API + Swagger
kubectl port-forward svc/iiot-fleet-app-backend 8080:8080
# → http://localhost:8080/swagger

# PostgreSQL (e.g. for psql or a GUI client)
kubectl port-forward svc/iiot-fleet-app-db 5432:5432
```

If you enabled the Ingress (`--set ingress.enabled=true --set ingress.host=fleet.example.local`),
point DNS (or `/etc/hosts` for local testing) at your ingress controller's external IP, then the
frontend is reachable at `http://fleet.example.local/` and the backend API at
`http://fleet.example.local/api`, `/swagger`, `/fleethub`.

### Worked example: kind end-to-end

```bash
# 1. Create a local cluster
kind create cluster --name iiot-fleet

# 2. Build the three custom images (db pulls postgres:16-alpine directly, nothing to build)
docker build -f containers/backend/Dockerfile -t iiot-fleet-backend ./backend
docker build -f containers/frontend/Dockerfile -t iiot-fleet-frontend ./frontend
docker build -f containers/emitter/Dockerfile -t iiot-fleet-emitter ./emitter

# 3. Load them into the kind cluster (kind can't pull from your local Docker
#    daemon directly — it runs its own container runtime)
kind load docker-image iiot-fleet-backend iiot-fleet-frontend iiot-fleet-emitter --name iiot-fleet

# 4. Install the chart
helm install iiot-fleet-app ./helm/iiot-fleet-app --set db.password=devpassword

# 5. Wait for pods to become ready
kubectl get pods -w

# 6. Port-forward and verify
kubectl port-forward svc/iiot-fleet-app-frontend 3000:3000 &
curl -s http://localhost:3000 | head -5

kubectl port-forward svc/iiot-fleet-app-backend 8080:8080 &
curl -s http://localhost:8080/api/vehicles/metadata | head -c 200

# 7. Tear down
helm uninstall iiot-fleet-app
kind delete cluster --name iiot-fleet
```

---

## What has no direct Docker Compose equivalent

### Kubernetes has no `depends_on: service_healthy`

`containers/docker-compose.yml` gates `backend`'s and `emitter`'s startup on the upstream service's
healthcheck passing (`depends_on: condition: service_healthy`). Kubernetes has no native
equivalent — Pods start independently regardless of other Pods' readiness.
`templates/emitter/deployment.yaml` approximates this with an `initContainer` that polls the backend Service until it responds before
the main emitter container starts. This is **not identical** to Compose's behavior: it only gates
the emitter's *initial* startup, not any ongoing dependency — if the backend later becomes
unavailable, the emitter container is not restarted the way Compose's healthcheck-driven ordering
would imply.

### Storage: `volumeClaimTemplates` vs. a single named volume

`containers/docker-compose.yml` uses one named volume (`postgres_data`) shared implicitly by whichever
container mounts it. The Helm chart uses a StatefulSet with `volumeClaimTemplates`, which is the
idiomatic Kubernetes pattern: each StatefulSet pod gets its own dynamically-provisioned
PersistentVolumeClaim, bound for the pod's lifetime and preserved across rescheduling. With a
single-replica `db` StatefulSet (this chart's default), the practical effect is similar to
Compose's single volume, but the provisioning model is different — see
[Pending PVC](#pending-pvc-no-default-storageclass) below if it doesn't bind.

### Health checks: `tcpSocket`/`httpGet` probes vs. Compose `healthcheck:`

`containers/docker-compose.yml`'s `backend` healthcheck uses a raw `/dev/tcp` bash check because the
`aspnet:8.0` base image has neither `curl` nor `wget`. The Helm chart's `templates/backend/deployment.yaml`
uses a `tcpSocket` probe on port 8080 instead — same underlying signal (is Kestrel accepting
connections), expressed as a native kubelet probe instead of a shelled-out command. The frontend's
Compose healthcheck (`wget --spider`) becomes an `httpGet` probe on `/` — the kubelet performs the
HTTP check itself, so no `wget` binary inside the container is needed either way.

---

## Troubleshooting

### `ImagePullBackOff` on backend/frontend/emitter pods

The three custom images (`backend`/`frontend`/`emitter`; `db` pulls stock `postgres:16-alpine`)
aren't on any public registry (see
[prerequisites](#three-images-are-not-on-any-public-registry)). Build them, then `kind load
docker-image` (local kind) or push to a registry your cluster can actually pull from, and set
`*.image.repository`/`*.image.tag` to match if you didn't use the default names.

### Pending PVC (no default `StorageClass`)

```bash
kubectl get pvc
kubectl describe pvc postgres-data-iiot-fleet-app-db-0
```

If the PVC stays `Pending`, the cluster has no default `StorageClass` to dynamically provision
against. `kind` and `minikube` both ship one out of the box (`standard`); on a bare cluster,
either install a `StorageClass` provisioner or set an explicit one via a values override
(`db.storage.storageClassName`, if you add that key — not present by default in this chart since
`kind`/`minikube`'s default `StorageClass` covers the common local-dev case).

### `iiot-fleet-app-emitter` stuck `Init:0/1` or `CrashLoopBackOff`

`Init:0/1` for a while after install is expected — the `wait-for-backend` initContainer is
polling until the backend Service responds (see
[Kubernetes has no `depends_on: service_healthy`](#kubernetes-has-no-depends_on-service_healthy)).
If it's stuck for more than a couple minutes:

```bash
kubectl logs -l app.kubernetes.io/component=emitter -c wait-for-backend
kubectl get pods -l app.kubernetes.io/component=backend   # is the backend pod even Running?
```

Most often this means the backend pod itself is `ImagePullBackOff` or `CrashLoopBackOff` — fix
that first.

### Ingress not routing (404, connection refused, or DNS doesn't resolve)

```bash
kubectl get ingress
kubectl get pods -n <ingress-controller-namespace>   # e.g. ingress-nginx
```

`ingress.enabled=true` alone is not sufficient — the cluster also needs an Ingress **controller**
installed (kind and minikube do not ship one by default; install
[ingress-nginx](https://kubernetes.github.io/ingress-nginx/) or similar). Confirm
`ingress.host` matches whatever hostname you're actually sending requests to (DNS or
`/etc/hosts`), and that `ingress.className` matches your controller's IngressClass if your
cluster has more than one.
