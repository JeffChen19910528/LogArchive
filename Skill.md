# Cross-Platform Log Backup & Integrity Management Skill

## 1. Skill Name

**Cross-Platform Log Backup & Integrity Management**

A cross-platform log backup, encryption, cryptographic hash verification, retention management, restoration, and audit tool.

The tool MUST support:

- Windows
- Linux
- macOS

The primary purpose is to securely archive application/system logs while preserving their integrity and allowing authorized personnel to restore logs into readable plaintext form when necessary.

---

# 2. Core Design Principle

The system MUST clearly distinguish between:

### 2.1 Encryption

Encryption is used to protect the confidentiality of backup logs.

```text
Plaintext Log
     ↓
Encryption
     ↓
Encrypted Backup
```

Encrypted backup files MUST be decryptable by authorized users/processes with the appropriate key.

---

### 2.2 Cryptographic Hash

A cryptographic hash is used to verify whether the backup file has been modified or corrupted.

Recommended algorithms:

- SHA-256 — default
- SHA-512 — optional
- SHA-3-256 — optional

Example:

```text
backup.log.enc
        ↓
     SHA-256
        ↓
a3f7...9c21
```

The hash MUST NOT be treated as an encryption mechanism.

Hash values are one-way and MUST NOT be used as the mechanism for restoring plaintext logs.

---

# 3. System Architecture

The system SHOULD use the following logical architecture:

```text
┌──────────────────────────────────────────────┐
│                Log Sources                   │
│                                              │
│ Application Logs / System Logs / Service     │
│ Logs / Error Logs / Audit Logs               │
└──────────────────────┬───────────────────────┘
                       │
                       ▼
┌──────────────────────────────────────────────┐
│              Backup Engine                   │
│                                              │
│ File Discovery                               │
│ File Lock Detection                          │
│ Incremental / Full Backup                    │
│ Compression                                  │
└──────────────────────┬───────────────────────┘
                       │
                       ▼
┌──────────────────────────────────────────────┐
│             Encryption Engine                │
│                                              │
│ AES-256-GCM / ChaCha20-Poly1305              │
│ Key Management                               │
└──────────────────────┬───────────────────────┘
                       │
                       ▼
┌──────────────────────────────────────────────┐
│            Integrity Engine                  │
│                                              │
│ SHA-256 / SHA-512 / SHA-3                    │
│ Hash Generation                              │
│ Hash Verification                            │
└──────────────────────┬───────────────────────┘
                       │
                       ▼
┌──────────────────────────────────────────────┐
│              Backup Storage                  │
│                                              │
│ Encrypted Log                                │
│ Hash Metadata                                │
│ Backup Metadata                              │
│ Audit Records                                │
└──────────────────────┬───────────────────────┘
                       │
             ┌─────────┴──────────┐
             │                    │
             ▼                    ▼
       Verification          Restoration
                              Engine
                                  │
                                  ▼
                         Plaintext Log
```

---

# 4. Functional Requirements

## 4.1 Backup

The tool MUST support backing up logs from configurable source directories.

Example:

```text
Windows:
C:\Application\Logs\

Linux:
/var/log/application/

macOS:
/var/log/application/
```

Configuration SHOULD support:

```yaml
backup:
  sources:
    - path: "./logs"
      recursive: true

  destination: "./backup"

  compression: true

  encryption:
    enabled: true
    algorithm: "AES-256-GCM"

  hash:
    algorithm: "SHA-256"
```

---

# 5. Backup Modes

The system SHOULD support at least:

## 5.1 Full Backup

Back up all eligible log files.

```text
logs/
 ├── app.log
 ├── error.log
 └── access.log
```

---

## 5.2 Incremental Backup

Only back up files that have changed since the previous backup.

The system SHOULD use:

- Modification timestamp
- File size
- Previous hash
- File identity where available

The hash MUST be used as the final integrity comparison mechanism.

---

## 5.3 Rotated Log Support

The system MUST support common rotated log formats.

Examples:

```text
app.log
app.log.1
app.log.2
app.log.3.gz
```

Windows examples:

