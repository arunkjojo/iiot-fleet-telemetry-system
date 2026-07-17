# Understanding `_helpers.tpl` in Helm Charts — Line by Line

This file is a **Helm template helpers file**. It doesn't create any Kubernetes resource by itself — it defines *reusable snippets* (like functions) that other templates call. Let's go through it carefully, building up the DevOps/Helm concepts as we go.

## Background concepts you need first

- **Helm** is a package manager for Kubernetes. A "chart" is a package of YAML templates that render into real Kubernetes manifests (Deployments, Services, etc.) when you run `helm install`.
- Helm templates use the **Go templating language**. `{{ ... }}` is how you inject dynamic logic/values into YAML.
- `.Values` = things from `values.yaml` (or `--set` flags) — user-configurable.
- `.Chart` = metadata from `Chart.yaml` (name, version, etc.) — chart-defined, not user-configurable.
- `.Release` = info Helm knows about *this specific install* (release name, namespace, etc.).
- A `.tpl` file's job is only to `define` named templates (like functions) that get `include`d elsewhere. It produces no output on its own.

---

## The comment block at the top

```gotemplate
{{/*
_helpers.tpl — required, not optional. ...
*/}}
```

`{{/* ... */}}` is a **Go template comment** — it's stripped out during rendering, so it never appears in the final Kubernetes YAML. It's purely for humans reading the source.

This particular comment is a "why does this file exist" note left by whoever wrote the chart. It explains:
- Every other template folder (`db/`, `backend/`, `frontend/`, etc.) depends on the three helpers defined here.
- Helm doesn't care about *where* under `templates/` a `.tpl` file lives — it scans the whole `templates/` directory tree recursively for `*.tpl` files (and `*.yaml` files) and loads all defined templates into one shared namespace. So there's no benefit to nesting this file inside a subfolder — it would still be global, just harder to find.
- It's a warning: **don't delete this file**, because many other templates silently depend on it.

This is a great DevOps habit to notice: **leaving a "why" comment**, not just a "what" comment, so future maintainers don't accidentally break something non-obvious.

---

## Helper 1: `iiot-fleet-app.name`

```gotemplate
{{/*
Expand the name of the chart.
*/}}
{{- define "iiot-fleet-app.name" -}}
{{- .Chart.Name | trunc 63 | trimSuffix "-" }}
{{- end }}
```

Line by line:

- `{{/* Expand the name of the chart. */}}` — a doc comment describing what this helper does.
- `{{- define "iiot-fleet-app.name" -}}` — this **starts the definition of a named template** called `iiot-fleet-app.name`. Think of this like defining a function named `iiot-fleet-app.name`. Anywhere else in the chart, you can call it with `{{ include "iiot-fleet-app.name" . }}`.
  - The `-` before/after `{{` and `}}` (called **whitespace chomping**) tells Helm to trim surrounding newlines/whitespace, keeping the rendered output clean (important because YAML is whitespace-sensitive).
- `{{- .Chart.Name | trunc 63 | trimSuffix "-" }}` — the actual body of the "function":
  - `.Chart.Name` pulls the chart's name from `Chart.yaml` (e.g. `iiot-fleet-app`).
  - `| trunc 63` — pipes that string into the `trunc` function, cutting it to a maximum of 63 characters.
  - `| trimSuffix "-"` — then removes a trailing `-` if truncation happened to leave one dangling.
  - Why 63? **Kubernetes DNS-1035/1123 naming rules** limit certain resource name fields (like Service names, labels) to 63 characters. This helper future-proofs the name so it never breaks resource creation on a long release/chart name.
- `{{- end }}` — closes the `define` block.

**Purpose:** Produce a safe, short "base name" for the chart, usable as a building block in other names/labels.

---

## Helper 2: `iiot-fleet-app.fullname`

```gotemplate
{{- define "iiot-fleet-app.fullname" -}}
{{- if .Values.fullnameOverride }}
{{- .Values.fullnameOverride | trunc 63 | trimSuffix "-" }}
{{- else }}
{{- $name := default .Chart.Name .Values.nameOverride }}
{{- if contains $name .Release.Name }}
{{- .Release.Name | trunc 63 | trimSuffix "-" }}
{{- else }}
{{- printf "%s-%s" .Release.Name $name | trunc 63 | trimSuffix "-" }}
{{- end }}
{{- end }}
{{- end }}
```

