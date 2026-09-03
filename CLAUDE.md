# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this is

`logbackup` — a cross-platform (.NET 8) CLI that backs up application/system logs by compressing, encrypting (AES-256-GCM), hashing (SHA-256), and archiving them, with integrity verification and restoration. Implemented from the spec in `Skill.md` — read that file in full before making architectural changes; it is the source of truth for requirements this codebase must keep satisfying (security rules, exit codes, directory layout, CLI surface, Definition of Done in §44).

## Build, test, run

```bash
dotnet build LogBackup.slnx        # note: .slnx (XML solution), not .sln — this SDK (10.0.400) scaffolds that format
dotnet test LogBackup.slnx
dotnet test tests/LogBackup.Tests --filter FullyQualifiedName~BackupRestoreTests   # single test class
dotnet run --project src/LogBackup.CLI -- <command> [options]   # e.g. `-- config init`, `-- backup`, `-- list`
```

The published CLI binary is named `logbackup` (see `AssemblyName` in `LogBackup.CLI.csproj`), but during development invoke it via `dotnet run --project src/LogBackup.CLI --`.

## Project layout

```
src/LogBackup.Core/            # pure domain logic, no file-system/DB-specific dependencies beyond System.IO
  Models/                      # BackupMetadata, LogBackupConfig, AuditRecord, BackupStatus
  Abstractions/                # IStorageProvider, IEncryptionEngine, IHashEngine, IKeyProvider, IMetadataStore, IAuditLogger
  Encryption/AesGcmEncryptionEngine.cs
  Hashing/HashEngine.cs        # SHA-256 (default) / SHA-512 / SHA3-256 only — never wire up MD5/SHA-1
  Backup/BackupEngine.cs       # discover -> tar -> gzip -> encrypt -> hash -> verify -> atomic rename -> metadata
  Restoration/RestoreEngine.cs # hash-verify -> decrypt -> decompress -> extract (path-traversal-safe)
  Retention/RetentionEngine.cs # keep_days / keep_count, respects per-backup lock

src/LogBackup.Infrastructure/  # concrete adapters implementing Core's abstractions
  Storage/LocalStorageProvider.cs
  KeyStore/{EnvironmentKeyProvider,FileKeyProvider,CompositeKeyProvider}.cs
  Database/SqliteMetadataStore.cs   # backup.db — one JSON blob per row, indexed by backup_id
  Audit/FileAuditLogger.cs          # append-only JSONL, one file per UTC day under audit/
  Configuration/ConfigLoader.cs     # YAML (YamlDotNet, underscored naming convention)

src/LogBackup.CLI/             # System.CommandLine 2.0.0-beta4 front end
  AppServices.cs                # composition root: builds the full stack from a config path
  Commands/*.cs                 # one file per subcommand (backup, list, info, verify, restore, delete, retention, config, audit, repair)

tests/LogBackup.Tests/         # xUnit; TestHarness.cs wires a full stack into a fresh temp directory per test
```

## Key design points to preserve

- **Encryption and hashing are separate concerns** — the hash (`IHashEngine`) is only ever used to detect tampering/corruption, never to reconstruct plaintext. Don't blur this boundary (see `Skill.md` §2, §42).
- **Atomicity**: `BackupEngine` writes `*.tmp`, hashes it, re-reads and re-hashes it, and only renames to the final artifact name if both hashes match. A failed backup is left as `*.tmp` and is never indexed as a valid/verified backup. `logbackup repair` finds and cleans up leftover `*.tmp` files from a crash.
- **Restore never overwrites by default** — output goes to `restore.default_directory/<backup_id>/` (or `--output`), and `--overwrite` is required to replace existing files. Restore hash-verifies before decrypting; a mismatch blocks restore unless `--force` is passed, which is always audited (`RESTORE_FORCED_OVERRIDE`).
- **Path-traversal protection**: `RestoreEngine.ResolveSafeDestination` rejects any tar entry whose resolved path would land outside the restore root — see `RestoreSecurityTests` for the zip-slip-style regression test.
- **Key management**: `CompositeKeyProvider` tries `EnvironmentKeyProvider` first (`LOGBACKUP_KEY_<KEY_ID>`, base64 256-bit key — dev/CI override), then falls back to `FileKeyProvider` (key generated on first use, persisted under `%LocalAppData%/LogBackup/keys` on Windows, DPAPI-wrapped; owner-only file permissions on Linux/macOS). True OS Keychain/Secret Service integration is a known follow-up, not yet implemented — don't claim it's done.
- **Metadata never contains the encryption key** — only `key_id` and the per-backup `nonce`.
- **Exit codes are part of the contract** (`ExitCode.cs` mirrors `Skill.md` §39) — when adding a new failure path, map it to an existing code rather than inventing a new one.
- Note the `Environment.ExitCode` vs. `RootCommand.InvokeAsync` return-value split in `Program.cs` — command handlers set `Environment.ExitCode`, and `Main` reconciles it with the parser's own result. If you add a new command, follow the same `SetHandler(async (...) => Environment.ExitCode = await RunAsync(...))` pattern used by the existing commands, or exit codes silently collapse to 0.

## Known gaps vs. the full Skill.md spec (not yet implemented)

- No OS Keychain / Secret Service / HashiCorp Vault / cloud KMS key providers (only env var + DPAPI-or-file fallback).
- No network (SMB/NFS) or object-storage (S3/Azure/GCS) `IStorageProvider` implementations — only `LocalStorageProvider`.
- No sensitive-data sanitization/masking pass (`SanitizationConfig` exists in config but nothing consumes it yet).
- No periodic/scheduled verification, no `export-metadata` command, no RBAC/role enforcement.
- AES-GCM encryption/decryption buffers the full plaintext/ciphertext in memory (fine for typical log volumes; would need streaming AEAD chunking for very large backups).
