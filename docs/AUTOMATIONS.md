# Automations & storage

What GluetunWeb does **on its own** — on a timer, in the background, or reactively — and **where it
keeps state**. Nothing here needs configuring to work; this is a reference for understanding and
operating the app. Intervals in parentheses are the defaults and where to change them.

---

## Background services (server-side, on a timer)

Two hosted services run for the life of the dashboard process.

### 1. Container reconciler — self-healing after external recreates

`Services/ContainerReconcilerService.cs` · **every `GLUETUNWEB_RECONCILE_MINUTES` (default 5 min);
`0` disables · 20 s startup delay**

A container's identity (its Docker id) changes when it is recreated — which is exactly what a
**Watchtower image update** does, and what `docker compose up` does after an image change. The
reconciler keeps the database and the running containers in sync through that:

- Looks each Gluetun container up **by name** (the name is stable across recreates) and **retracks
  its new id** in the database, so logs / start / stop / status keep working.
- Detects a **stale SOCKS5 sidecar** whose network namespace (`network_mode: container:<old-id>`)
  points at a container that no longer exists, and **recreates the sidecar** against the current
  Gluetun container. Also restarts a sidecar that is merely stopped while Gluetun runs.
- Retracks recreated **load-balancer** containers the same way.

It is deliberately conservative — it does **not**:

- resurrect a container you (or something else) genuinely removed;
- restart a connection you stopped;
- touch anything without the `managed-by=gluetunweb` label.

