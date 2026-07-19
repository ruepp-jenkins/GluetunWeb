# Environment Variable Reference

This is the authoritative catalog of the Gluetun (and dashboard) settings GluetunWeb manages. The
same descriptions and examples are shown **inline in the UI** (never as tooltips) via
`src/web/src/data/envCatalog.ts` — keep the two in sync.

Two kinds of variables appear below:

- **Dashboard runtime** — configure the GluetunWeb container itself (set on the dashboard).
- **Gluetun container** — written by GluetunWeb onto each Gluetun container it creates, based on
  your Global Settings, Provider, Custom config, and per-connection overrides.

---

## Dashboard runtime (set on the GluetunWeb container)

| Variable | Description | Example |
| --- | --- | --- |
| `GLUETUNWEB_MASTER_KEY` | **Required.** Passphrase used to derive the AES-256-GCM key that encrypts stored secrets. Losing/changing it makes previously stored secrets unreadable. | `openssl rand -base64 32` |
| `GLUETUNWEB_DB_PATH` | Path to the SQLite database file. | `/data/gluetunweb.db` |
| `DOCKER_HOST` | Docker/Podman endpoint. Overridden by **Global Settings → Docker host override**. Empty ⇒ mounted socket. | `tcp://10.0.0.5:2375` |
| `ASPNETCORE_URLS` | Bind address for the dashboard web server. | `http://+:8080` |
| `TZ` | Timezone for the dashboard process logs. | `Europe/Berlin` |
| `GLUETUNWEB_SERVERS_REPO` | Git repo for the provider-name catalog. | `https://github.com/qdm12/gluetun-servers.git` |
| `GLUETUNWEB_SERVERS_PATH` | Where the catalog is cloned. Defaults to `<data-dir>/gluetun-servers`. | `/data/gluetun-servers` |
| `GLUETUNWEB_SERVERS_REFRESH_HOURS` | How often the catalog is auto-refreshed (git pull). | `6` |
| `GLUETUNWEB_RECONCILE_MINUTES` | How often the container reconciler runs (`0` disables it). | `5` |

**Provider catalog:** valid `VPN_SERVICE_PROVIDER` names come from cloning
`GLUETUNWEB_SERVERS_REPO` and reading `pkg/servers/*.json` (each filename without `.json` is a valid
provider). The catalog is cloned at startup and **git-pulled automatically every
`GLUETUNWEB_SERVERS_REFRESH_HOURS`**. `GET /api/providers/catalog` returns the list;
`POST /api/providers/catalog/refresh` pulls on demand. Requires `git` in the image (installed in the
Dockerfile) and network access — there is **no hardcoded list**: if the repo is unreachable the
catalog is empty and the provider field simply accepts free-text.

---

## Global Settings → Gluetun env

| Variable | Description | Example |
| --- | --- | --- |
| `TZ` | IANA timezone for container logs/scheduling. | `Europe/Berlin` |
| `PUBLICIP_API` | Service Gluetun queries for the tunnel's public IP: `ipinfo`, `ip2location`, `cloudflare`. | `ipinfo` |
| `PUBLICIP_API_TOKEN` | Optional token for the public-IP service (higher rate limits). Stored encrypted. | `a1b2c3d4e5` |
| `HTTPPROXY` | Enables Gluetun's built-in HTTP proxy (`on`/`off`). Toggled per connection. | `on` |
| `HTTPPROXY_USER` | Optional HTTP proxy username. | `proxyuser` |
| `HTTPPROXY_PASSWORD` | Optional HTTP proxy password. Stored encrypted. | `s3cr3t` |
| `HTTPPROXY_LISTENING_ADDRESS` | Address:port the HTTP proxy binds inside the container. | `:8888` |
| `HTTPPROXY_STEALTH` | Removes proxy-identifying headers (`on`/`off`). | `on` |
| `HTTPPROXY_LOG` | Logs each HTTP proxy request (`on`/`off`). | `off` |
| `HTTP_CONTROL_SERVER_AUTH_CONFIG_FILEPATH` | Path to the generated control-server auth file. GluetunWeb injects a `config.toml` at `/gluetun/auth/config.toml` when auth is `basic` or `apikey`. | `/gluetun/auth/config.toml` |