This is the most important helper — it computes the name used to prefix almost every resource (Deployments, Services, ConfigMaps, etc.) so resources from different releases/installs of the same chart don't collide.

Step by step:

- `{{- define "iiot-fleet-app.fullname" -}}` — starts defining this helper/"function".
- `{{- if .Values.fullnameOverride }}` — **first priority**: if the user explicitly set `fullnameOverride` in `values.yaml` or via `--set fullnameOverride=...`, use exactly that.
  - `{{- .Values.fullnameOverride | trunc 63 | trimSuffix "-" }}` — take that override, safely truncate to 63 chars, trim a trailing dash if needed. This is the return value in this branch.
- `{{- else }}` — otherwise (no override given), compute a name automatically:
  - `{{- $name := default .Chart.Name .Values.nameOverride }}` — declare a local variable `$name`. `default A B` means "use B if it's set/non-empty, otherwise fall back to A." So: if the user set `.Values.nameOverride`, use that; otherwise use `.Chart.Name`.
  - `{{- if contains $name .Release.Name }}` — check: does the **release name** (e.g. what you typed as `helm install <RELEASE_NAME> ...`) already contain the chart name as a substring? E.g., if you run `helm install iiot-fleet-app-prod ./chart`, the release name already contains "iiot-fleet-app".
    - If true: `{{- .Release.Name | trunc 63 | trimSuffix "-" }}` — just use the release name as-is (avoids ugly duplication like `iiot-fleet-app-prod-iiot-fleet-app`).
    - `{{- else }}` — if the release name does *not* already contain the chart name:
      - `{{- printf "%s-%s" .Release.Name $name | trunc 63 | trimSuffix "-" }}` — concatenate them as `<release-name>-<chart-name>`, e.g. `prod-iiot-fleet-app`, then truncate/trim as before.
  - `{{- end }}` — closes the inner `if/else`.
- `{{- end }}` — closes the outer `if/else`.
- `{{- end }}` — closes the `define`.

**Purpose:** This is the classic Helm pattern (auto-generated by `helm create`) for computing a unique, DNS-safe, collision-avoiding base name for all resources in the release. Nearly every `metadata.name:` in the chart's templates calls `include "iiot-fleet-app.fullname" .` to get this value.

---

## Helper 3: `iiot-fleet-app.chart`

```gotemplate
{{/*
Create chart name and version as used by the chart label.
*/}}
{{- define "iiot-fleet-app.chart" -}}
{{- printf "%s-%s" .Chart.Name .Chart.Version | replace "+" "_" | trunc 63 | trimSuffix "-" }}
{{- end }}
```

- `{{- printf "%s-%s" .Chart.Name .Chart.Version ... }}` — combines chart name and chart version into one string, e.g. `iiot-fleet-app-1.2.3`.
- `| replace "+" "_"` — if the version uses **build metadata** per SemVer (e.g. `1.2.3+build.5`), the `+` character is not legal in Kubernetes label values, so it's replaced with `_`.
- `| trunc 63 | trimSuffix "-"` — same safety truncation as before.

