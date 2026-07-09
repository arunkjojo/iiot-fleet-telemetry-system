# =============================================================================
# db/Dockerfile — Tuned PostgreSQL image for IIoT Fleet Telemetry
# =============================================================================
# Pins the base image and layers a tuned postgresql.conf on top of the
# official entrypoint so first-run database creation (POSTGRES_DB,
# POSTGRES_USER, POSTGRES_PASSWORD) still works exactly as with the stock
# postgres:16-alpine image. Schema is owned exclusively by EF Core
# migrations — this image never touches schema, only server configuration.
# =============================================================================
FROM postgres:16-alpine

# Ship the tuned config alongside the image; readable by the postgres user
# that the official entrypoint drops privileges to.
COPY postgresql.conf /etc/postgresql/postgresql.conf

# Keep the official entrypoint (handles POSTGRES_DB/USER/PASSWORD-driven
# first-run initdb + database/user creation) and only override the server's
# config file via the postgres CMD arguments it execs into.
CMD ["postgres", "-c", "config_file=/etc/postgresql/postgresql.conf"]
