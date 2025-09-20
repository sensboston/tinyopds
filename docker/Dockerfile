# Base image with Mono + GDI+ for .NET Framework apps
FROM debian:bookworm-slim AS base
ENV DEBIAN_FRONTEND=noninteractive
RUN apt-get update && apt-get install -y --no-install-recommends \
    ca-certificates tzdata fontconfig \
    mono-runtime mono-complete libgdiplus \
    fonts-dejavu-core fonts-liberation fonts-noto \
    && rm -rf /var/lib/apt/lists/*
WORKDIR /app

# ---------- CLI image ----------
FROM base AS cli
COPY app/TinyOPDSCLI.exe /app/TinyOPDSCLI.exe
# Create wrapper script for better signal handling
RUN printf '%s\n' '#!/bin/sh' \
  'exec mono /app/TinyOPDSCLI.exe "$@"' \
  > /usr/local/bin/tinyopds-cli && chmod +x /usr/local/bin/tinyopds-cli

ENTRYPOINT ["/usr/local/bin/tinyopds-cli"]
CMD []

# ---------- GUI deps (Xvfb + noVNC) ----------
FROM base AS gui-base
RUN apt-get update && apt-get install -y --no-install-recommends \
    xvfb xauth x11vnc fluxbox curl python3 procps \
    dbus-x11 \
    && rm -rf /var/lib/apt/lists/*

# Install noVNC + websockify
ENV NOVNC_DIR=/opt/novnc \
    WEBSOCKIFY_DIR=/opt/websockify
RUN mkdir -p $NOVNC_DIR $WEBSOCKIFY_DIR && \
    curl -L -o /tmp/novnc.tar.gz https://github.com/novnc/noVNC/archive/refs/tags/v1.5.0.tar.gz && \
    tar -xzf /tmp/novnc.tar.gz -C /opt && mv /opt/noVNC-* $NOVNC_DIR && \
    curl -L -o /tmp/websockify.tar.gz https://github.com/novnc/websockify/archive/refs/tags/v0.12.0.tar.gz && \
    tar -xzf /tmp/websockify.tar.gz -C /opt && mv /opt/websockify-* $WEBSOCKIFY_DIR && \
    ln -s $NOVNC_DIR/vnc.html $NOVNC_DIR/index.html && \
    rm -f /tmp/*.tar.gz

# Create startup script with proper signal handling
RUN cat > /usr/local/bin/run-gui.sh <<'EOF'
#!/bin/sh
set -e

# Setup display
export DISPLAY=:99
export MONO_THREADS_PER_CPU=2000

# Create log directory
mkdir -p /tmp/logs

# Start Xvfb
Xvfb :99 -screen 0 1280x800x24 -nolisten tcp -ac &
XVFB_PID=$!
sleep 2

# Start window manager
fluxbox >/tmp/logs/fluxbox.log 2>&1 &
FLUXBOX_PID=$!

# Start VNC server
x11vnc -display :99 -nopw -forever -shared -rfbport 5901 >/tmp/logs/x11vnc.log 2>&1 &
VNC_PID=$!

# Start websockify for noVNC
python3 /opt/websockify/run --web /opt/novnc 6080 localhost:5901 >/tmp/logs/websockify.log 2>&1 &
WEBSOCKIFY_PID=$!

echo "Starting TinyOPDS GUI..."
echo "noVNC available at http://localhost:6080"

# Start the application
mono /app/TinyOPDS.exe "$@" &
APP_PID=$!

# Signal handler
cleanup() {
    echo "Shutting down..."
    # Kill processes in reverse order
    for pid in $APP_PID $WEBSOCKIFY_PID $VNC_PID $FLUXBOX_PID $XVFB_PID; do
        if kill -0 $pid 2>/dev/null; then
            kill -TERM $pid 2>/dev/null || true
        fi
    done
    wait $APP_PID 2>/dev/null || true
    exit 0
}

trap cleanup TERM INT

# Wait for the main app
wait $APP_PID
RESULT=$?

cleanup
exit $RESULT
EOF

RUN chmod +x /usr/local/bin/run-gui.sh

# ---------- GUI image ----------
FROM gui-base AS gui
COPY app/TinyOPDS.exe /app/TinyOPDS.exe

# Health check
HEALTHCHECK --interval=30s --timeout=3s --start-period=10s \
  CMD pgrep -f "mono.*TinyOPDS.exe" || exit 1

EXPOSE 80 8080 6080
ENTRYPOINT ["/usr/local/bin/run-gui.sh"]
CMD []