### Control-server `config.toml`

When control-server auth is **basic** or **apikey**, GluetunWeb generates and injects a role file:

```toml
[[roles]]
name = "gluetunweb"
routes = ["GET /v1/publicip/ip", "GET /v1/vpn/status", "PUT /v1/vpn/status", ...]
auth = "apikey"          # or "basic"
apikey = "FdaMDnTs9fiqSqkNQ4RKH7"   # or username/password for basic
```

Generate an API key from the UI (**Generate**), which mirrors `docker run --rm qmcgaw/gluetun
genkey` (a 22-char Base58 value).

---

## Credentials (shared secrets)

One VPN account usually backs several provider entries — `nordvpn-nl`, `nordvpn-ch` and so on all use
the same login. A **credential** stores that login once under a name; providers and custom configs
then reference it instead of repeating it.

Credentials are **typed**: an OpenVPN one holds a username/password, a WireGuard one holds the
private key, optional pre-shared key and addresses. A provider only offers credentials matching its
own VPN type, because an OpenVPN provider handed WireGuard keys would deploy with nothing usable and
fail only at connect time.

**Precedence is replace, not merge.** When a credential is selected it supplies *every* secret and
the entry's own fields are ignored — so there is never a question of which half won. Leave the
selection on *enter directly below* to keep secrets on the provider itself.

Two consequences worth knowing:

- Editing a credential changes every provider that references it, applied on their **next deploy**.
  Rotating a password is therefore a single edit rather than one per country.
- A credential in use cannot be deleted, and its VPN type is locked while referenced. Both are
  reported with the number of dependants rather than failing silently.

### Editing a credential that is in use

A container's environment is fixed when it is **created**, so a running connection keeps the old
secrets until it is recreated. Critically, **restart does not help** — it reuses the same container:

| Action | Container | Secrets |
| --- | --- | --- |
| Edit the credential | untouched, still running | old |
| Start / stop / **restart** | same container id | **old** |
| **Deploy / redeploy** | new container id | new |

A Watchtower image update behaves like a restart here: it recreates the container from its stored
configuration, carrying the old environment forward.

So after saving a credential that connections depend on, GluetunWeb lists them and asks whether to
redeploy. Only **running** connections are offered — deploying a stopped one would also start it,
which is rarely what you want, so those are listed separately and pick the change up on their next
deploy. Redeploys run one at a time, so only one tunnel is interrupted at a time, each for a few
seconds; per-connection success or failure is reported as it goes.

Custom VPN configs can reference **OpenVPN credentials only** — a WireGuard custom config carries its
keys inside the config text, so there is nothing for a credential to supply.

As everywhere else, secrets are AES-encrypted at rest and never returned to the browser; the API
exposes only `has*` flags plus the OpenVPN username, which is not a key and makes a credential
recognisable when picking one.

---

## Providers → Gluetun env

| Variable | Description | Example |
| --- | --- | --- |
| `VPN_SERVICE_PROVIDER` | Gluetun provider identifier (exact spelling matters). | `mullvad` |
| `VPN_TYPE` | `wireguard` or `openvpn`. | `wireguard` |
| `OPENVPN_PROTOCOL` | Transport for OpenVPN — `udp` (default) or `tcp`. Only emitted for OpenVPN providers; WireGuard is UDP-only and has no equivalent option. | `udp` |
| `OPENVPN_USER` | OpenVPN account username. | `p1234567` |
| `OPENVPN_PASSWORD` | OpenVPN account password. Stored encrypted. | `••••••••` |
| `WIREGUARD_PRIVATE_KEY` | WireGuard client private key (base64). Stored encrypted. | `wOEI9rqq…=` |
| `WIREGUARD_PRESHARED_KEY` | Optional WireGuard pre-shared key. Stored encrypted. | `K5f2…=` |
| `WIREGUARD_ADDRESSES` | Tunnel interface address(es). | `10.64.0.2/32` |
| `SERVER_REGIONS` | Region filter (provider-specific). | `Europe` |
| `SERVER_COUNTRIES` | Country filter (comma-separated). | `Sweden,Netherlands` |
| `SERVER_CITIES` | City filter. | `Amsterdam` |
| `SERVER_HOSTNAMES` | Exact server hostnames to pin. | `se-sto-wg-001` |

