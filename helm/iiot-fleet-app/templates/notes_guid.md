# Understanding `NOTES.txt` in Helm Charts — Line by Line

`NOTES.txt` is another special file in a Helm chart's `templates/` directory — but it behaves completely differently from every other template. Let's build up the concepts and then go through it piece by piece.

## Background: what makes `NOTES.txt` special

- Every other file in `templates/` (Deployments, Services, ConfigMaps, `_helpers.tpl`, etc.) either renders into a **Kubernetes object** that gets applied to the cluster, or — like `_helpers.tpl` — defines reusable snippets consumed by those objects.
- `NOTES.txt` does **neither**. Helm recognizes this exact filename as a convention: it renders the file through the same Go templating engine (so it has access to `.Values`, `.Release`, `.Chart`, and can `include` helpers), but instead of sending the output to the Kubernetes API, Helm just **prints the rendered text to the terminal** after a successful `helm install` or `helm upgrade`.
- Nothing in `NOTES.txt` is written to disk, and it creates no cluster resource. Its only job is to hand the person running the install their next steps.

---

## The comment block at the top

```gotemplate
{{/*
NOTES.txt is a special filename Helm recognizes: it is never applied to the
cluster like the other templates in this directory. ...
*/}}
```

Same as in `_helpers.tpl`, `{{/* ... */}}` is a **Go template comment** — it's parsed and discarded during rendering. So even though this comment is long, it produces **zero visible output** in what the user sees printed to their terminal after `helm install`. It exists purely for the next developer reading the chart source, explaining:
- Why this filename is treated specially by Helm.
- That it's rendered with the same templating context as everything else, but printed rather than applied.
- Its purpose: giving the operator actionable next steps (build images, check rollout, find the URL) without needing to read through the whole chart.

---

## The opening status line

```gotemplate
IIoT Fleet Telemetry System has been installed as release "{{ .Release.Name }}" in namespace "{{ .Release.Namespace }}".
```

- Plain text mixed with two template expressions — this is normal in `NOTES.txt`; unlike YAML templates, there's no syntax to preserve, so it reads like a mail-merge.
- `{{ .Release.Name }}` — the name given at install time (`helm install <RELEASE_NAME> ...`).
- `{{ .Release.Namespace }}` — the namespace the release was installed into (defaults to whatever `-n`/`--namespace` was passed, or `default`).

**Purpose:** immediately confirms to the operator *which* release, and *where*, just succeeded — useful when someone manages many releases across many namespaces.

---

## Explaining the `db` image

```gotemplate
`db` pulls the stock public `{{ .Values.db.image.repository }}:{{ .Values.db.image.tag }}` image —
nothing to build. Before this chart's other Pods can pull images, build and make available the
three custom images they reference (none are published to a public registry):
```

- `{{ .Values.db.image.repository }}:{{ .Values.db.image.tag }}` — pulls the configured image repo and tag straight from `values.yaml`, so if someone overrides the Postgres/Mongo/etc. version via `--set db.image.tag=...`, the notes stay accurate rather than hardcoding a value that could drift out of sync.
- The surrounding prose tells the operator something they can't infer from `kubectl get pods` alone: this particular image is public and needs no local build step, but the *other* three do. This is the kind of tribal knowledge NOTES.txt exists to surface automatically instead of relying on a wiki page staying up to date.

---

## Conditionally listing build commands

```gotemplate
{{- if .Values.backend }}
  docker build -f containers/backend/Dockerfile -t {{ .Values.backend.image.repository }}:{{ .Values.backend.image.tag }} ./backend
{{- end }}
{{- if .Values.frontend }}
  docker build -f containers/frontend/Dockerfile -t {{ .Values.frontend.image.repository }}:{{ .Values.frontend.image.tag }} ./frontend
{{- end }}
{{- if .Values.emitter }}
  docker build -f containers/emitter/Dockerfile -t {{ .Values.emitter.image.repository }}:{{ .Values.emitter.image.tag }} ./emitter
{{- end }}
```

This block prints a `docker build` command for each of the three custom services — **but only if that service is actually enabled** in this install.

- `{{- if .Values.backend }} ... {{- end }}` — checks whether the `backend` key exists/is truthy under `.Values`. This is the standard Helm pattern for **optional subcomponents**: a chart might let an operator disable a service entirely (e.g. `--set frontend=null` or an empty `frontend: {}` guarded elsewhere), and the notes shouldn't tell them to build an image for something that isn't even being deployed this time.
- The `-` in `{{- if ... }}` again chomps the preceding whitespace/newline, so disabled blocks don't leave stray blank lines in the printed output.
- Inside each block: `docker build -f containers/<service>/Dockerfile -t <repo>:<tag> ./<service>` — a ready-to-copy-paste command, using the exact repository/tag values configured for *this* release, built from a per-service Dockerfile in a per-service build context directory.

**Purpose:** rather than a static "remember to build your images" reminder, this dynamically tells the operator the *exact* commands to run for *only* the services that are actually part of this install — accurate even as `values.yaml` changes over time or across environments.

---

