# Learning Guide: Helm

> Concepts-first guide. For core Kubernetes objects Helm charts render, see [`K8s.md`](K8s.md). For the actual `helm install`/`upgrade` commands against this repo's chart, see [`docs/HELM_GUIDE.md`](../HELM_GUIDE.md) — this doc doesn't duplicate those steps.

---

## 1. What is Helm

Helm is the de facto package manager for Kubernetes. A Kubernetes app is usually a dozen-plus raw YAML manifests (Deployments, Services, Secrets, ConfigMaps, …) — Helm packages all of them into a single unit called a **chart**, templated so the same chart can be installed with different configuration (a different image tag, replica count, or environment) without hand-editing YAML each time.

Key vocabulary:
- **Chart** — the package: a directory of templates plus metadata (`Chart.yaml`) and default configuration (`values.yaml`).
- **Release** — a specific installation of a chart into a cluster, with a name (e.g. `helm install my-release ./iiot-fleet-app`). You can install the same chart multiple times under different release names.
- **Values** — the configuration passed into a chart's templates, either from `values.yaml`'s defaults or overridden at install time (`--set key=value` or `-f custom-values.yaml`).

## 2. Why use Helm

Compare the alternative: `kubectl apply -f` against a folder of static YAML files. That works until you need the *same* set of manifests with a different image tag for staging vs. production, or a different replica count, or a database password that must never be hardcoded in a file that gets committed to git. Helm's templating (`values.yaml` + Go template syntax in the manifests) solves exactly that — one chart, many configurations, no copy-pasted YAML forks.

The other big win is **release management**: `helm upgrade` diffs the new desired state against what's installed and applies just the delta; `helm rollback` reverts to a previous release revision if an upgrade goes wrong. Raw `kubectl apply` has no built-in concept of "the previous version of this whole app" to roll back to.

## 3. Chart anatomy

```
iiot-fleet-app/
├── Chart.yaml          # chart name, version, description
├── values.yaml         # default configuration — the knobs every template reads from
└── templates/
    ├── _helpers.tpl     # reusable named template snippets (e.g. standard label sets)
    ├── *-deployment.yaml, *-statefulset.yaml, *-service.yaml, ...
    └── NOTES.txt        # printed to the user after a successful install/upgrade
```

- **`Chart.yaml`** — chart identity and version. Bumped when the chart's structure changes (not the same as the application's own version in `frontend/package.json`/`CHANGELOG.md`).
- **`values.yaml`** — every configurable knob a template references via `{{ .Values.something }}`. This is what you override with `--set` or `-f` at install time — never hardcode environment-specific values directly in a template.
- **`templates/`** — the actual Kubernetes manifests, as Go templates. `helm template`/`helm install --dry-run` renders these against `values.yaml` so you can inspect the final YAML before it ever touches the cluster.
- **`_helpers.tpl`** — named template snippets (`{{ define "iiot-fleet-app.labels" }}`) so common blocks like standard labels aren't copy-pasted into every manifest.

Common commands:

```bash
helm install <release-name> ./helm/iiot-fleet-app     # first install
helm upgrade <release-name> ./helm/iiot-fleet-app     # apply changes to an existing release
helm rollback <release-name> <revision>                # revert to a previous release revision
helm uninstall <release-name>                          # remove the release entirely
helm template ./helm/iiot-fleet-app                    # render manifests locally without installing (dry run)
```

## 4. How this project uses Helm

This repo's chart lives at `helm/iiot-fleet-app/` and renders one release covering the whole stack:

- **`db-statefulset.yaml` + a PVC** — Postgres needs stable identity and storage that survives Pod rescheduling, so it's a `StatefulSet`, not a `Deployment` (see [`K8s.md`](K8s.md) for why that distinction matters).
- **`backend-deployment.yaml` + `backend-service.yaml`**, **`frontend-deployment.yaml` + `frontend-service.yaml`** — stateless services, so plain `Deployment`+`Service` pairs, mirroring the `backend`/`frontend` split in `docker-compose.yml`.
- **`emitter-deployment.yaml`** — the Python telemetry simulator, with an **init-container gate**: raw Kubernetes has no built-in `depends_on: service_healthy` equivalent between two Deployments (unlike Compose), so the chart approximates it by giving the emitter Pod an init container that blocks until the backend Service responds, only then letting the main emitter container start.
- **`backend-secret.yaml` / `db-secret.yaml`** — passwords sourced from `values.yaml` as placeholder defaults, meant to be overridden per-environment at install time (`--set` or a values override file) — never real credentials committed to this chart.
- **`ingress.yaml`** — opt-in (disabled by default in `values.yaml`); enable it to expose the frontend outside the cluster through a real hostname instead of `kubectl port-forward`.

For the actual install/verify workflow against a real cluster (including the Docker-Desktop-Kubernetes path this chart was last verified against), see [`docs/HELM_GUIDE.md`](../HELM_GUIDE.md).

**See also:** [`Docker_Compose.md`](Docker_Compose.md) · [`K8s.md`](K8s.md)