```text
app-2026-09-03.log
app-2026-09-02.log
```

The system SHOULD detect rotated logs without treating every rotation as a new unrelated application.

---

# 6. File Handling

The backup engine MUST:

1. Detect whether the file exists.
2. Detect whether the file is currently being written.
3. Avoid corrupting actively written logs.
4. Handle files locked by another process where possible.
5. Retry failed reads.
6. Record failures in the audit log.
7. Never delete the original log as part of normal backup operation.

Example workflow:

```text
Detect Log
    ↓
Check File Status
    ↓
Read Safely
    ↓
Compress
    ↓
Encrypt
    ↓
Calculate Hash
    ↓
Store Backup
    ↓
Verify Backup
    ↓
Record Metadata
```

---

# 7. Encryption Requirements

The system SHOULD use authenticated encryption.

Preferred algorithms:

```text
AES-256-GCM
```

or

```text
ChaCha20-Poly1305
```

The implementation MUST NOT use:

- MD5
- SHA-1 as the primary integrity algorithm
- ECB mode
- Custom encryption algorithms
- Hard-coded encryption keys

Each backup SHOULD have a unique nonce/IV.

Example metadata:

```json
{
  "encryption": {
    "algorithm": "AES-256-GCM",
    "key_id": "key-2026-01",
    "nonce": "..."
  }
}
```

---

# 8. Key Management

Encryption keys MUST NOT be stored directly inside the application source code.

The system SHOULD support:

### Development

```text
Environment Variable
```

### Production

Prefer:

```text
Operating System Secret Store
```

or:

```text
Windows Credential Manager
Linux Secret Service / Secret Store
macOS Keychain
```

Enterprise deployments MAY additionally support:

- HashiCorp Vault
- AWS KMS
- Azure Key Vault
- Google Cloud KMS

The backup metadata SHOULD contain a `key_id`, not the actual encryption key.

---

# 9. Hash Generation

After encryption, the system MUST calculate the cryptographic hash of the final backup artifact.

Recommended workflow:

```text
Log
 ↓
Compression
 ↓
Encryption
 ↓
Encrypted Backup
 ↓
SHA-256
 ↓
Hash Metadata
```

Example:

```text
backup_20260903_230000.tar.gz.enc
```

Hash:

```text
SHA256:
9b7e6f...
```

The hash MUST be calculated over the exact stored backup file.

This ensures that the integrity check detects:

- File corruption
- Accidental modification
- Unauthorized modification
- Partial file replacement
- Storage corruption

---

# 10. Backup Metadata

Every backup MUST have metadata.

Example:

```json
{
  "backup_id": "20260903-230000-001",
  "source": "/var/log/myapp",
  "created_at": "2026-09-03T23:00:00+08:00",
  "platform": "linux",
  "hostname": "server01",
  "file_count": 12,
  "original_size": 18573422,
  "compressed_size": 4273912,
  "encrypted_size": 4274056,
  "encryption_algorithm": "AES-256-GCM",
  "hash_algorithm": "SHA-256",
  "hash": "9b7e6f...",
  "key_id": "key-2026-01",
  "status": "verified"
}
```

Metadata SHOULD be stored separately from the encrypted backup file.

---

# 11. Backup Directory Structure

The system SHOULD use a predictable structure.

Example:

```text
backup/
├── 2026/
│   └── 09/
│       └── 03/
│           ├── backup_20260903_230000.tar.gz.enc
│           ├── backup_20260903_230000.metadata.json
│           └── backup_20260903_230000.sha256
│
└── index/
    └── backup-index.json
```

For multiple applications:

```text
backup/
├── application-a/
├── application-b/
├── application-c/
└── system/
```

---

# 12. Backup Verification

Immediately after backup creation, the system SHOULD verify the backup.

Process:

```text
Create Backup
     ↓
Calculate SHA-256
     ↓
Write Metadata
     ↓
Read Backup Again
     ↓
Calculate SHA-256 Again
     ↓
Compare
     ↓
MATCH ───────► VERIFIED
     │
     └────────► MISMATCH / FAILED
```

If the calculated hash differs:

