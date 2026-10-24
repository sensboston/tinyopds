#!/bin/sh
# Runs TinyOPDS CLI as the user given by PUID/PGID, so the files in the data folder belong to that user
set -e

if [ "$(id -u)" = "0" ]; then
    mkdir -p "$TINYOPDS_DATA_DIR"
    # Files left by a run as another user (or as root) are given to this one
    chown "$PUID:$PGID" "$TINYOPDS_DATA_DIR"
    find "$TINYOPDS_DATA_DIR" -mindepth 1 -maxdepth 1 ! -user "$PUID" -exec chown "$PUID:$PGID" {} +
    exec gosu "$PUID:$PGID" mono /app/TinyOPDSCLI.exe "$@"
fi

exec mono /app/TinyOPDSCLI.exe "$@"