### Server selection is a cascade

The four fields appear only once a **provider type** is chosen, and every value is read from that
provider's entry in the [gluetun-servers](https://github.com/qdm12/gluetun-servers) catalog — so a
dropdown never offers something the provider does not have.

They narrow **top to bottom**:

```
provider → region → country → city → hostname
```

Each level is filtered by the levels *above* it only. Picking `Europe` cuts NordVPN's countries from
137 to 51 and its cities from 211 to 62; adding `Sweden` cuts cities to 1 and hostnames to 165. Its
own level stays complete, so you can still switch to a different country without starting over.

**Changing a level clears everything below it.** A city left over from a previous country would match
no server, and Gluetun only reports that at connect time as an opaque *"no server found"*.

Two further refinements:

- **VPN type filters every level.** An OpenVPN provider entry never offers WireGuard-only hostnames —
  asking Mullvad for OpenVPN correctly yields nothing, since it ships WireGuard servers only.
- **Transport support follows the narrowing.** `OPENVPN_PROTOCOL` options are enabled from the
  *currently selected* servers, so a country that only has UDP servers disables TCP even when the
  provider as a whole supports it.

Hostname lists are capped at 500 (NordVPN has ~9k); the field says so and asks you to narrow by
country or city. Free-text entry still works throughout, and an unlisted provider is left
unconstrained rather than blocked.

Per-connection **overrides** for countries/cities/hostnames replace the provider values at deploy
time.

### Choosing UDP vs TCP

UDP is faster and is Gluetun's default. Pick **TCP** when UDP is blocked — restrictive corporate or
hotel networks, some ISPs, and deep-packet-inspection setups.