```text
status = "integrity_failed"
```

The system MUST NOT mark the backup as successful.

---

# 13. Restoration

The system MUST provide a restoration mechanism.

Important:

> Hash verification happens before restoration. The hash itself is never used to reconstruct the log.

Restoration workflow:

```text
Select Backup
      ↓
Read Metadata
      ↓
Verify SHA-256
      ↓
Hash Match?
   ┌──┴───┐
   │      │
  YES     NO
   │      │
   ▼      ▼
Decrypt   STOP
   │
   ▼
Decompress
   │
   ▼
Restore Plaintext
   │
   ▼
Verify Restored Files
   │
   ▼
Audit Result
```

---

# 14. Restore Output

The default restoration location MUST NOT overwrite the production log.

Example:

```text
restore/
└── 20260903-230000-001/
    ├── app.log
    ├── error.log
    └── access.log
```

The system MUST require an explicit `--overwrite` or equivalent option before overwriting existing files.

Default behavior:

```text
Restore → Separate Directory
```

NOT:

```text
Restore → Production Log Directory
```

---

# 15. Readable Log Access

The tool SHOULD provide a convenient way for authorized developers or support personnel to inspect restored logs.

Example:

```bash
logbackup restore --id 20260903-230000-001
```

Output:

```text
Restore completed.

Backup ID:
20260903-230000-001

Integrity:
PASS

Encryption:
AES-256-GCM

Files restored:
12

Restore directory:
./restore/20260903-230000-001/
```

Users can then inspect the plaintext logs.

---

# 16. Optional Secure Viewer

The system MAY provide:

```bash
logbackup view --id 20260903-230000-001
```

The viewer SHOULD:

- Verify integrity
- Decrypt in memory where practical
- Avoid creating unnecessary plaintext copies
- Support searching
- Support filtering by timestamp
- Support log level filtering
- Support keyword search

Example:

```bash
logbackup view \
  --id 20260903-230000-001 \
  --keyword "ERROR"
```

---

# 17. Access Control

Restoration MUST be treated as a privileged operation.

The system SHOULD support roles:

```text
Administrator
    │
    ├── Backup
    ├── Restore
    ├── Delete
    ├── Verify
    └── Configuration

Developer
    │
    ├── List Backups
    ├── Verify
    └── Restore

Auditor
    │
    ├── List Backups
    ├── Verify
    └── Read Audit Logs
```

The exact role model MAY be adapted to the deployment environment.

---

# 18. Audit Logging

Every important operation MUST generate an audit record.

Events SHOULD include:

```text
BACKUP_STARTED
BACKUP_COMPLETED
BACKUP_FAILED
HASH_GENERATED
HASH_VERIFICATION_SUCCESS
HASH_VERIFICATION_FAILED
RESTORE_STARTED
RESTORE_COMPLETED
RESTORE_FAILED
BACKUP_DELETED
KEY_ACCESS_FAILED
AUTHORIZATION_FAILED
CONFIGURATION_CHANGED
```

Example:

```json
{
  "timestamp": "2026-09-03T23:15:20+08:00",
  "event": "RESTORE_COMPLETED",
  "backup_id": "20260903-230000-001",
  "operator": "developer01",
  "source": "backup",
  "destination": "./restore/20260903-230000-001",
  "result": "success"
}
```

Audit logs SHOULD themselves be protected against unauthorized modification.

---

# 19. Retention Management

The system MUST support configurable retention.

Examples:

```yaml
retention:
  keep_days: 30
```

or:

```yaml
retention:
  keep_count: 50
```

or:

```yaml
retention:
  policy:
    daily: 7
    weekly: 4
    monthly: 12
```

The system SHOULD support both:

```text
Time-based retention
```

and

```text
Count-based retention
```

Before deleting a backup, the system MUST verify that:

1. The backup is not currently being restored.
2. The backup is not locked.
3. The backup is not under legal/audit hold if such a feature is configured.
4. The deletion operation is recorded in the audit log.

---

# 20. Backup Lifecycle

The complete lifecycle SHOULD be:

