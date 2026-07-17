# Learning Guide: Kubernetes

> Core concepts first, then how this project's chart uses them. For the actual `helm install` workflow, see [`docs/HELM_GUIDE.md`](../HELM_GUIDE.md). For the chart layer on top of these concepts, see [`Helm.md`](Helm.md).

---

## 1. What is Kubernetes

Docker Compose (see [`Docker_Compose.md`](Docker_Compose.md)) orchestrates containers on **one host**. Kubernetes (K8s) orchestrates containers across a **cluster** of many hosts (nodes), and adds things a single-host tool doesn't need: scheduling containers onto whichever node has capacity, restarting failed containers automatically, rolling out new versions without downtime, and scaling a service to N replicas behind a stable network address.

The core model is **declarative desired state**: you describe *what* should exist (e.g. "3 replicas of the backend image, listening on port 8080"), submit it to the cluster's API server, and a set of control loops continuously reconcile actual state toward that description — restarting a crashed Pod, rescheduling one off a dead node, etc. You don't imperatively say "start this container here"; you say "this is what should be running" and K8s makes it true, repeatedly, forever.

## 2. Core objects

| Object | What it is |
|--------|-----------|
| **Pod** | The smallest deployable unit — one or more containers that share a network namespace and are always scheduled together. Usually you don't create Pods directly; a higher-level controller (Deployment, StatefulSet) creates and manages them for you. |
| **Deployment** | Manages a set of identical, stateless Pod replicas. Handles rolling updates (replace old Pods with new ones gradually) and self-healing (replace a Pod that dies). The right choice for anything where any replica is interchangeable. |
| **StatefulSet** | Like a Deployment, but for workloads that need a stable identity and/or stable storage per replica — each Pod gets a predictable name and can be bound to its own PersistentVolumeClaim that follows it across rescheduling. The right choice for a database. |
| **Service** | A stable network endpoint (a virtual IP + DNS name) in front of a changing set of Pods. Pods come and go (rolling updates, crashes) and get new IPs each time; a Service is what lets other Pods address "the backend" without tracking individual Pod IPs. |
| **PersistentVolumeClaim (PVC)** | A request for durable storage that outlives any single Pod. A Pod mounts a PVC the way a Compose service mounts a named volume — the data survives the Pod being deleted and recreated. |
| **Ingress** | Routes external HTTP(S) traffic into the cluster to the right Service, based on hostname/path. The cluster-external front door — roughly analogous to publishing a port in Compose, but for HTTP routing rules across many Services from one entry point. |

## 3. How to use — general

```bash
kubectl apply -f manifest.yaml       # create/update objects to match the file's desired state
kubectl get pods                     # list Pods and their status
kubectl get pods -w                  # watch status changes live
kubectl describe pod <name>          # full detail + recent events — first stop when a Pod won't start
kubectl logs <pod-name>              # container stdout/stderr
kubectl logs -f <pod-name>           # follow logs live
kubectl delete pod <name>            # delete a Pod — its controller (Deployment/StatefulSet) recreates it
```

Two health-check concepts worth knowing before reading any Deployment manifest:
- **Readiness probe** — "is this Pod ready to receive traffic?" A Service only routes to Pods currently passing readiness.
- **Liveness probe** — "is this Pod still working?" A Pod that fails liveness gets restarted by the kubelet.

These are K8s's version of Compose's `healthcheck` — but where Compose's `depends_on: service_healthy` gates *startup order between services*, raw Kubernetes has no native equivalent gate between one Deployment and another. Charts have to build that ordering themselves (see the emitter's init-container pattern in [`Helm.md`](Helm.md) §5).

## 4. How this project uses Kubernetes

The `helm/iiot-fleet-app/templates/` directory renders the following object kinds (confirmed by reading the actual template files in this repo):

| Manifest | Kind | Role |
|----------|------|------|
| `templates/db/statefulset.yaml` | `StatefulSet` | Runs Postgres (stock `postgres:16-alpine`, no custom image) with a stable identity and durable storage — a plain `Deployment` would risk two Postgres Pods racing for the same data directory during a rollout, or losing data on rescheduling without a bound PVC. |
| `templates/db/service.yaml` | `Service` | Stable DNS name (`db`) other Pods use to reach Postgres, regardless of which node the StatefulSet's Pod currently runs on. |
| `templates/backend/deployment.yaml` | `Deployment` | Stateless ASP.NET Core API — any replica can serve any request, so it's a plain rolling-update Deployment, not a StatefulSet. |
| `templates/backend/service.yaml` | `Service` | Stable address (`backend:8080`) for the frontend and emitter to call. |
| `templates/frontend/deployment.yaml` | `Deployment` | Stateless Next.js app — same reasoning as backend. |
| `templates/frontend/service.yaml` | `Service` | Stable address for the Ingress (or `kubectl port-forward`) to reach the frontend. |
| `templates/emitter/deployment.yaml` | `Deployment` | Stateless Python telemetry simulator; uses an init-container gate (see [`Helm.md`](Helm.md)) to approximate Compose's `depends_on: service_healthy` against `backend`. |
| `app-configmap.yaml` | `ConfigMap` | Non-secret configuration shared across Pods. |
| `db-secret.yaml`, `backend-secret.yaml` | `Secret` | Passwords/connection strings — placeholder defaults in `values.yaml`, meant to be overridden at install time, never committed as real credentials. |
| `ingress.yaml` | `Ingress` | Opt-in external HTTP entry point into the cluster (disabled by default in `values.yaml`). |

The single most important concept this chart demonstrates: **why the database is a `StatefulSet`+PVC while everything else is a plain `Deployment`.** Backend/frontend/emitter Pods are stateless and disposable — kill one, K8s starts a fresh one, nothing is lost. The database Pod is not disposable in the same way: it needs the *same* storage every time it (re)starts, which is exactly what a `StatefulSet`'s per-replica PVC binding guarantees and a `Deployment`'s Pods do not.

For the actual install/upgrade commands against this chart, see [`docs/HELM_GUIDE.md`](../HELM_GUIDE.md).

**See also:** [`Docker_Compose.md`](Docker_Compose.md) · [`Helm.md`](Helm.md)
