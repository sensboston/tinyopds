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
ENTRYPOINT ["mono", "/app/TinyOPDSCLI.exe"]
CMD []

# ---------- GUI base with Xvfb + noVNC ----------
FROM base AS gui-base
RUN apt-get update && apt-get install -y --no-install-recommends \
    xvfb xauth x11vnc fluxbox curl python3 procps \
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

# Create startup script
RUN cat > /usr/local/bin/run-gui.sh <<'EOF'
#!/bin/sh
set -e

export DISPLAY=:99
export MONO_THREADS_PER_CPU=2000

# Start Xvfb
Xvfb :99 -screen 0 1280x800x24 -nolisten tcp -ac &
XVFB_PID=$!
sleep 2

# Start fluxbox window manager
fluxbox >/dev/null 2>&1 &

# Start VNC server
x11vnc -display :99 -nopw -forever -shared -rfbport 5901 >/dev/null 2>&1 &

# Start websockify for noVNC
python3 /opt/websockify/run --web /opt/novnc 6080 localhost:5901 >/dev/null 2>&1 &
WEBSOCKIFY_PID=$!

echo "TinyOPDS GUI started. noVNC available at http://localhost:6080"

# Run TinyOPDS GUI
exec mono /app/TinyOPDS.exe "$@"
EOF

RUN chmod +x /usr/local/bin/run-gui.sh

# ---------- GUI image ----------
FROM gui-base AS gui
COPY app/TinyOPDS.exe /app/TinyOPDS.exe
EXPOSE 80 8080 6080
ENTRYPOINT ["/usr/local/bin/run-gui.sh"]
CMD []