```text
ACTIVE LOG
    ↓
BACKUP
    ↓
COMPRESS
    ↓
ENCRYPT
    ↓
HASH
    ↓
VERIFY
    ↓
ARCHIVE
    ↓
RETENTION
    ↓
EXPIRE
    ↓
DELETE
```

---

# 21. CLI Design

The application SHOULD provide a cross-platform CLI.

Suggested command structure:

```bash
logbackup backup
logbackup list
logbackup info
logbackup verify
logbackup restore
logbackup delete
logbackup retention
logbackup config
logbackup audit
```

---

## 21.1 Backup

```bash
logbackup backup
```

Optional:

```bash
logbackup backup --source ./logs
```

---

## 21.2 List

```bash
logbackup list
```

Example:

```text
ID                         Date                  Size       Status
-----------------------------------------------------------------------
20260903-230000-001        2026-09-03 23:00      4.2 MB     VERIFIED
20260902-230000-001        2026-09-02 23:00      3.8 MB     VERIFIED
20260901-230000-001        2026-09-01 23:00      4.1 MB     VERIFIED
```

---

## 21.3 Verify

```bash
logbackup verify --id 20260903-230000-001
```

Expected:

```text
Backup:
20260903-230000-001

Hash Algorithm:
SHA-256

Expected:
9b7e6f...

Actual:
9b7e6f...

Integrity:
PASS
```

---

## 21.4 Restore

```bash
logbackup restore --id 20260903-230000-001
```

Optional destination:

```bash
logbackup restore \
  --id 20260903-230000-001 \
  --output ./restore/
```

---

## 21.5 Delete

```bash
logbackup delete --id 20260903-230000-001
```

The tool SHOULD require confirmation unless running in an explicitly configured automated mode.

Example:

```text
WARNING:
This operation permanently deletes backup:

20260903-230000-001

Continue? [y/N]
```

---

# 22. Configuration

The system SHOULD use a platform-independent configuration format.

Recommended:

```text
YAML
```

or:

```text
JSON
```

Example:

```yaml
application:
  name: "LogBackup"
  version: "1.0"

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
```

---

# 23. Cross-Platform Requirements

The implementation MUST NOT assume a specific operating system.

Avoid hard-coded:

```text
C:\
```

or:

```text
/
```

Use platform-independent path APIs.

Examples:

```text
.NET:
System.IO.Path

Python:
pathlib

Go:
filepath
```

The implementation SHOULD detect:

```text
Operating System
CPU Architecture
Path Separator
User
Hostname
Timezone
File Permissions
```

---

# 24. Platform-Specific Behavior

## Windows

Support:

- NTFS
- Windows file locks
- Windows services
- Windows Event Log integration where configured
- Windows Credential Manager
- Scheduled Task integration

Potential log sources:

```text
C:\ProgramData\Application\Logs
C:\Program Files\Application\Logs
```

---

## Linux

Support:

- ext4
- XFS
- systemd environments
- cron
- systemd timers
- `/var/log`
- POSIX permissions

Potential sources:

```text
/var/log
/opt/application/logs
```

---

## macOS

Support:

- APFS
- launchd
- macOS Keychain
- standard filesystem permissions

Potential sources:

```text
/var/log
/Library/Logs
```

---

# 25. Permissions

The tool MUST respect operating system permissions.

If the process cannot read a file:

```text
Do NOT bypass permissions.
```

Instead:

```text
Record permission error
Continue processing other files
Return meaningful failure information
```

Example:

```text
WARNING:
Unable to read:

/var/log/secure.log

Reason:
Permission denied
```

---

# 26. Handling Sensitive Information

Logs may contain:

- Passwords
- Tokens
- API keys
- Personal information
- Session identifiers
- Database connection strings
- Internal IP addresses

The system SHOULD provide optional sensitive-data masking.

Example:

```yaml
sanitization:
  enabled: true

  patterns:
    - "password"
    - "api_key"
    - "access_token"
    - "authorization"
```

However, the default backup behavior SHOULD preserve the original log unless the user explicitly enables sanitization.

This is important because sanitization changes the original evidence.

---

# 27. Security Requirements