**Purpose:** Produces a string like `iiot-fleet-app-1.2.3` used specifically for the `helm.sh/chart` label, which is a Helm convention that records which chart+version produced a resource (handy for debugging/auditing what's running in a cluster).

---

## Helper 4: `iiot-fleet-app.labels`

```gotemplate
{{/*
Common labels
*/}}
{{- define "iiot-fleet-app.labels" -}}
helm.sh/chart: {{ include "iiot-fleet-app.chart" . }}
{{ include "iiot-fleet-app.selectorLabels" . }}
{{- if .Chart.AppVersion }}
app.kubernetes.io/version: {{ .Chart.AppVersion | quote }}
{{- end }}
app.kubernetes.io/managed-by: {{ .Release.Service }}
{{- end }}
```

This one is different from the previous three — instead of returning a *string* (a name), it returns a **block of YAML label key/value lines**, meant to be pasted directly under a resource's `labels:` field.

- `helm.sh/chart: {{ include "iiot-fleet-app.chart" . }}` — calls Helper 3 above and outputs it as a label. `include` is how you call one named template from inside another (or from a real template file) and get its rendered string back. The `.` passed in is the "current context" (all the `.Values`, `.Chart`, `.Release` data) — it must be passed explicitly, since templates don't automatically inherit the caller's scope.
- `{{ include "iiot-fleet-app.selectorLabels" . }}` — inserts the output of Helper 5 (below) right here — meaning **all selector labels are automatically included inside the common labels** too. This avoids repeating those two lines twice.
- `{{- if .Chart.AppVersion }} ... {{- end }}` — **conditionally** adds an `app.kubernetes.io/version` label, only if `Chart.yaml` has an `appVersion` field set. (`AppVersion` is meant to reflect the version of the *application* being deployed, e.g. your Docker image tag, as opposed to `Chart.Version` which is the chart's own packaging version — these are conceptually different things in Helm.)
  - `| quote` — wraps the value in quotes, since version strings like `1.0` could otherwise be misinterpreted as a YAML number instead of a string.
- `app.kubernetes.io/managed-by: {{ .Release.Service }}` — a standard Kubernetes recommended label. `.Release.Service` is basically always the literal string `"Helm"` — it records that this resource is managed by Helm (as opposed to, say, kubectl or another tool), which is useful for tooling/humans inspecting the cluster later.

**Purpose:** A single reusable block of "here are all the standard labels every resource in this chart should carry" — so every Deployment, Service, ConfigMap, etc. can just do:

```yaml
metadata:
  labels:
    {{- include "iiot-fleet-app.labels" . | nindent 4 }}
```

and get consistent labeling everywhere, following [Kubernetes' recommended label conventions](https://kubernetes.io/docs/concepts/overview/working-with-objects/common-labels/).

---

## Helper 5: `iiot-fleet-app.selectorLabels`

```gotemplate
{{/*
Selector labels
*/}}
{{- define "iiot-fleet-app.selectorLabels" -}}
app.kubernetes.io/name: {{ include "iiot-fleet-app.name" . }}
app.kubernetes.io/instance: {{ .Release.Name }}
{{- end }}
```

- `app.kubernetes.io/name: {{ include "iiot-fleet-app.name" . }}` — uses Helper 1 to get the base chart name.
- `app.kubernetes.io/instance: {{ .Release.Name }}` — the specific release name (distinguishes, e.g., a "staging" install from a "prod" install of the *same* chart).

**Purpose — and why this one is special:** Selector labels are **immutable** on a Deployment once created (Kubernetes forbids changing `spec.selector` after creation) and must exactly match the Pod template's labels for the Deployment/Service to find its Pods. That's why this is deliberately kept as a **small, stable subset** of the full label set (just name + instance) — separate from `iiot-fleet-app.labels`, which can safely grow over time (e.g. adding a new label later) without breaking selector matching. This split (`selectorLabels` vs `labels`, with `labels` including `selectorLabels`) is the standard pattern `helm create` scaffolds, and it's considered a best practice.

---

## How this all fits together in practice

A typical `deployment.yaml` elsewhere in the chart would do:

```yaml
metadata:
  name: {{ include "iiot-fleet-app.fullname" . }}
  labels:
    {{- include "iiot-fleet-app.labels" . | nindent 4 }}
spec:
  selector:
    matchLabels:
      {{- include "iiot-fleet-app.selectorLabels" . | nindent 6 }}
  template:
    metadata:
      labels:
        {{- include "iiot-fleet-app.selectorLabels" . | nindent 8 }}
```

That's why the comment at the top warns not to delete this file — pulling it out would break `name`, `fullname`, `labels`, and `selectorLabels` references across **every** template in `db/`, `backend/`, `frontend/`, `emitter/`, `ingress.yaml`, and `app-configmap.yaml` simultaneously.

**Key DevOps takeaway:** this file is Helm's version of a shared "utils" module — write-once, reused-everywhere logic for naming and labeling, which keeps your whole chart consistent and avoids copy-paste drift between services.