## The `kind` (local cluster) hint

```gotemplate
For a local kind cluster, load them instead of pushing to a registry:
  kind load docker-image {{ .Values.db.image.repository }}:{{ .Values.db.image.tag }}
```

- This is a practical aside for local development: [`kind`](https://kind.sigs.k8s.io/) (Kubernetes-in-Docker) runs a cluster inside Docker containers, and its container runtime **can't see images on your host's Docker daemon** unless you explicitly load them in with `kind load docker-image`. Pushing to a real registry is unnecessary overhead for local testing, so this line saves a confused operator from a `ImagePullBackOff` error.
- Worth noting: as written, this example only shows the command for the `db` image variable — a real-world chart would likely repeat this line for the backend/frontend/emitter images too (inside their own `if` blocks) so the operator has the full set of `kind load` commands to run, not just one.

---

## Checking rollout status

```gotemplate
Check rollout status:
  kubectl get pods -n {{ .Release.Namespace }} -l app.kubernetes.io/instance={{ .Release.Name }}
```

- A ready-to-run `kubectl` command, again using `.Release.Namespace` and `.Release.Name` so it's correct for wherever this specific release landed.
- `-l app.kubernetes.io/instance={{ .Release.Name }}` — this label selector works precisely *because* of the `selectorLabels` helper convention (like the one in `_helpers.tpl`): every Pod created by this chart carries `app.kubernetes.io/instance: <release name>`, so this one command reliably lists every Pod belonging to this release and no others — a nice example of the labeling convention paying off later in the notes.

---

## Conditionally showing how to reach the frontend

```gotemplate
{{- if .Values.ingress }}
{{- if .Values.ingress.enabled }}
The frontend is reachable via the Ingress at:
  http://{{ .Values.ingress.host }}
{{- else }}
No Ingress is enabled. Reach the frontend with:
  kubectl port-forward -n {{ .Release.Namespace }} svc/{{ include "iiot-fleet-app.fullname" . }}-frontend 3000:3000
{{- end }}
{{- end }}
```

Two nested conditionals here:

- `{{- if .Values.ingress }}` — outer guard: only try to say anything about Ingress if the `ingress` key exists in `values.yaml` at all (protects against a nil-pointer-style template error if `ingress` were entirely absent from the values schema).
- `{{- if .Values.ingress.enabled }}` — inner check: is Ingress actually turned on for this install?
  - **If enabled:** prints the URL built from `{{ .Values.ingress.host }}` — the operator gets a clickable/copyable URL immediately, no need to inspect the Ingress object manually.
  - **If not enabled** (`{{- else }}`): falls back to telling the operator how to reach the frontend Service directly via `kubectl port-forward`, using `{{ include "iiot-fleet-app.fullname" . }}-frontend` to build the exact Service name — this is exactly the `fullname` helper from `_helpers.tpl` being reused here, which is why that file has to exist and stay correct: NOTES.txt depends on it too.
  - Both nested `{{- end }}` close the inner then outer `if`.

**Purpose:** this is the single most valuable pattern in the whole file — it means the *same* chart, deployed two different ways (with or without Ingress), always tells the operator the *correct* way to reach their app, rather than printing generic instructions that might not apply.

---

## The closing pointer to docs

```gotemplate
See docs/HELM_GUIDE.md in the repository for full install, configuration, and troubleshooting instructions.
```

- Plain static text, no templating — a deliberate choice to keep NOTES.txt itself short and point elsewhere for anything more involved (full configuration reference, troubleshooting steps, etc.), rather than trying to cram everything into what's meant to be a quick post-install summary.

---

## How this all fits together in practice

When someone runs:

```bash
helm install iiot-prod ./iiot-fleet-app -n telemetry
```

Helm renders every template in `templates/`, applies the Kubernetes ones to the cluster, and — because `NOTES.txt` is present — prints its rendered output straight to the terminal as the very last thing the operator sees, something like:

```
IIoT Fleet Telemetry System has been installed as release "iiot-prod" in namespace "telemetry".
`db` pulls the stock public `postgres:16` image — nothing to build. ...
  docker build -f containers/backend/Dockerfile -t myrepo/backend:1.0 ./backend
  docker build -f containers/frontend/Dockerfile -t myrepo/frontend:1.0 ./frontend
  docker build -f containers/emitter/Dockerfile -t myrepo/emitter:1.0 ./emitter
For a local kind cluster, load them instead of pushing to a registry:
  kind load docker-image postgres:16
Check rollout status:
  kubectl get pods -n telemetry -l app.kubernetes.io/instance=iiot-prod
The frontend is reachable via the Ingress at:
  http://iiot.example.com
See docs/HELM_GUIDE.md in the repository for full install, configuration, and troubleshooting instructions.
```

**Key DevOps takeaway:** `NOTES.txt` is Helm's built-in way to turn "read the docs to figure out what to do next" into "the chart tells you exactly what to do next, using the real values of *this* install." Combined with helpers like `iiot-fleet-app.fullname`, it stays accurate automatically as configuration changes — no manually-updated README can keep up as reliably.