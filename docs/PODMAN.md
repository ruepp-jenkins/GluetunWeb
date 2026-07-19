# Running GluetunWeb with Podman

GluetunWeb talks to the container engine through the **Docker Engine API**. Podman ships a
Docker-compatible API socket, so GluetunWeb works with Podman unchanged — you only point it at
Podman's socket instead of Docker's. The `Docker.DotNet` client the backend uses is API-compatible
with Podman's service.

There are two things to configure:

1. The socket GluetunWeb connects to (either bind-mount it, or set `DOCKER_HOST`).
2. That the Gluetun containers it creates can use `/dev/net/tun` + `NET_ADMIN`.

---

## 1. Enable the Podman API socket

### Rootful (simplest, closest to Docker behaviour)

```bash
sudo systemctl enable --now podman.socket
# socket path: /run/podman/podman.sock
```

### Rootless (recommended for least privilege)

```bash
systemctl --user enable --now podman.socket
# socket path: /run/user/$(id -u)/podman/podman.sock
```

Verify it responds:

```bash
curl --unix-socket /run/user/$(id -u)/podman/podman.sock http://d/v1.40/version
```

---

## 2a. Bind-mount the Podman socket (default approach)

Mount Podman's socket to the path GluetunWeb expects (`/var/run/docker.sock`) and leave
`DOCKER_HOST` unset:

```yaml
services:
  gluetunweb:
    image: ghcr.io/youruser/gluetunweb:latest
    environment:
      GLUETUNWEB_MASTER_KEY: "change-me"
    volumes:
      - gluetunweb-data:/data
      # rootful:
      - /run/podman/podman.sock:/var/run/docker.sock
      # rootless (replace 1000 with your UID):
      # - /run/user/1000/podman/podman.sock:/var/run/docker.sock
volumes:
  gluetunweb-data:
```

Run it with `podman-compose up -d` (or `podman run` with the equivalent flags).

## 2b. Or use a TCP / explicit socket endpoint

Instead of mounting, set the endpoint explicitly. Either set the `DOCKER_HOST` environment
variable, or set **Global Settings → Docker host override** in the UI (it takes precedence):

```
DOCKER_HOST=unix:///run/user/1000/podman/podman.sock
```

```
# or a remote/TCP Podman service
DOCKER_HOST=tcp://10.0.0.5:2375
```

---

## 3. TUN device + NET_ADMIN for Gluetun

The Gluetun containers GluetunWeb creates request `NET_ADMIN` and the `/dev/net/tun` device
(handled automatically by the orchestrator). For **rootless** Podman make sure the host exposes the
TUN device to your user; on most distributions it already does:

```bash
ls -l /dev/net/tun          # should exist
# if missing:
sudo modprobe tun
```

If a rootless Gluetun container cannot bring up the tunnel, run the Podman service rootful, or grant
your user access to `/dev/net/tun`.

---

## Notes & differences

- **`network_mode: container:<id>`** (used to attach the SOCKS5 sidecar to the Gluetun network
  namespace) is fully supported by Podman.
- **Port publishing** on the Gluetun container works the same; the auto-assigned host ports are
  published there and inherited by the sidecar.
- Rootless Podman publishes ports ≥ 1024 without extra privileges — the default auto-assign range
  (20000–21000) is already safe.
- Everything else (image pulls, file injection via the copy API, logs, inspect) uses standard
  Docker-API calls that Podman implements.