The implementation MUST:

- Never hard-code encryption keys.
- Never log encryption keys.
- Never log plaintext secrets.
- Never expose decrypted logs unnecessarily.
- Never overwrite production logs by default.
- Verify backup integrity before restoration.
- Use authenticated encryption.
- Use secure random nonce/IV generation.
- Use secure file permissions.
- Record restore operations.
- Record failed authorization attempts.
- Avoid temporary plaintext files where practical.
- Securely remove temporary decrypted files when they are no longer required.

---

# 28. Atomic Backup

Backup creation SHOULD be atomic.

Do NOT directly write:

```text
backup_001.enc
```

Instead:

```text
backup_001.enc.tmp
```

Then:

```text
Create
 ↓
Encrypt
 ↓
Hash
 ↓
Verify
 ↓
Rename
```

Only after successful verification:

```text
backup_001.enc
```

This prevents incomplete backup files from being treated as valid backups.

---

# 29. Crash Recovery

The system MUST handle interruption.

For example:

```text
Machine Shutdown
Process Crash
Disk Full
Network Storage Failure
Permission Error
```

Temporary files SHOULD be identifiable:

```text
*.tmp
*.partial
```

On startup:

```bash
logbackup repair
```

or automatically detect incomplete backups.

The system MUST NOT automatically mark incomplete backups as valid.

---

# 30. Storage Support

The architecture SHOULD support multiple storage backends.

### Local

```text
Local Disk
```

### Network

```text
SMB
NFS
Network Share
```

### Object Storage

Optional support:

```text
S3
Azure Blob Storage
Google Cloud Storage
```

The storage layer SHOULD be abstracted:

```text
IStorageProvider
```

Example:

```text
LocalStorageProvider
SmbStorageProvider
S3StorageProvider
```

---

# 31. Database / Index

The tool MAY use a lightweight database to manage backup metadata.

Recommended:

```text
SQLite
```

Example:

```text
backup.db
```

Tables:

```text
backups
backup_files
hash_records
restore_records
audit_records
retention_records
```

SQLite is preferred for single-node deployments because it is cross-platform and does not require an external database server.

---

# 32. Recommended Project Architecture

A clean architecture SHOULD be used.

Example:

```text
LogBackup/
│
├── src/
│   ├── Core/
│   │   ├── Backup/
│   │   ├── Encryption/
│   │   ├── Hashing/
│   │   ├── Restoration/
│   │   ├── Retention/
│   │   ├── Audit/
│   │   └── Security/
│   │
│   ├── Infrastructure/
│   │   ├── FileSystem/
│   │   ├── Storage/
│   │   ├── KeyStore/
│   │   └── Database/
│   │
│   ├── CLI/
│   │
│   └── Configuration/
│
├── tests/
│   ├── Unit/
│   ├── Integration/
│   ├── Security/
│   └── CrossPlatform/
│
├── docs/
│
├── config/
│
└── README.md
```

---

# 33. Recommended Technology

The implementation SHOULD prioritize cross-platform support.

Possible implementation choices:

### Option A — .NET

```text
.NET 8+
C#
```

Advantages:

- Windows support
- Linux support
- macOS support
- Strong filesystem APIs
- Strong cryptography APIs
- Easy CLI development

Recommended for an enterprise environment heavily using Windows.

---

### Option B — Python

```text
Python 3.11+
```

Recommended libraries MAY include:

```text
cryptography
PyYAML
SQLite
pytest
```

Advantages:

- Rapid development
- Easy automation
- Excellent scripting capability

---

### Option C — Go

```text
Go 1.22+
```

Advantages:

- Excellent cross-platform support
- Easy standalone binary distribution
- Low runtime dependency
- Strong concurrency support
- Suitable for server-side deployment

For a standalone enterprise backup agent, Go is a particularly strong option.

---

# 34. Testing Requirements

The system MUST have automated tests.

Minimum coverage:

## Backup Tests

- Full backup
- Incremental backup
- Empty directory
- Large file
- Multiple files
- Unicode filenames
- Special characters
- Rotated logs

## Encryption Tests

