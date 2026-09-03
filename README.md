# LogBackup

**English** | [繁體中文](./README.zh-TW.md)

A cross-platform (Windows / Linux / macOS) log backup, encryption, integrity-verification, and restoration tool, implemented in .NET 8 (C#).

Built from the specification in [`Skill.md`](./Skill.md): securely archive application/system logs while preserving their integrity, and let authorized personnel restore them back to readable plaintext when needed.

## Core design principle

The tool strictly separates two concerns that must never be confused:

- **Encryption**: protects the confidentiality of the backup artifact using AES-256-GCM (authenticated encryption). It is reversible by an authorized party holding the key.
- **Hash**: only ever used to verify whether a backup artifact has been tampered with or corrupted (SHA-256). It is one-way — it **must never** be used to reconstruct plaintext, and it is not an encryption mechanism.

```
Original log ──┬──► SHA-256    ──► Integrity check
               └──► AES-256-GCM ──► Encrypted backup ──► Authorized restore ──► Plaintext log
```

## Features

- **Backup**: full backup / incremental backup (compares file size and modification time; only backs up what changed)
- **Safe file handling**: detects and skips locked/unreadable files, retries, and records any remaining failures to the audit log without aborting the whole run
- **Compress + encrypt + hash**: tar → gzip → AES-256-GCM encryption → SHA-256 hash
- **Atomic backup**: writes to `*.tmp` first, re-reads and re-verifies the hash, and only then renames it to the final artifact name; a backup that fails verification is never marked `VERIFIED`
- **Verify before restore**: restore always hash-verifies first; a mismatch blocks the restore by default (override with `--force`, which always produces a high-priority audit record)
- **Never overwrites production by default**: restore goes to a separate directory by default; overwriting an existing file requires an explicit `--overwrite`
- **Retention policy**: supports time-based (`keep_days`) and/or count-based (`keep_count`) cleanup of expired backups; locked backups are never deleted
- **Audit logging**: every key operation (start/complete/fail, hash verification, restore, delete…) is recorded
- **Crash recovery**: `repair` finds and cleans up incomplete backup artifacts (`*.tmp`) left behind by an interruption
- **Multi-language UI**: CLI help text and command output can switch between English and Traditional Chinese at runtime via `--lang`, an environment variable, or the config file (see "Switching the UI language" below)

## Requirements

- [.NET 8 SDK](https://dotnet.microsoft.com/) or later (developed with the .NET 10 SDK, which builds the net8.0 target without issue)

## Project layout

```
LogBackup/
├── src/
│   ├── LogBackup.Core/            # domain logic: backup, restore, retention, encryption, hashing
│   ├── LogBackup.Infrastructure/  # concrete adapters: local storage, key store, SQLite index, audit files, YAML config
│   └── LogBackup.CLI/             # command-line interface (logbackup)
├── tests/LogBackup.Tests/         # automated tests (xUnit)
├── config/                        # configuration file location
├── docs/
├── Skill.md                       # the full specification document
└── LogBackup.slnx                 # solution file
```

## Quick start

Run all commands below from the repository root (where `LogBackup.slnx` lives). During development, invoke the CLI via `dotnet run`:

```bash
dotnet run --project src/LogBackup.CLI -- <command> [options]
```

Once built as a release binary, run `logbackup <command> [options]` directly.

### Switching the UI language

Both the `--help` text and every command's output support switching between English (`en`, default) and Traditional Chinese (`zh-TW`) at runtime. Resolution order, highest priority first:

1. **`--lang` / `-l` option** (per-invocation override, highest priority)
2. **`LOGBACKUP_LANG` environment variable** (good for a personal/machine-wide default)
3. **`ui.language` in the config file** (good for a team/project-wide default)
4. Falls back to English if none of the above are set

```bash
# One-off override to Traditional Chinese
dotnet run --project src/LogBackup.CLI -- --lang zh-TW list
dotnet run --project src/LogBackup.CLI -- -l zh-TW backup

# Set a default for the current shell session via an environment variable
# PowerShell
$env:LOGBACKUP_LANG = "zh-TW"
# bash
export LOGBACKUP_LANG=zh-TW

# Or bake it into the config file so the whole project defaults to Chinese
```

```yaml
ui:
  language: zh-TW
```

`--lang` overrides both the environment variable and the config file. Even `--help` reflects the resolved language, including command descriptions and option help text.

### 1. Build and test

```bash
dotnet build LogBackup.slnx
dotnet test LogBackup.slnx
```

### 2. Initialize the config file

```bash
dotnet run --project src/LogBackup.CLI -- config init
```

Writes a default config to `./config/logbackup.yaml` (does nothing if one already exists):

```yaml
backup:
  sources:
    - path: "./logs"
      recursive: true
  destination: "./backup"
  compression:
    enabled: true
    algorithm: "gzip"
  encryption:
    enabled: true
    algorithm: "AES-256-GCM"
    key_id: "default"
  hash:
    algorithm: "SHA-256"
retention:
  keep_days: 30
restore:
  default_directory: "./restore"
audit:
  enabled: true
  directory: "./audit"
ui:
  language: "en"
```

Edit `backup.sources` (the log directories to back up), `backup.destination` (where backups are stored), `retention` (retention policy), etc. as needed. Use `--config <path>` to point at a different config file.

```bash
# Show the currently effective configuration
dotnet run --project src/LogBackup.CLI -- config show
```

### 3. Set up an encryption key (development)

Backups are encrypted with AES-256-GCM; the key is resolved by `key_id` (`backup.encryption.key_id` in the config, `default` unless changed):

- **Development**: set the environment variable `LOGBACKUP_KEY_<KEY_ID_UPPERCASE>` to a base64-encoded 256-bit (32-byte) key. If unset, the tool **automatically** generates and persists a key on first use (Windows: DPAPI-encrypted under `%LocalAppData%\LogBackup\keys`; Linux/macOS: an owner-only-readable key file) — no manual setup required to just start using it.
- The key itself is never written to source code, Git, or backup metadata (`metadata.json` only ever records `key_id`, never the key material).

PowerShell example (supplying your own key):

```powershell
$env:LOGBACKUP_KEY_DEFAULT = [Convert]::ToBase64String((1..32 | ForEach-Object { Get-Random -Max 256 }))
```

### 4. Run a backup

```bash
# Full backup (per backup.sources in the config)
dotnet run --project src/LogBackup.CLI -- backup

# Incremental backup: only files changed since the last verified backup
dotnet run --project src/LogBackup.CLI -- backup --incremental

# Override the source directory for this run
dotnet run --project src/LogBackup.CLI -- backup --source ./logs
```

Example output:

```
Backup ID:        20260903-153917-277
Source:            C:\App\logs
Mode:              full
Files backed up:   2
Original size:     73 bytes
Encrypted size:    315 bytes
Encryption:        AES-256-GCM
Hash (SHA-256): ae89cda3bbb3d3c6dadb2590e7909c88c73a9365b07e3b5872ac05cc7f97210f
Status:            Verified
```

### 5. List backups and view details

```bash
dotnet run --project src/LogBackup.CLI -- list
dotnet run --project src/LogBackup.CLI -- info --id <backup-id>
```

### 6. Verify backup integrity

Recomputes the hash of a stored backup artifact and compares it against the recorded value:

```bash
dotnet run --project src/LogBackup.CLI -- verify --id <backup-id>
```

### 7. Restore a backup

```bash
# Restore to the default directory restore/<backup-id>/
dotnet run --project src/LogBackup.CLI -- restore --id <backup-id>

# Restore to a custom destination
dotnet run --project src/LogBackup.CLI -- restore --id <backup-id> --output ./my-restore

# Allow overwriting existing files at the destination
dotnet run --project src/LogBackup.CLI -- restore --id <backup-id> --overwrite

# Emergency override when hash verification fails (audited at high priority — avoid unless necessary)
dotnet run --project src/LogBackup.CLI -- restore --id <backup-id> --force
```

Restore flow: hash is verified first → on mismatch, the restore is aborted by default with a non-zero exit code → only on success does the tool decrypt, decompress, and write out plaintext files.

### 8. Retention policy (clean up old backups)

```bash
# Apply the retention policy from the config
dotnet run --project src/LogBackup.CLI -- retention

# Preview what would be deleted without deleting anything
dotnet run --project src/LogBackup.CLI -- retention --dry-run
```

### 9. Delete a single backup

```bash
dotnet run --project src/LogBackup.CLI -- delete --id <backup-id>
```

Prompts for confirmation by default; `--yes` skips the prompt (for scripted/automated use), and `--force` deletes even a locked backup.

### 10. View audit records

```bash
dotnet run --project src/LogBackup.CLI -- audit
dotnet run --project src/LogBackup.CLI -- audit --id <backup-id>
```

### 11. Crash recovery

If a backup is interrupted (power loss, crash, etc.), it may leave behind an incomplete `*.tmp` artifact. These are **never** treated as valid backups. Find and clean them up with:

```bash
dotnet run --project src/LogBackup.CLI -- repair          # list only
dotnet run --project src/LogBackup.CLI -- repair --delete # list and delete
```

## Command reference

| Command | Description |
| --- | --- |
| `config init` / `config show` | Initialize / inspect the config file |
| `backup [--source] [--incremental]` | Run a backup |
| `list` | List all backups |
| `info --id <id>` | Show detailed metadata for one backup |
| `verify --id <id>` | Re-verify backup integrity |
| `restore --id <id> [--output] [--overwrite] [--force]` | Restore a backup |
| `delete --id <id> [--yes] [--force]` | Delete a backup |
| `retention [--dry-run]` | Apply the retention policy |
| `audit [--id <id>]` | View audit records |
| `repair [--delete]` | Detect and clean up incomplete backups |

Every command also accepts the global options `--config <path>` / `-c <path>` to select a config file, and `--lang <en|zh-TW>` / `-l <en|zh-TW>` to select the UI language (see "Switching the UI language" above).

## Exit codes

The CLI returns meaningful exit codes so it integrates cleanly with scheduled jobs (cron, Task Scheduler) or CI/CD:

| Code | Meaning |
| --- | --- |
| 0 | Success |
| 1 | General error |
| 2 | Invalid arguments |
| 3 | File not found |
| 4 | Permission denied |
| 5 | Encryption error |
| 6 | Decryption error |
| 7 | Integrity verification failed |
| 8 | Backup not found |
| 9 | Retention error |
| 10 | Authorization failed |

## Security notes

- Encryption keys are never written to source code, Git, or backup metadata.
- By default, the original log is preserved **without any sanitization**, since it may be raw evidence needed for later forensics; the `sanitization` section in the config is currently a reserved field only — the masking logic itself is not implemented yet.
- Restore defaults to a separate directory and never overwrites production logs.
- For the full list of security requirements and what's not implemented yet, see the "Known gaps" section in [`CLAUDE.md`](./CLAUDE.md).

## Full specification

This project was built from the complete specification in [`Skill.md`](./Skill.md), which covers system architecture, backup modes, key management, audit events, cross-platform requirements, testing requirements, and the Definition of Done. Read it before making architectural changes.
