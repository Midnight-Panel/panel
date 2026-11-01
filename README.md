# Midnight Panel

Open-source panel for hosting game and app servers on Docker. This repository contains the web panel
(ASP.NET Core API in C# + React/TypeScript frontend based on [TailAdmin](https://tailadmin.com)). The node demon lives in
[Midnight-Panel/demon](https://github.com/Midnight-Panel/demon).

> Status: **Phase 2** – nodes, allocations and the demon connection (status, system info, logs, boot sync).

## Installation

On a fresh Linux server (Debian, Ubuntu, RHEL family) as root:

```bash
bash <(curl -fsSL https://get.midnightpanel.xyz)
```

The [installer](https://github.com/Midnight-Panel/installer) runs the panel as Docker Compose stack (panel, PostgreSQL,
Caddy with automatic HTTPS) and installs the `midnight` management CLI.

### Midnight CLI Commands (`midnight`)

| Command | Description |
|---|---|
| `midnight install [panel\|demon\|both]` | Run pre-flight checks (OS, RAM, disk, ports, firewall) and install components with repair/reconfigure support. |
| `midnight status [--json]` | View real-time container states, database health, demon status, and disk space (or emit JSON for monitoring). |
| `midnight backup [dir] [--encrypt] [--keep N]` | Dump PostgreSQL, `.env`, storage & state into `.tar.gz` (permissions 600) with SHA-256 and optional AES-256 encryption. |
| `midnight restore <archive>` | Verify checksum, inspect manifest, create pre-restore safety backup, stop stack, restore DB & storage, restart and health-check. |
| `midnight update [target] [--use-images]` | Pre-update safety backup, update via GHCR images or source build, post-update health check with **automatic rollback** on failure. |
| `midnight rollback [target]` | Revert to previous version and restore database dump from pre-update backup. |
| `midnight doctor` | Deep diagnostic audit: pre-flight checks, verify strict 600 permissions on secrets, Docker daemon, SSL and logs. |
| `midnight logs [target] [-f] [--tail N]` | Inspect logs for `panel`, `db`, `caddy`, `demon`, or `installer`. |
| `midnight completion [bash\|zsh]` | Generate shell tab-completion scripts. |

## Requirements

- .NET SDK 10.0
- Node.js 20+ (the frontend is built automatically during `dotnet build`, `-p:SkipWebBuild=true` skips it)

The demon references `src/Midnight.Shared` from this repository, so both repositories are expected next to each other:

```
midnight-panel/
├── panel/   # this repository
└── demon/   # demon repository
```

## Development

```bash
dotnet run --project src/Midnight.ApiServer
```

The panel listens on <http://localhost:5220> and serves the built frontend from `src/Midnight.ApiServer/wwwroot`.

For frontend work with hot reload, run the Vite dev server next to the api (it proxies `/api` to port 5220):

```bash
cd src/Midnight.Web && npm run dev
```

Then open <http://localhost:5173>. On first start it creates `src/Midnight.ApiServer/storage/`
with `config.json` (including a generated JWT secret), the SQLite database, data protection keys and logs.
The first registered account becomes administrator.

- API reference: <http://localhost:5220/api/reference>
- OpenAPI document: <http://localhost:5220/api/openapi/v1.json>

Tests:

```bash
dotnet test Midnight.Panel.slnx
```

## Configuration

`Security:TrustedProxies` lists networks (CIDR) whose `X-Forwarded-*` headers are trusted, e.g. the Docker network of a
reverse proxy. `docker run ... create-admin --email <email>` creates an administrator, the password comes from
`MIDNIGHT_ADMIN_PASSWORD`. `GET /api/health` reports the database connection.

`storage/config.json` can be edited directly. Every value can be overridden with environment variables
prefixed with `MIDNIGHT_`, e.g. `MIDNIGHT_Database__Provider=PostgreSQL` and
`MIDNIGHT_Database__ConnectionString=Host=db;Database=midnight;Username=midnight;Password=...`.
`MIDNIGHT_STORAGE` moves the storage directory.

### Migrations

Each database provider has its own migrations:

```bash
dotnet ef migrations add <Name> --project src/Midnight.ApiServer --context SqliteDataContext --output-dir Migrations/Sqlite
dotnet ef migrations add <Name> --project src/Midnight.ApiServer --context PostgresDataContext --output-dir Migrations/Postgres
```

## Security model (phase 1)

- Passwords: Argon2id (PHC format, parameters can be raised later and hashes are upgraded on login).
- Access tokens: HMAC-signed JWT, 10 minutes, kept in memory by the client. Every request checks the user's
  lock state and security stamp, so locking, password changes and "logout everywhere" apply immediately.
- Refresh tokens: random, stored as SHA-256 hash, rotated on every use, delivered as `HttpOnly`,
  `SameSite=Strict` cookie restricted to `/api/auth`.
- 2FA: TOTP (RFC 6238), secret encrypted with ASP.NET Data Protection, replayed codes are rejected.
- API keys: `mdp_<prefix>_<secret>`, only the hash is stored. Effective rights = owner permissions ∩ key scopes.
  Security actions (password, 2FA, sessions, API keys) are never possible with an API key or while impersonating.
- Permissions: dot separated strings with wildcards (`admin.*`, `*`). Assigning roles requires `admin.roles.write`.
- Impersonation: short lived token without refresh, only for users with no more permissions than the admin.
- Rate limiting on login, register and password reset (per IP, configurable).
- Admins see and can revoke every active login session (`/admin/users/sessions`).
- Node tokens: `mdn_<tokenId>.<token>`, the secret is stored encrypted with Data Protection. The same token
  authenticates panel → demon and demon → panel (`/api/nodes/remote/*`). The setup page needs `admin.nodes.write`.

## Open items

Things that are intentionally not done yet and marked with `// TODO:` in the code:

- [ ] SMTP mail transport – password reset mails are written to the log for now (`LogMailSender`)
- [ ] Real server values on the dashboards and node status list on the admin overview (phase 3)
- [ ] Theme editor and module/plugin loading (phase 7), `IPanelPlugin` is only defined
- [ ] CI for the panel (the installer has a release workflow)

## Roadmap

1. ~~Scaffolding, layout, auth with JWT and 2FA, users, permissions~~
2. ~~Demon base, nodes, allocations~~
3. Images (eggs) and servers, installation, power actions
4. Live console and stats over WebSocket
5. File manager, SFTP
6. Backups, schedules, networks, variables
7. Admin extras, module and plugin system
8. Polish, docker, CI

## UI

`src/Midnight.Web` is based on the free TailAdmin React template (Vite, React 19, TypeScript, Tailwind CSS v4,
Base UI) and keeps its components and colors. On top: React Router, TanStack Query, react-hook-form and i18next.
It talks to the api only over REST; the DTOs are mirrored in `src/api/types.ts`.
Highlights: light/dark mode, page search (Ctrl/Cmd + K), English and German translations (`src/messages`).
Page structure follows Moonlight Panel v2: dashboard, account (overview, security, API keys) and admin areas with
tab navigation (users: overview, sessions, roles).

## License

MIT. Third party: TailAdmin React (MIT, see `src/Midnight.Web/LICENSE.md`), React, Base UI (MIT), Outfit font (SIL Open Font License).