- Encrypt/decrypt round trip
- Invalid key
- Invalid nonce
- Corrupted ciphertext
- Authentication failure

## Hash Tests

- Correct SHA-256
- Modified backup detection
- Corrupted backup detection
- Hash metadata mismatch

## Restore Tests

```text
Backup
 ↓
Encrypt
 ↓
Hash
 ↓
Restore
 ↓
Compare Original vs Restored
```

The result MUST be identical when sanitization is disabled.

## Retention Tests

- Expired backup deletion
- Non-expired backup preservation
- Count-based retention
- Locked backup protection

## Security Tests

- Unauthorized restore
- Unauthorized delete
- Key access failure
- Path traversal
- Malicious filenames
- Symlink attacks
- Temporary file exposure

---

# 35. Cross-Platform Test Matrix

CI MUST test at least:

```text
Windows
Linux
macOS
```

Example:

```text
Windows 11
Ubuntu LTS
macOS
```

The CI pipeline SHOULD verify:

```text
Build
Unit Tests
Integration Tests
Security Tests
CLI Tests
Backup/Restore Round Trip
```

---

# 36. Integrity Verification Rules

The system MUST treat the following states differently:

```text
VERIFIED
INTEGRITY_FAILED
DECRYPTION_FAILED
MISSING
CORRUPTED
INCOMPLETE
EXPIRED
DELETED
```

Example:

```text
VERIFIED
    ↓
Allowed to restore

INTEGRITY_FAILED
    ↓
Restore blocked by default
```

An administrator MAY override the restore block only through an explicit emergency option.

Example:

```bash
logbackup restore \
  --id 20260903-230000-001 \
  --force
```

The override MUST generate a high-priority audit record.

---

# 37. Disaster Recovery

The system SHOULD support exporting backup metadata.

Example:

```bash
logbackup export-metadata
```

This produces:

```text
backup-index.json
```

The metadata SHOULD allow administrators to identify:

```text
What was backed up
When it was backed up
Where it came from
Which encryption key was used
Which hash algorithm was used
What the hash was
Whether integrity verification succeeded
```

---

# 38. Backup Verification Schedule

In addition to verifying immediately after backup, the system SHOULD support periodic verification.

Example:

```text
Backup Created
      ↓
Immediate Verification
      ↓
Daily Verification
      ↓
Weekly Deep Verification
```

This helps detect silent storage corruption.

---

# 39. CLI Exit Codes

The CLI SHOULD provide meaningful exit codes.

Example:

```text
0 = Success
1 = General Error
2 = Invalid Arguments
3 = File Not Found
4 = Permission Denied
5 = Encryption Error
6 = Decryption Error
7 = Integrity Verification Failed
8 = Backup Not Found
9 = Retention Error
10 = Authorization Failed
```

This allows the tool to integrate with CI/CD, cron, Task Scheduler, and monitoring systems.

---

# 40. Observability

The application SHOULD provide structured application logs.

Example:

```json
{
  "timestamp": "2026-09-03T23:20:10+08:00",
  "level": "INFO",
  "component": "BackupEngine",
  "event": "BACKUP_COMPLETED",
  "backup_id": "20260903-230000-001",
  "duration_ms": 1823
}
```

Sensitive values MUST NOT appear in application logs.

---

# 41. Example End-to-End Scenario

Given:

```text
/opt/myapp/logs/
```

containing:

```text
app.log
error.log
access.log
```

Run:

```bash
logbackup backup
```

System performs:

```text
1. Discover logs
2. Check file state
3. Read logs
4. Compress logs
5. Encrypt using AES-256-GCM
6. Calculate SHA-256
7. Save encrypted backup
8. Save metadata
9. Recalculate SHA-256
10. Verify integrity
11. Mark backup VERIFIED
12. Write audit record
```

Result:

```text
backup/
└── 2026/09/03/
    ├── backup_20260903_230000.tar.gz.enc
    ├── backup_20260903_230000.metadata.json
    └── backup_20260903_230000.sha256
```

Later, a developer needs the log.

Run:

```bash
logbackup restore --id 20260903-230000-001
```

