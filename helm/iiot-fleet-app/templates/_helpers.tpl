{{/*
_helpers.tpl — required, not optional. Every other template in this chart
(db/, backend/, frontend/, emitter/, ingress.yaml, app-configmap.yaml)
calls `include "iiot-fleet-app.fullname"`/`.labels`/`.selectorLabels` from
here. It stays at the templates/ root rather than inside any one per-service
folder because Helm loads *.tpl files from anywhere under templates/
recursively — there's no per-folder scoping, so one shared copy is correct
and moving it into e.g. backend/ would not make it backend-only, only
confusing to find. Removing it would break every template that references
these three helpers (grep them if in doubt: they're used far more than they
look).
*/}}

{{/*
Expand the name of the chart.
*/}}
{{- define "iiot-fleet-app.name" -}}
{{- .Chart.Name | trunc 63 | trimSuffix "-" }}
{{- end }}

{{/*
Create a default fully qualified app name. Truncated at 63 chars since some
k8s name fields are limited to that (by the DNS naming spec).
*/}}
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

{{/*
Create chart name and version as used by the chart label.
*/}}
{{- define "iiot-fleet-app.chart" -}}
{{- printf "%s-%s" .Chart.Name .Chart.Version | replace "+" "_" | trunc 63 | trimSuffix "-" }}
{{- end }}

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

{{/*
Selector labels
*/}}
{{- define "iiot-fleet-app.selectorLabels" -}}
app.kubernetes.io/name: {{ include "iiot-fleet-app.name" . }}
app.kubernetes.io/instance: {{ .Release.Name }}
{{- end }}