It shares a lock with user actions (see [operation lock](#operation-lock)) so a repair pass can never
land in the middle of a deploy/stop.

> Because a container's environment is fixed at creation, the reconciler relinks and restarts but
> never *reconfigures*. A settings/credential change still needs a **redeploy** to take effect — the
> Credentials page prompts you for exactly that.

### 2. Provider-catalog refresh — keeping provider names current

`Services/ProviderCatalogRefreshService.cs` · **git pull every `GLUETUNWEB_SERVERS_REFRESH_HOURS`
(default 6 h); warm-up at startup**

Valid `VPN_SERVICE_PROVIDER` names, and the country/region/city/hostname options behind the
server-selection cascade, come from a **shallow git clone of
[qdm12/gluetun-servers](https://github.com/qdm12/gluetun-servers)** (`GLUETUNWEB_SERVERS_REPO`). This
service clones it on first use and `git pull`s on the schedule so the list tracks upstream. There is
**no hardcoded fallback**: if the repo is unreachable the catalog is simply empty and the provider
field accepts free-text. Users can also pull on demand with **↻ update list** (Providers page →
`POST /api/providers/catalog/refresh`). Requires `git` in the image (installed by the Dockerfile).

---

## Reactive automations (triggered by an action or a read)

### Real VPN state from Gluetun's control server

`Gluetun/GluetunControlClient.cs` · **cached ~10 s per connection · 3 s request timeout**

Docker only knows whether the container *process* is up — Gluetun retries a failed tunnel internally
rather than exiting, so a broken connection still shows `running` to Docker. So whenever the
connections list is rendered, the dashboard also queries each connection's Gluetun **control server**
(container port `8000`):

| Endpoint | Used for |
| --- | --- |
| `GET /v1/vpn/status` | VPN process state |
| `GET /v1/publicip/ip` | exit IP, city, country |
| `GET /v1/portforward` | forwarded port (falls back to `/v1/openvpn/portforwarded` on old images) |

Results are cached ~10 s so the polling list doesn't hammer the control server, and any failure
degrades to *"vpn unknown"* rather than breaking the page. The status badge keys off the **public
IP** (obtained only after a real connection), not `vpn/status`, because the latter reports `running`
even mid-`AUTH_FAILED`. Full detail:
[ENVVARS → Control server](ENVVARS.md#control-server-how-the-dashboard-reads-real-vpn-state).

### Reaching a container's ports (host / gateway / same-network)

`Services/HostEndpointResolver.cs` · **2 s per probe · cached per container id**

The control-server reads and the **test** button need to reach a managed container's port, but the
right address depends on where the dashboard runs. Rather than assume, the resolver **probes
candidates and uses the first that accepts a TCP connection**:

1. `localhost:<published>` — dashboard on the host;
2. `<default-gateway>:<published>` — dashboard in a container (the gateway *is* the host; needs no
   config);
3. `host.docker.internal:<published>` — same, when the `host-gateway` mapping is present;
4. `<container-ip>:<internal>` — dashboard on the same Docker network as the target.

This is what makes the connectivity test and live status work when GluetunWeb itself runs as a
container on its own Compose network (where the Gluetun container's own IP is unreachable). The
Compose file adds `host.docker.internal:host-gateway` as a belt-and-suspenders path.

### Connectivity test

`Services/ProxyTester.cs` — the **test** button fetches a URL (default `https://ipwho.is/`) through a
connection's SOCKS5 proxy (or a balancer's listener). It first checks the tunnel is actually up (via
the control server) and TCP-preflights the proxy, so a failure names the real cause instead of a
blind 20 s timeout. It reports the verdict, timing, and the raw response.

### Auto port-block assignment

`Services/PortManager.cs` + `Services/PortLayout.cs` — each connection reserves one contiguous,
aligned **block** of host ports (8 per connection, 4 per balancer) at create time; each purpose sits
at a **fixed offset** inside it, so toggling a proxy never moves the others. A block is skipped if any
port in it is already held by another owner **or currently published by a live Docker container**
(checked against the daemon, so it won't collide with containers GluetunWeb doesn't manage).
See [ENVVARS → Host ports](ENVVARS.md#host-ports--fixed-blocks).

### DNS resolution of custom-VPN endpoints

`Gluetun/EndpointResolver.cs` — Gluetun requires an IP, not a hostname. For a custom config using the
`{{DNS_IP}}` placeholder, GluetunWeb resolves the configured endpoint DNS name and substitutes the IP
**just before the container starts** (so a changed DNS record is picked up on the next deploy).

### Operation lock

`Services/ContainerOperationLock.cs` — a single process-wide gate serialises all container lifecycle
work (deploy / start / stop / restart / delete) against the reconciler, so a background repair can
never interleave with a user action.

### Orphan detection

The Connections page lists containers carrying the `managed-by=gluetunweb` label that no longer match
any connection (e.g. left by a failed delete) and offers to remove them — removal is label-verified,
so it can never touch unrelated containers.

---

## Frontend automations (in the browser)

| What | Interval | Where |
| --- | --- | --- |
| Connections list refresh | 8 s | Connections page |
| Load-balancers list refresh | 8 s | Load Balancers page |
| Open **logs** modal tail refresh | 3 s | Connections / Load Balancers |
| Docker online/offline indicator | 15 s | sidebar (Layout) |
| **Landing decision** | once at login | `/` → guide if no connections, else connections |

---

## Where information is stored

| What | Location | Notes |
| --- | --- | --- |
| All configuration (settings, credentials, providers, custom configs, connections, balancers, the admin account) | **SQLite** at `GLUETUNWEB_DB_PATH` (compose: `/data/gluetunweb.db`, on a named volume) | The one file to back up. Tables: `AdminUsers`, `GlobalSettings`, `Credentials`, `Providers`, `CustomVpnConfigs`, `Connections`, `LoadBalancers`, `LoadBalancerUpstreams`. |
| **Secrets** (provider/credential passwords & keys, tokens, proxy & Shadowsocks passwords, raw custom-config text) | inside that SQLite DB, **AES-256-GCM encrypted** with a key derived from `GLUETUNWEB_MASTER_KEY` | Never returned to the browser (only `has*` flags). **Back up the master key** — losing/changing it makes stored secrets unreadable. |
| Admin **password** | that SQLite DB, PBKDF2-hashed (`PasswordHasher<T>`) | Not reversible; not a secret to decrypt. |
| Provider-name **catalog** | a git clone at `GLUETUNWEB_SERVERS_PATH` (default `<data-dir>/gluetun-servers`) | Reproducible — safe to delete; re-cloned on next use. |
| Injected **container config** (control-server `config.toml`, `custom.ovpn`, balancer `config.json`) | a **named Docker volume per resource** — `gluetunweb-<id>-conf` (`/gluetunweb`) for a connection, `gluetunweb-lb-<id>-conf` (`/conf`) for a balancer | Not host bind mounts, so config **survives container recreation** (Watchtower). Removed when the resource is deleted. |
| **Button-style** display preference | the browser's `localStorage` (`gluetunweb.buttonStyle`) | Per-browser, never sent to the server. |
| Session | HttpOnly, `SameSite=Strict` cookie | 7-day sliding expiry. |

### Backup, in one line

Back up the **SQLite database** and the **`GLUETUNWEB_MASTER_KEY`** — together they are the entire
state. The catalog clone and the per-resource config volumes are regenerated automatically (the
volumes on the next deploy of each connection/balancer).

---

## Quick knobs

| Variable | Default | Effect |
| --- | --- | --- |
| `GLUETUNWEB_RECONCILE_MINUTES` | `5` | Reconciler interval; `0` disables self-healing. |
| `GLUETUNWEB_SERVERS_REFRESH_HOURS` | `6` | Provider-catalog git-pull interval. |
| `GLUETUNWEB_SERVERS_REPO` / `_PATH` | qdm12/gluetun-servers · `<data-dir>/gluetun-servers` | Catalog source and clone location. |

See [ENVVARS.md](ENVVARS.md#dashboard-runtime-set-on-the-gluetunweb-container) for the full runtime
variable list.