System performs:

```text
1. Locate backup
2. Read metadata
3. Calculate SHA-256
4. Compare expected hash
5. If mismatch → STOP
6. If match → decrypt
7. Decompress
8. Restore plaintext logs
9. Record restore operation
```

Output:

```text
restore/
└── 20260903-230000-001/
    ├── app.log
    ├── error.log
    └── access.log
```

---

# 42. Important Security Principle

The system MUST follow this model:

```text
                    Confidentiality
                         │
                         ▼
                    Encryption
                         │
                         │
                         ▼
Log ──────────────► Encrypted Backup
                         │
                         ▼
                       Hash
                         │
                         ▼
                    Integrity
```

Do NOT implement:

```text
Log
 ↓
Hash
 ↓
"Decrypt Hash"
 ↓
Original Log
```

This is mathematically impossible for a cryptographic hash.

The correct implementation is:

```text
                 ┌──────────────► SHA-256 ──► Integrity
                 │
Original Log ────┤
                 │
                 └──────────────► AES-256-GCM ──► Confidential Backup
                                           │
                                           ▼
                                      Authorized
                                        Restore
                                           │
                                           ▼
                                    Original Plaintext
```

---

# 43. Development Rules for Coding Agents

When implementing this Skill, the coding agent MUST:

1. Understand the existing repository before modifying code.
2. Do not delete existing functionality without explicit approval.
3. Do not introduce platform-specific assumptions into core logic.
4. Separate encryption, hashing, backup, restoration, storage, and audit responsibilities.
5. Never hard-code secrets.
6. Never store encryption keys in Git.
7. Never treat hashes as encryption.
8. Never overwrite production logs during restore by default.
9. Add automated tests for every new core feature.
10. Run the complete test suite after major changes.
11. Verify Windows/Linux/macOS compatibility.
12. Maintain backward compatibility for existing backup metadata where possible.
13. Document configuration changes.
14. Document security-sensitive changes.
15. Never mark a backup as VERIFIED unless integrity verification has actually succeeded.

---

# 44. Definition of Done

The implementation is considered complete only when all of the following are satisfied:

```text
[ ] Cross-platform build succeeds
[ ] Windows tested
[ ] Linux tested
[ ] macOS tested
[ ] Full backup works
[ ] Incremental backup works
[ ] Compression works
[ ] AES-256-GCM encryption works
[ ] SHA-256 integrity hash works
[ ] Post-backup verification works
[ ] Backup metadata works
[ ] Restore works
[ ] Restored log matches original
[ ] Integrity failure blocks restore
[ ] Retention policy works
[ ] Audit logging works
[ ] Permission errors handled correctly
[ ] Crash recovery implemented
[ ] Atomic backup implemented
[ ] Temporary files handled safely
[ ] Encryption keys are not hard-coded
[ ] Automated tests pass
[ ] Security tests pass
[ ] Documentation completed
```

Target final result:

```text
              ┌──────────────────────┐
              │      LOG SOURCE      │
              └──────────┬───────────┘
                         │
                         ▼
              ┌──────────────────────┐
              │   BACKUP / COMPRESS  │
              └──────────┬───────────┘
                         │
                         ▼
              ┌──────────────────────┐
              │   AES-256-GCM        │
              │      ENCRYPTION      │
              └──────────┬───────────┘
                         │
                         ▼
              ┌──────────────────────┐
              │      SHA-256         │
              │   INTEGRITY HASH     │
              └──────────┬───────────┘
                         │
                         ▼
              ┌──────────────────────┐
              │   VERIFIED BACKUP    │
              └──────────┬───────────┘
                         │
                ┌────────┴────────┐
                │                 │
                ▼                 ▼
          RETENTION           RESTORE
                                  │
                                  ▼
                         HASH VERIFICATION
                                  │
                                  ▼
                            DECRYPTION
                                  │
                                  ▼
                         PLAINTEXT LOG
                                  │
                                  ▼
                       DEVELOPER / AUDITOR
```

The implementation SHOULD prioritize security, integrity, recoverability, auditability, and cross-platform compatibility over implementation convenience.