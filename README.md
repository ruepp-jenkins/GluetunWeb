# GluetunWeb

A high-performance, minimalist control panel for [Gluetun](https://github.com/qdm12/gluetun) VPN
containers and their companion **SOCKS5 proxy** containers. Manage the full Docker container
lifecycle — providers, custom OpenVPN/WireGuard configs, and VPN-linked proxies — from one
retro-terminal dashboard, without hand-editing `docker-compose.yml` files.

```
~/gluetunweb — proxy control panel
[00] Guide  [01] Settings  [02] Credentials  [03] Providers
[04] Custom VPN  [05] Connections  [06] Load Balancers
```

A first-run **Guide** walks you through the flow end to end (each step links straight to its page),
and after login the dashboard lands on it until you have your first connection — then on Connections.

- **Backend:** ASP.NET Core 10 (C#) Web API · EF Core + SQLite · Docker.DotNet
- **Frontend:** React 19 + Vite + TypeScript + Tailwind CSS v4 (dark, monospaced, high-density)
- **Packaging:** single multi-stage Docker image (the API serves the built SPA)

---

## Features

### 01 · Global Settings
Manage timezone (`TZ`), the public-IP API (`PUBLICIP_API` — presets or a custom value, + token), the HTTP proxy block, and the
control-server authentication mode (`none` / `basic` / `apikey`, with an API-key generator that
mirrors `gluetun genkey`), the auto-assign port ranges, and a per-browser **button style**
(text / ascii / icons). Every field shows its **environment-variable description and example
inline** — no tooltips.

### 02 · Credentials
Store a VPN account **once** and reference it from as many providers as you like — one NordVPN login
shared by `nordvpn-nl`, `nordvpn-ch` and so on, rather than retyping it per country. Credentials are
typed (OpenVPN username/password or WireGuard keys) and only offered where they fit. Selecting one
*replaces* the entry's own secrets rather than merging, and one in use cannot be deleted. Because a
container's environment is fixed at creation, editing a credential prompts you with **exactly which
running connections carry it** and offers to redeploy them (a restart would not pick the change up).
See [docs/ENVVARS.md](docs/ENVVARS.md#credentials-shared-secrets).

### 03 · Provider Management
Store credentials for multiple concurrent VPN providers (Mullvad, NordVPN, ProtonVPN, PIA, …),
OpenVPN or WireGuard, with server-selection filters. OpenVPN providers can pick their transport
(`OPENVPN_PROTOCOL` — **UDP** by default, **TCP** for networks that block UDP); the form reads each
provider's per-server `tcp`/`udp` flags from the catalog and disables a transport the provider has no
servers for, instead of letting it fail later as an opaque "no server found". Secrets are AES-256-GCM
encrypted at rest and never returned to the browser. The valid provider names are sourced live from
[qdm12/gluetun-servers](https://github.com/qdm12/gluetun-servers) (`pkg/servers/*.json`) — the app
shallow-clones the repo on first use and can `git pull` to update via **↻ update list** — so the
suggestions always match what Gluetun accepts. Free-text is still allowed for anything unlisted.

Server selection is a **cascade** read from that provider's server list: `region → country → city →
hostname`. Each dropdown offers only values that exist given the levels above it, changing a level
clears the ones below, and the VPN type filters all of them. See
[docs/ENVVARS.md](docs/ENVVARS.md#server-selection-is-a-cascade).

### 04 · Custom VPN Groups
Upload a `.ovpn`/`.conf` file **or paste raw text** — the config stays freely editable. OpenVPN
configs are injected into the container as files (with optional `OPENVPN_USER`/`OPENVPN_PASSWORD`);
WireGuard configs are parsed into `WIREGUARD_*` variables. Because Gluetun requires an IP rather than
a hostname, set an **endpoint DNS name** and use the `{{DNS_IP}}` placeholder — it's resolved and
substituted into the config just before the container starts.

### 05 · Connection Orchestrator
Each connection = one Gluetun container + an optional SOCKS5 sidecar (joined via
`network_mode: container:<gluetun>`). Identifiers are validated against `^[a-zA-Z0-9-]+$`, and the
**port manager** reserves one contiguous **block** of host ports per connection (and per load
balancer), checking both the database and live Docker for conflicts. Within a block each purpose has
a fixed offset — control `+0`, SOCKS5 `+1`, HTTP `+2`, Shadowsocks `+3` — so toggling a proxy off and
on again always returns the same port instead of reshuffling the others. Deploy / start / stop / restart / logs — real Docker operations. The status column reports the
**tunnel**, not just the container: GluetunWeb queries Gluetun's control server for VPN state, exit
IP (with city/country) and the currently forwarded port. A **test** button fetches a URL of your
choice through the proxy and shows the verdict plus the raw response, and **info** gives copy-ready
endpoints and a tun2socks snippet. Optional per-connection **port forwarding**
(`VPN_PORT_FORWARDING`, PIA/ProtonVPN/PrivateVPN/Perfect Privacy) and firewall settings
(`FIREWALL_OUTBOUND_SUBNETS` for LAN access) are exposed too. Optional per-connection
SOCKS5 credentials (`PROXY_USER`/`PROXY_PASSWORD`; runs open with `REQUIRE_AUTH=false` when unset).

Per-connection **DNS filtering** uses Gluetun's internal resolver to drop malicious
(`BLOCK_MALICIOUS`, on by default) and ad (`BLOCK_ADS`) hostnames, with `DNS_UNBLOCK_HOSTNAMES` as
the escape hatch when a list breaks something.

Each connection can also enable Gluetun's **built-in Shadowsocks server** (`SHADOWSOCKS`) — no
sidecar, it runs inside the Gluetun container. Password (required) and AEAD cipher are per
connection, and the auto-assigned host port is published on **both TCP and UDP**, which Shadowsocks
needs.

Every container GluetunWeb creates is stamped with `managed-by=gluetunweb` +
`gluetunweb.connection=<id>` labels. The page detects **orphaned** labeled containers (no matching
connection — e.g. left after a failed delete) and offers to remove them; removal is label-verified
so it can never touch unrelated containers. Each row also has a **duplicate** action that opens the
create dialog pre-filled from an existing entry (available on Providers, Custom VPN and Connections).

### 06 · Load Balancers
Create [Socks5BalancerAsio](https://github.com/Socks5Balancer/Socks5BalancerAsio)
(`ruepp/socks5balancerasio`) containers that load-balance across the SOCKS5 proxies of selected
connections. Pick which SOCKS5-enabled connections are upstreams (add/remove any time), tune the
balancer (`upstreamSelectRule`, timeouts, health-check host/port, thread count, …), and the
`config.json` is generated and injected at deploy. The page links to the balancer's **web UI**
(Socks5BalancerAsio's embedded status dashboard) and shows the balanced SOCKS5 endpoint. Upstreams
are reached via `host.docker.internal` (a `host-gateway` mapping) on each connection's published
SOCKS5 port, so credentials-per-upstream carry through automatically.

---

## Quick start (Docker)

```bash
# 1. Set a strong encryption key (protects stored secrets at rest)
export GLUETUNWEB_MASTER_KEY="$(openssl rand -base64 32)"
#    …and put it in docker-compose.yml (environment: GLUETUNWEB_MASTER_KEY)

# 2. Build and run
docker compose up -d --build

# 3. Open the dashboard and create the admin account on first run
open http://localhost:8080
```

The compose file mounts `/var/run/docker.sock` (to manage containers) and a named volume at `/data`
(SQLite database). See [docs/PODMAN.md](docs/PODMAN.md) for Podman.

---

## Host requirements & troubleshooting

### The `tun` kernel module (most common issue)

Every Gluetun container needs a working TUN device. GluetunWeb already gives each container
`--cap-add=NET_ADMIN` and `--device /dev/net/tun` (verified correct), **but the host kernel must have
the `tun` module loaded** — container device passthrough cannot supply a driver the host lacks.

If a deployed connection's logs show:

```
ERROR [vpn] setting up tun device: checking TUN device:
      TUN device is not available: open /dev/net/tun: no such device
```

that `no such device` is `ENODEV` — the driver isn't loaded on the **host**. Fix it on the host
(not in the container):

```bash
sudo modprobe tun                     # load it now
echo tun | sudo tee /etc/modules-load.d/tun.conf   # persist across reboots
ls -l /dev/net/tun                    # should be: crw-rw-rw- ... 10, 200
```

After loading the module, redeploy the connection — Gluetun will pass the TUN stage. (On most bare
metal / standard VMs the module is already loaded; minimal VPS/LXC/sandbox hosts often need this.)

### Container stays "running" but never connects
Gluetun retries internally rather than exiting, so *Docker* reports `running`. The status column
shows Gluetun's own view next to it — **connecting** (amber) means the process is up but no public IP
has been obtained yet, which is the usual sign of bad credentials or a server filter matching
nothing. Use **logs** for the exact reason, or **test** to try a real request through the proxy.

### `No free 8-port block left in range …`
Each connection reserves a block of 8 host ports and each load balancer a block of 4. Widen the range
in **Global Settings → Auto-Assign Port Range**, or remove stale connections holding blocks.

---

## Local development

Two processes with hot reload:

```bash
# Terminal 1 — backend API on :8080
cd src/GluetunWeb.Api
GLUETUNWEB_MASTER_KEY=dev-key ASPNETCORE_URLS=http://localhost:8080 dotnet run

# Terminal 2 — Vite dev server on :5173 (proxies /api to :8080)
cd src/web
npm install
npm run dev
```

Open http://localhost:5173. A `gluetunweb.db` file is created next to the API for local runs.

### Tests

```bash
dotnet test        # 84 unit tests: port blocks/layout, parsers, crypto, identifier, tar,
                   # auth config, DNS/port-forwarding env, provider cascade, credential resolution
```

The frontend is typechecked and built with `npm run build` (or `npx tsc --noEmit`) from `src/web`.

---

## Architecture

```
Browser (React SPA)
   │  HttpOnly cookie session (SameSite=Strict)
   ▼
ASP.NET Core API ──► EF Core / SQLite      (config + AES-encrypted secrets)
                └──► Docker.DotNet ──► Docker/Podman Engine API
                                         ├─ gluetun container  (NET_ADMIN, /dev/net/tun)
                                         └─ socks5 sidecar     (network_mode: container:<gluetun>)
```

- Secrets are decrypted **only** server-side when building a container; API responses expose
  presence booleans (`has*`), never values.
- Config files (control-server `config.toml`, custom `.ovpn`, balancer `config.json`) are injected via
  the Docker copy API into a **named volume** per connection/balancer — no host bind mounts, so it
  stays portable, and the config **survives container recreation** (e.g. a Watchtower image update,
  which discards the writable layer). Volumes are removed when the resource is deleted.
- A background **reconciler** keeps the stack self-healing under external updates: if something
  recreates a Gluetun container, it retracks the new id and relinks the SOCKS5 sidecar (whose network
  namespace points at the old container id). It never resurrects removed containers or restarts
  connections you stopped.
- The dashboard reads **Gluetun's own control server** for real tunnel state (VPN status, exit IP,
  forwarded port), and reaches each container's published port at whatever address actually works
  from where it runs — `localhost` on the host, or the host gateway / `host.docker.internal` when the
  dashboard is itself a container on a different Docker network.

These background behaviours — what runs on a timer, what state is polled, and where everything is
stored — are documented in **[docs/AUTOMATIONS.md](docs/AUTOMATIONS.md)**.

Project layout:

```
src/GluetunWeb.Api/     ASP.NET Core API
  Auth/ Crypto/ Data/ Docker/ Gluetun/ Services/ Endpoints/ Models/ Validation/
  Services/ContainerReconcilerService.cs   background self-heal
  Services/ProviderCatalogRefreshService.cs  periodic git pull
  Gluetun/GluetunControlClient.cs          reads real VPN state
  Services/HostEndpointResolver.cs         reaches published ports cross-network
src/GluetunWeb.Api.Tests/  xUnit tests
src/web/                React + Tailwind SPA (built into the API's wwwroot)
docs/                   AUTOMATIONS.md, ENVVARS.md, PODMAN.md
Dockerfile              multi-stage build
docker-compose.yml
```

See [docs/ENVVARS.md](docs/ENVVARS.md) for the full environment-variable catalog and
[docs/AUTOMATIONS.md](docs/AUTOMATIONS.md) for the background automation & storage reference.

---

## Security

- **Authentication:** single admin account; password hashed with PBKDF2
  (`PasswordHasher<T>`, per-password salt, transparent rehash). HttpOnly, `SameSite=Strict` session
  cookie. All `/api/*` routes require authentication (verified: unauthenticated requests → `401`).
- **Secrets at rest:** provider credentials, WireGuard keys, tokens, and proxy passwords are
  encrypted with AES-256-GCM using a key derived from `GLUETUNWEB_MASTER_KEY`. **Set a strong key
  and back it up** — rotating it invalidates stored secrets.
- **No secret exposure to the frontend:** provider/global/proxy secrets are never serialized — the
  UI shows `has*` flags and write-only fields. The one deliberate exception is a **custom VPN
  config's own text**, which the authenticated admin can fetch to edit (it is their own uploaded
  config) via `GET /api/custom-vpn/{id}/raw`.
- **Docker socket access:** this tool manages the Docker daemon, so it needs the socket — an
  inherently privileged mount, and the container runs as root by default for that reason (like
  Portainer/dockge). To harden:
  - Put a **Docker socket proxy** (e.g. `tecnativa/docker-socket-proxy`) in front and point
    `DOCKER_HOST` at it, granting only the needed API scopes.
  - Or use **rootless Docker / Podman** (see [docs/PODMAN.md](docs/PODMAN.md)).
  - Keep the dashboard on a trusted network / behind a reverse proxy with TLS.