**Not every provider offers both.** Gluetun filters its server list by `OPENVPN_PROTOCOL`, so asking
for a transport no server supports fails at connect time with an opaque *"no server found"*. The
provider form reads the per-server `tcp`/`udp` flags out of the
[gluetun-servers](https://github.com/qdm12/gluetun-servers) catalog it already clones, disables the
unsupported option, and switches your selection with a note if you pick one the provider lacks. At
the time of writing, 6 of the 24 catalogued providers are **UDP-only** (expressvpn, giganews,
ipvanish, privado, vpn unlimited, vyprvpn), and mullvad has no OpenVPN servers at all.

Custom VPN configs are unaffected: an uploaded `.ovpn` carries its own `proto` line, so GluetunWeb
does not emit `OPENVPN_PROTOCOL` for them.

---

## Custom VPN → Gluetun env

| Variable | Description | Example |
| --- | --- | --- |
| `VPN_SERVICE_PROVIDER` | Always `custom` for custom configs. | `custom` |
| `OPENVPN_CUSTOM_CONFIG` | Path to the injected `.ovpn` file (OpenVPN customs). | `/gluetun/custom.ovpn` |
| `OPENVPN_USER` / `OPENVPN_PASSWORD` | Optional OpenVPN credentials for the custom config (many providers require `auth-user-pass`). Stored encrypted. | `user123` |
| `WIREGUARD_PRIVATE_KEY` / `WIREGUARD_ADDRESSES` / `WIREGUARD_PUBLIC_KEY` / `WIREGUARD_PRESHARED_KEY` / `WIREGUARD_ENDPOINT_IP` / `WIREGUARD_ENDPOINT_PORT` | Parsed from an uploaded/pasted WireGuard `.conf` at deploy time. | — |

OpenVPN custom configs are injected as a file into the container (via the Docker copy API — no host
bind mounts). WireGuard configs are parsed into the `WIREGUARD_*` variables above.

**Config content** can be uploaded (`.ovpn`/`.conf`) or pasted, and stays freely editable in the UI
(the decrypted text is loaded for the authenticated admin when editing).

**DNS endpoints:** Gluetun requires an **IP**, not a hostname. Put the `{{DNS_IP}}` placeholder where
the endpoint IP belongs and set an **endpoint DNS name** — GluetunWeb resolves the name and
substitutes the IP into the config **just before** the container starts:

```
# OpenVPN
remote {{DNS_IP}} 1194
# WireGuard
Endpoint = {{DNS_IP}}:51820
```

---

## SOCKS5 sidecar (serjs/go-socks5-proxy)

Set per connection. When a username **and** password are provided the sidecar runs with
authentication; otherwise it runs open.

| Variable | Description | Example |
| --- | --- | --- |
| `PROXY_USER` | SOCKS5 username. | `proxyuser` |
| `PROXY_PASSWORD` | SOCKS5 password. Stored encrypted. | `s3cr3t` |
| `REQUIRE_AUTH` | Set to `true` when credentials are provided, else `false` (open proxy). GluetunWeb sets this automatically. | `false` |

---

## Port forwarding & firewall

Set **per connection**.

| Variable | Default | Description |
| --- | --- | --- |
| `VPN_PORT_FORWARDING` | `off` | Request an inbound port from the provider. Supported by **PIA, ProtonVPN, PrivateVPN, Perfect Privacy** only. |
| `VPN_PORT_FORWARDING_PROVIDER` | current provider | Override, mainly for custom configs. |
| `VPN_PORT_FORWARDING_PORTS_COUNT` | `1` | Ports to forward; up to 5 on ProtonVPN. Omitted when 1 (Gluetun's default). |
| `FIREWALL_VPN_INPUT_PORTS` | | Ports to accept from the VPN side. A forwarded port is useless if the firewall still drops it. |
| `FIREWALL_OUTBOUND_SUBNETS` | | LAN subnets reachable from the container and anything sharing its namespace. |
| `WIREGUARD_MTU` | | Lower it (1420/1380/1280) when the tunnel connects but large transfers stall. |

The forwarded port is **assigned by the provider and changes on reconnect**, so it is read back from
the control server and shown on the connection row rather than stored.

`FIREWALL_OUTBOUND_SUBNETS` is the fix for "I can't reach my NAS through the proxy": once the tunnel
is up, Gluetun's firewall drops traffic to local addresses unless they are listed here.

---

## Control server (how the dashboard reads real VPN state)

Docker only reports whether the *process* is up. Gluetun retries internally instead of exiting, so a
connection that has never established still shows `running` to Docker. GluetunWeb therefore queries
Gluetun's own HTTP control server per connection:

| Endpoint | Used for |
| --- | --- |
| `GET /v1/vpn/status` | VPN process state |
| `GET /v1/publicip/ip` | Exit IP, city and country |
| `GET /v1/portforward` | Currently forwarded port (falls back to `/v1/openvpn/portforwarded` on older images) |

Results are cached for ~10s so the polling connections list doesn't hammer the control server, and a
failure degrades to "vpn unknown" rather than breaking the page.

### How GluetunWeb reaches these ports

A managed container's control server and proxies bind **two** addresses: the port *inside* the
container (e.g. `8000`, `1080`) on the container's own IP, and the auto-assigned **published** port
on the host. Which one GluetunWeb can use depends on where it runs:

| GluetunWeb runs… | Reachable address |
| --- | --- |
| On the host | `localhost:<published>` |
| In a container, **same** Docker network | the container's IP `:<internal>` |
| In a container, **different** network (the compose default) | the host, via its default gateway or `host.docker.internal`, on `:<published>` |

The last row is the common one and the easy one to get wrong: under `docker compose` GluetunWeb sits
on its own network while the Gluetun containers it creates sit on the default bridge, so the
container IP is **not** reachable — only the published port on the host is. GluetunWeb therefore
**probes** the candidates and uses the first that accepts a connection (results cached per container),
rather than assuming one. The compose file maps `host.docker.internal:host-gateway` as a belt-and-
suspenders path, but the default-gateway route works without it. This applies equally to the status
readout and to the **test** button.

### Why the badge keys off the public IP

`/v1/vpn/status` returns `{"status":"running"}` for a VPN process that is **stuck retrying** — it was
observed reporting `running` throughout an `AUTH_FAILED` loop. The public IP is only fetched *after* a
successful connection, so it is the trustworthy signal:

| Shown | Meaning |
| --- | --- |
| **vpn up** (green) | Public IP present — the tunnel really works |
| **connecting** (amber) | Process running, no public IP yet — still connecting, or failing auth |
| **vpn stopped/crashed** (red) | Gluetun reports a non-running state |
| **vpn unknown** (amber) | Control server unreachable |

### Authentication is required, even for "none"

Current Gluetun **denies every control-server route that no role covers** — its image default is
`HTTP_CONTROL_SERVER_AUTH_DEFAULT_ROLE={}`, an empty role. Omitting the config file therefore does
not mean "open", it means every request returns `Unauthorized`, including the dashboard's own reads.

So GluetunWeb **always** injects `config.toml` and sets
`HTTP_CONTROL_SERVER_AUTH_CONFIG_FILEPATH`, including when auth is set to **none** — in which case
the role is written with `auth = "none"` limited to the routes above. Selecting `basic` or `apikey`
additionally requires those credentials, which the dashboard sends on its own requests.

> Note that with `none`, anyone who can reach the published control port can read the exit IP and
> start/stop the VPN. Use `basic` or `apikey` if that port is exposed beyond a trusted network.

---

## DNS filtering (Gluetun built-in)

Set **per connection**. Gluetun runs an internal DNS server (DNS-over-TLS to Cloudflare by default)
and can drop lookups against its block lists. Defaults mirror Gluetun's own.

| Variable | Default | Description |
| --- | --- | --- |
| `BLOCK_MALICIOUS` | `on` | Block known malicious hostnames and IPs. Little reason to disable. |
| `BLOCK_ADS` | `off` | Block ad hostnames. More false-positive prone than malicious blocking — it can break sites that serve content from ad domains. |
| `DNS_UNBLOCK_HOSTNAMES` | | Comma-separated hostnames exempted from the lists above. |

`BLOCK_MALICIOUS` and `BLOCK_ADS` are **always written** to the container rather than omitted when
off, so turning a toggle back off actually reverts it instead of inheriting whatever the image
defaults to.

> Gluetun's old `BLOCK_SURVEILLANCE` option is not exposed: it is obsolete upstream (its block lists
> are no longer maintained), so it does nothing but log a warning. Use malicious + ads for filtering.

**It only covers lookups made through the connection.** The filtering happens at Gluetun's DNS
server, so a client that resolves a name locally and then opens a connection through the proxy has
already bypassed it. Use `socks5h://` rather than `socks5://` so resolution happens at the proxy
end. Containers wired up with the tun2socks sidecar get it automatically, since all their traffic —
DNS included — enters the tunnel.

> **Upgrading:** `BLOCK_MALICIOUS` backfills to **on** for existing connections, matching what
> Gluetun was already doing. The other two backfill to off. No existing connection changes behavior.

---

## Shadowsocks (Gluetun built-in)

Set **per connection**. Unlike SOCKS5, this is not a sidecar — Gluetun runs the Shadowsocks server
itself inside the VPN container, so there is no extra image to pull and it shares the tunnel directly.

| Variable | Description | Example |
| --- | --- | --- |
| `SHADOWSOCKS` | Enables the built-in Shadowsocks server (`on`/`off`). | `on` |
| `SHADOWSOCKS_PASSWORD` | Password clients must present. **Required** — see below. Stored encrypted. | `s3cr3t` |
| `SHADOWSOCKS_CIPHER` | AEAD cipher: `chacha20-ietf-poly1305` (default), `aes-128-gcm`, `aes-256-gcm`. Must match the client. | `chacha20-ietf-poly1305` |
| `SHADOWSOCKS_LISTENING_ADDRESS` | Internal listening address. GluetunWeb pins this to `:8388` and publishes an auto-assigned host port. | `:8388` |
| `SHADOWSOCKS_LOG` | Logs Shadowsocks activity (`on`/`off`). | `off` |

**A password is mandatory.** Shadowsocks has no anonymous mode, and the port is published on the
host — an unauthenticated server would be an open relay. The UI rejects enabling it without one.

**TCP *and* UDP.** Shadowsocks serves both on the same port, so GluetunWeb publishes the assigned
host port twice — `<port>/tcp` and `<port>/udp`. Point your client at the host port shown in the
**SS** column of the connections table.

---

## Config persistence (named volumes)

Injected config is written to a **named Docker volume** per connection/balancer, not into the
container's writable layer — so it survives container recreation (e.g. a Watchtower image update),
which discards that layer.

| Resource | Volume | Mounted at | Contents |
| --- | --- | --- | --- |
| Connection | `gluetunweb-<identifier>-conf` | `/gluetunweb` | `config.toml` (control-server auth), `custom.ovpn` (custom OpenVPN) |
| Load balancer | `gluetunweb-lb-<identifier>-conf` | `/conf` | `config.json` |

The mount paths deliberately avoid `/gluetun` and `/app` so they don't shadow the images' own
contents. The balancer is started as `./Socks5BalancerAsio /conf/config.json` so it reads the config
from the volume. Volumes are labeled `managed-by=gluetunweb` and are removed when the connection or
balancer is deleted.

### Self-healing after an external recreate

A SOCKS5 sidecar joins its Gluetun container's network namespace via `network_mode: container:<id>`,
and Docker stores the **resolved container id**. If something outside the dashboard recreates the
Gluetun container (a Watchtower image update), the new container gets a new id and the sidecar is left
bound to a namespace that no longer exists.

A background **reconciler** (`GLUETUNWEB_RECONCILE_MINUTES`, default 5) repairs this: it looks the
Gluetun container up by name, retracks the new id, and recreates/restarts the sidecar against it. It
also retracks recreated load-balancer containers. It is deliberately conservative — it never
resurrects containers you removed, and never restarts a connection you stopped (container lifecycle
work and reconciliation share a lock, so a repair pass can't race a user action).

## Ownership labels (on every created container)

Every Gluetun container and SOCKS5 sidecar is stamped with these labels so the tool can detect what
it owns. The **Connections** page surfaces any labeled container that no longer matches a known
connection (an "orphan") and lets you remove it from Docker — removal is refused for containers
lacking the label.

| Label | Value |
| --- | --- |
| `managed-by` | `gluetunweb` |
| `gluetunweb.connection` | the connection identifier |

## Load balancer (Socks5BalancerAsio)

A load balancer is a `ruepp/socks5balancerasio` container whose `config.json` is generated from the
balancer settings + selected connections and injected at `/app/config.json`. Container-internal
ports are fixed; the host publishes them on auto-assigned ports.

Load balancers draw their host ports from a **separate** range (default `30000`–`31000`), configured
in Global Settings independently of the connection range (default `20000`–`21000`).

| Setting | config.json key | Description | Example |
| --- | --- | --- | --- |
| Upstream host | `upstream[].host` | Host used to reach each connection's SOCKS5 port. | `host.docker.internal` |
| Upstreams | `upstream[]` | Selected SOCKS5-enabled connections → `{name, host, port, authUser, authPwd}`. | — |
| Select rule | `upstreamSelectRule` | `loop` / `random` / `one_by_one` / `change_by_time`. | `loop` |
| Retry times | `retryTimes` | Upstreams tried before failing. | `3` |
| Connect timeout | `connectTimeout` | Upstream connect timeout (ms). | `2000` |
| Health host/port | `testRemoteHost` / `testRemotePort` | Connect-test target. | `www.google.com` / `443` |
| Check periods | `tcpCheckPeriod` / `connectCheckPeriod` / `additionCheckPeriod` | Health-check intervals (ms). | `30000` / `300000` / `10000` |
| Threads | `threadNum` | Worker threads. | `10` |
| Server change time | `serverChangeTime` | Dwell time for time-based selection (ms). | `5000` |

Internal ports (published to auto-assigned host ports):

| Purpose | Container port | Notes |
| --- | --- | --- |
| Balanced SOCKS5 | `15000` (`listenPort`) | Clients connect here. |
| Web UI | `80` (`EmbedWebServerConfig`) | Status dashboard (`stateBootstrap.html`); the UI links to it. |
| State server | `15010` (`stateServerPort`) | JSON state/control API. |

The container is given a `host.docker.internal:host-gateway` extra host so it can reach the host's
published SOCKS5 ports, and is labeled `gluetunweb.loadbalancer=<identifier>`.

## Host ports — fixed blocks

Every connection and every load balancer is assigned **one contiguous block** of host ports, and
inside that block each purpose sits at a **fixed offset**. Enabling or disabling a proxy therefore
never moves any other port: the disabled purpose simply leaves its slot unused. A port a client is
already pointed at stays put for the life of the connection.

The block is reserved when the connection/balancer is **created**, so its ports are visible in the UI
before the first deploy. Blocks are aligned to the start of the configured range.

### Connection block — 8 ports (4 used, 4 spare)

Drawn from the connection range (default `20000`–`21000`), so `20000`, `20008`, `20016`, …

| Offset | Purpose | Container port | Notes |
| --- | --- | --- | --- |
| `+0` | Control server | `8000` | Gluetun HTTP control API (status, public IP). Always allocated. |
| `+1` | SOCKS5 | `1080` | Served by the sidecar sharing the Gluetun network namespace. |
| `+2` | HTTP proxy | `8888` | Gluetun's built-in HTTP proxy. |
| `+3` | Shadowsocks | `8388` | Gluetun's built-in Shadowsocks server; published on **TCP and UDP**. |
| `+4…+7` | *(spare)* | | Headroom so a future proxy can be added without relocating anything. |

A connection whose block starts at `20008` therefore always has its control server on `20008`, SOCKS5
on `20009`, HTTP proxy on `20010` and Shadowsocks on `20011`.

### Load-balancer block — 4 ports (3 used, 1 spare)

Drawn from the separate balancer range (default `30000`–`31000`), so `30000`, `30004`, `30008`, …

| Offset | Purpose | Notes |
| --- | --- | --- |
| `+0` | Balanced SOCKS5 listener | The endpoint clients connect to. |
| `+1` | Web UI | Socks5BalancerAsio's status dashboard. |
| `+2` | State/backend | Queried by the web UI (passed as `?backend=`). |
| `+3` | *(spare)* | |

### Choosing a block

A block is skipped if any port in it is already held by another connection/balancer **or** is
currently published by a live Docker container — including containers GluetunWeb does not manage. If
no whole block is free, the deploy fails with a clear message rather than colliding; widen the range
in **Global Settings** or remove unused connections.

> **Upgrading:** before fixed blocks, ports were handed out one at a time and compacted when a proxy
> was disabled. On upgrade every connection and balancer starts with **no block** and is assigned one
> on its next deploy, so **existing ports will change** — update any client configs pointing at them.
> Connections still running on their old ports are protected while they run: those ports show up as
> live and are never handed to someone else's block.
