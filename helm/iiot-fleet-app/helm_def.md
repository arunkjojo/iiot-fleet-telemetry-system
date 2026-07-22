# Helm Files

## 1. Core Helm Files (The Root Directory)

**`Chart.yaml`**

* **What:** The main metadata file for the Helm chart.
* **Why use it:** It gives your package an identity. Without it, Helm doesn't know what this package is.
* **How it works:** It contains essential information like the chart's `name`, `version`, `appVersion`, and `description`. Helm reads this when packaging or installing the chart.

**`values.yaml`**

* **What:** The default configuration file containing all the variables for your templates.
* **Why use it:** It allows you to deploy the same application into different environments (e.g., dev, staging, prod) simply by changing the values, without ever touching the actual template files.
* **How it works:** You define key-value pairs here (e.g., `frontend.replicaCount: 3`). Inside your templates, Helm injects these values using the syntax `{{ .Values.frontend.replicaCount }}`.

---

## 2. Shared Templates (The `templates/` Directory)

**`_helpers.tpl`**

* **What:** A file containing reusable template snippets, macros, and functions.
* **Why use it:** To keep your code DRY (Don't Repeat Yourself). You can define standard naming conventions or label blocks here instead of copying them into every single deployment file.
* **How it works:** You define a block using `{{- define "mychart.name" -}}...{{- end -}}` and then inject it into other files using `{{ include "mychart.name" . }}`.

**`NOTES.txt`**

* **What:** A plain text file containing instructions for the user.
* **Why use it:** To provide a smooth developer experience. It tells the user what to do next after the installation is complete.
* **How it works:** Helm renders this file (allowing you to inject variables like IPs or ports) and prints it to the terminal screen immediately after running `helm install`.

**`app-configmap.yaml`**

* **What:** A template for a Kubernetes ConfigMap.
* **Why use it:** To centralize shared, non-sensitive configuration data (like environment variables, third-party API URLs, or global settings) that multiple components might need to read.
* **How it works:** The ConfigMap is created in the cluster, and the backend/frontend Pods reference it in their `envFrom` sections to load the variables during startup.

**`ingress.yaml`**

* **What:** A template for a Kubernetes Ingress resource.
* **Why use it:** To expose your web application to the outside world. It acts as an API gateway or reverse proxy.
* **How it works:** It defines routing rules (e.g., `[mydomain.com/api](https://mydomain.com/api)` goes to the backend service, `[mydomain.com/](https://mydomain.com/)` goes to the frontend service). An Ingress Controller (like NGINX) reads this and configures the actual load balancer.

---

## 3. The Backend Component (`templates/backend/`)

**`deployment.yaml`**

* **What:** The template for the backend's Kubernetes Deployment.
* **Why use it:** To run the stateless backend API code (e.g., Node.js, Python, Java).
* **How it works:** It ensures a specific number of backend Pods are running, defines which Docker image to use, and dictates how to perform rolling updates if the code changes.

**`secret.yaml`**

* **What:** A template for a Kubernetes Secret specific to the backend.
* **Why use it:** To securely store sensitive data the backend needs, like third-party API keys, JWT secrets, or database connection passwords.
* **How it works:** The values are base64-encoded. When the backend Pod starts, Kubernetes securely decodes them and mounts them into the container as environment variables.

**`service.yaml`**

* **What:** The internal networking rule for the backend.
* **Why use it:** Because Pods die and get new IP addresses constantly. The frontend needs a reliable way to talk to the backend.
* **How it works:** It creates a stable internal DNS name (e.g., `my-app-backend-service`). When the frontend sends a request to that name, the Service load-balances the traffic to the healthy backend Pods.

---

## 4. The Database Component (`templates/db/`)

**`statefulset.yaml`**

* **What:** A template for a Kubernetes StatefulSet.
* **Why use it:** Databases are *stateful*—they save data. You cannot use a standard Deployment because databases require persistent identities and stable storage.
* **How it works:** It guarantees strict ordering and unique network identifiers (e.g., `db-0`, `db-1`) for the database Pods. Crucially, it dynamically provisions Persistent Volume Claims (PVCs) so that if a DB Pod restarts, it reconnects to the exact same hard drive containing its data.

**`secret.yaml`**

* **What:** A Kubernetes Secret for the database.
* **Why use it:** To set the initial root passwords, admin users, or replication keys when the database first initializes.
* **How it works:** Mounted into the database container upon creation, allowing the DB software (like PostgreSQL or MySQL) to establish secure credentials.

**`service.yaml`**

* **What:** Internal networking for the database.
* **Why use it:** So the backend application can reliably connect to the database.
* **How it works:** Typically, a headless service (where `clusterIP: None`) is used with StatefulSets to allow the backend to connect directly to the primary database Pod rather than randomly load-balancing across replicas.

---

## 5. The Frontend Component (`templates/frontend/`)

**`deployment.yaml`**

* **What:** The template for the frontend's Kubernetes Deployment.
* **Why use it:** To serve the user interface (e.g., a React, Vue, or Angular app served by NGINX).
* **How it works:** Manages the Pods containing the compiled UI assets. Like the backend deployment, it handles scaling and updates.

**`service.yaml`**

* **What:** Internal networking for the frontend.
* **Why use it:** To act as the bridge between the Ingress (external traffic) and the frontend Pods.
* **How it works:** The Ingress routes external internet traffic to this Service, which then distributes the traffic to the available frontend UI Pods.

---

# Template

## 1. Top-Level Identity

**`apiVersion`**

* **What:** The version of the Kubernetes API you are targeting.
* **Why use it:** It tells the cluster which schema to use to validate and create your object.
* **How it works:** The Kubernetes API server routes your request to the internal handler for this specific version.
* **Values:** Depends on the object type (e.g., `apps/v1` for Deployments/StatefulSet, `v1` for Pods/ConfigMaps/Secret/Service, `networking.k8s.io/v1` for Ingress).
s
**`kind`**

* **What:** The type of Kubernetes resource you want to create.
* **Why use it:** Defines the actual infrastructure component you are deploying.
* **How it works:** Instructs the cluster to instantiate this specific controller.
* **Values:** `Deployment`, `Pod`, `Service`, `Secret`, `StatefulSet`, `ConfigMap`, etc.

**`metadata`**

* **What:** Data that helps uniquely identify the object.
* **Why use it:** Used for organizing, searching, and managing resources within a namespace.
* **How it works:** Attaches a unique identity and searchable tags to your resource.
* **Values:** A dictionary object containing fields like `name`, `namespace`, `labels`, and `annotations`.

**`name`** (under metadata)

* **What:** The unique identifier for this specific resource.
* **Why use it:** So you and the cluster can reference, update, or delete this specific object later.
* **How it works:** Registers this name in the cluster's etcd database under the current namespace.
* **Values:** A valid DNS subdomain string (e.g., `my-frontend-app`).

**`labels`**

* **What:** Key-value pairs attached to the object.
* **Why use it:** To group, categorize, and select related resources.
* **How it works:** Other resources (like Services or Deployments) use "selectors" to find objects matching these labels.
* **Values:** A map of string keys and string values.

**`app.kubernetes.io/component`**

* **What:** A standardized Kubernetes recommended label.
* **Why use it:** Provides a universal way to identify the architectural role of this resource.
* **How it works:** Tools like Helm or observability dashboards look for this specific key to understand your app's architecture.
* **Values:** Strings like `frontend`, `database`, `cache`, or `backend`.

---

## 2. Deployment Specification

**`spec`** (Top level)

* **What:** The desired state for the resource.
* **Why use it:** This is where you declare exactly how you want the cluster to configure your application.
* **How it works:** Kubernetes constantly monitors the cluster and works to make the actual state match this `spec`.
* **Values:** A complex object containing `replicas`, `selector`, `template`, etc.

**`replicas`**

* **What:** The number of identical Pods you want running.
* **Why use it:** Provides high availability and scaling.
* **How it works:** The Deployment controller creates or deletes Pods until this exact number is running.
* **Values:** Any non-negative integer (e.g., `1`, `3`, `10`).

**`selector`**

* **What:** Defines how the Deployment finds which Pods to manage.
* **Why use it:** So the Deployment doesn't accidentally manage Pods belonging to a different application.
* **How it works:** The controller continuously queries the API for Pods that match the rules defined inside this selector.
* **Values:** Contains fields like `matchLabels` or `matchExpressions`.

**`matchLabels`**

* **What:** A strict key-value matching rule.
* **Why use it:** It is the simplest and most common way to link a Deployment to its Pods.
* **How it works:** Any Pod with labels exactly matching this dictionary is assumed to be owned by this Deployment.
* **Values:** A map of string keys and values (must perfectly match the labels in the Pod template).

---

## 3. Pod Template

**`template`**

* **What:** The blueprint for the Pods the Deployment will create.
* **Why use it:** You don't create Pods manually; you provide this template, and the Deployment stamps them out.
* **How it works:** Whenever a new replica is needed, the controller copies this template to generate a new Pod object.
* **Values:** An object containing `metadata` and `spec`.

*(Note: The `metadata`, `labels`, and `app.kubernetes.io/component` nested under `template` function exactly like the top-level metadata, but they apply directly to the generated Pods, not the Deployment itself.)*

**`spec`** (under template)

* **What:** The actual configuration of the Pod itself.
* **Why use it:** To define containers, volumes, and networking rules for the application.
* **How it works:** The kubelet reads this on the worker node to figure out how to run your application.
* **Values:** A complex object containing `containers`, `volumes`, `securityContext`, etc.

---

## 4. Container Configuration

**`containers`**

* **What:** The list of applications running inside this Pod.
* **Why use it:** A Pod must contain at least one container to execute your code.
* **How it works:** The container runtime (like containerd) iterates through this list and starts them sharing the same network space.
* **Values:** An array of container definition objects.

**`name`** (under containers)

* **What:** The identifier for this specific container within the Pod.
* **Why use it:** Helpful for checking logs or executing commands inside a specific container if the Pod has multiple.
* **How it works:** Tags the local container process with this alias.
* **Values:** A string (e.g., `nginx`, `web-server`).

**`image`**

* **What:** The Docker/OCI image repository and tag.
* **Why use it:** Dictates exactly what software to run.
* **How it works:** The node downloads this image from a container registry before starting it.
* **Values:** A string (e.g., `nginx:1.25.1`, `gcr.io/my-project/my-app:v2`).

**`imagePullPolicy`**

* **What:** Rules for when the node should download the image from the registry.
* **Why use it:** To force fresh pulls (good for "latest" tags) or use cached images to speed up startup times.
* **How it works:** The kubelet checks this policy before attempting to fetch the image over the network.
* **Values:** `Always`, `IfNotPresent`, or `Never`.

**`ports`**

* **What:** The network ports this container wants to expose.
* **Why use it:** Primarily informational, but required by some network policies and helpful for developers to know what port the app uses.
* **How it works:** Does not actually map ports (Pods share a network namespace), but acts as a declaration of intent.
* **Values:** An array of port objects.

**`containerPort`**

* **What:** The actual port number the application process inside the container is listening on.
* **Values:** An integer between `1` and `65535`.

---

## 5. Environment Variables

**`envFrom`**

* **What:** A bulk-import mechanism for environment variables.
* **Why use it:** To inject dozens of variables at once without typing them out individually in the manifest.
* **How it works:** Pulls every key-value pair from a referenced ConfigMap or Secret and injects them all into the container.
* **Values:** An array containing `configMapRef` or `secretRef`.

**`configMapRef`** -> **`name`**

* **What:** The name of the ConfigMap to import.
* **Why use it:** Separates plain-text configuration from your deployment code.
* **Values:** The string name of an existing ConfigMap in the same namespace.

**`env`**

* **What:** A list of explicit, individually defined environment variables.
* **Why use it:** To set one-off variables or map a specific sensitive value securely.
* **How it works:** Injects these explicitly into the container's OS environment on boot.
* **Values:** An array of objects with `name` and either `value` or `valueFrom`.

**`valueFrom`** -> **`secretKeyRef`**

* **What:** Fetches a variable's value from a Kubernetes Secret.
* **Why use it:** Keeps passwords and API keys out of your plain-text manifest file.
* **How it works:** Kubernetes decodes the base64 Secret dynamically and passes it securely to the container.
* **Values:** An object defining the `name` of the secret and the specific `key`.

**`key`** (under secretKeyRef)

* **What:** The specific item inside the Secret to retrieve.
* **Values:** A string matching a key in the referenced Secret (e.g., `DB_PASSWORD`).

---

## 6. Health Checks (Probes)

**`readinessProbe`**

* **What:** Checks if your application is ready to receive network traffic.
* **Why use it:** Prevents Kubernetes from sending user traffic to a container that is still booting up or temporarily overloaded.
* **How it works:** If this probe fails, the Pod is removed from Service load balancers.

**`livenessProbe`**

* **What:** Checks if your application is fundamentally alive or hopelessly stuck.
* **Why use it:** To automatically recover from deadlocks, memory leaks, or crashed processes.
* **How it works:** If this probe fails, Kubernetes forcefully restarts the container.

### Shared Probe Parameters

Both readiness and liveness probes use the following settings to execute their checks:

* **`tcpSocket`**: The method of checking. Tells Kubernetes to attempt a raw TCP connection. (Other options include `httpGet` or `exec`).
* **`port`**: The port to probe. Values: Integer (e.g., `8080`) or a named port string.
* **`initialDelaySeconds`**: How long to wait after the container starts before running the first probe. Prevents killing an app that just takes a long time to boot. Values: Integer (seconds).
* **`periodSeconds`**: How often to run the probe. Values: Integer (default is 10 seconds).
* **`timeoutSeconds`**: How long to wait for the probe to respond before considering it a failure. Values: Integer (default is 1 second).
* **`failureThreshold`**: How many times the probe must fail consecutively before Kubernetes takes action (restarting or cutting traffic). Values: Integer (default is 3).

---

## 7. Compute Limits

**`resources`**

* **What:** Dictates the CPU and Memory limits and requests for the container.
* **Why use it:** Ensures your app gets the compute power it needs (requests) and prevents a rogue app from consuming the entire server (limits).
* **How it works:** The Kubernetes scheduler uses `requests` to decide which node has enough space for the Pod. The container runtime uses `limits` to throttle the app if it uses too much.
* **Values:** An object containing `requests` (minimum guaranteed) and `limits` (maximum allowed).