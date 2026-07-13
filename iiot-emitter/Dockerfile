# IIoT Fleet Emitter — simulates up to VEHICLE_COUNT independent onboard devices posting
# telemetry over HTTP to the backend's ingestion endpoint. Outbound-only client: no ports
# exposed, no healthcheck (Compose orders it via `backend`'s service_healthy condition).
FROM python:3.12-slim

WORKDIR /app

# Keep stdout unbuffered so `docker-compose logs -f iiot-emitter` shows log lines immediately.
ENV PYTHONUNBUFFERED=1

COPY requirements.txt .
RUN pip install --no-cache-dir -r requirements.txt

COPY emitter.py .

CMD ["python", "emitter.py"]
