# TinyOPDS CLI on Mono: OPDS server for a home book library on NAS and other Linux hosts.
# Build context is this folder, TinyOPDSCLI.exe is expected in app/ (the GitHub workflow puts it there)
FROM debian:bookworm-slim

RUN apt-get update \
    && apt-get install -y --no-install-recommends \
        mono-complete libgdiplus libsqlite3-0 \
        gosu tini curl ca-certificates tzdata \
    && rm -rf /var/lib/apt/lists/*

COPY app/TinyOPDSCLI.exe /app/
COPY entrypoint.sh /entrypoint.sh
RUN chmod +x /entrypoint.sh

# Library is mounted to /books (read only is fine), config, database and log are kept in /data.
# Any setting of TinyOPDS.config can be given as TINYOPDS_<SETTING NAME>, e.g. TINYOPDS_LANGUAGE=ru.
# Container mode: the library is scanned on start and every TINYOPDS_SCAN_INTERVAL_HOURS (24 by default),
# and watched for changes
ENV TINYOPDS_CONTAINER=1 \
    TINYOPDS_DATA_DIR=/data \
    TINYOPDS_LIBRARYPATH=/books \
    TINYOPDS_USEUPNP=false \
    TINYOPDS_OPENNATPORT=false \
    PUID=1000 \
    PGID=1000 \
    HOME=/tmp

VOLUME ["/data"]
EXPOSE 8080

# SIGINT lets the server save the books found so far before it exits
STOPSIGNAL SIGINT

# Any HTTP answer means the server is up (with authorization enabled it answers 401)
HEALTHCHECK --interval=60s --timeout=10s --start-period=60s \
    CMD curl -s -o /dev/null "http://127.0.0.1:${TINYOPDS_SERVERPORT:-8080}/" || exit 1

ENTRYPOINT ["/usr/bin/tini", "--", "/entrypoint.sh"]
CMD ["start"]
