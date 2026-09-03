# LogBackup

[English](./README.md) | **繁體中文**

跨平台（Windows / Linux / macOS）的日誌備份、加密、完整性驗證與還原工具，以 .NET 8（C#）實作。

安全地封存應用程式／系統日誌，同時保留其完整性，並允許授權人員在需要時將日誌還原為可讀的明文。可以當作可寫腳本的 CLI 使用（`logbackup <指令> [選項]`），也可以直接雙擊執行檔，自動開啟方向鍵操作的互動選單，不需要記指令語法（見下方「互動選單」）。

## 核心設計原則

工具嚴格區分兩件事，兩者不可混淆：

- **加密（Encryption）**：保護備份檔的機密性，使用 AES-256-GCM（authenticated encryption），可被授權方解密還原。
- **雜湊（Hash）**：僅用來驗證備份檔是否被竄改或損毀（SHA-256），是單向的，**絕不能**拿來還原明文，也不是加密機制。

```
原始日誌 ──┬──► SHA-256   ──► 完整性驗證
          └──► AES-256-GCM ──► 加密備份 ──► 授權還原 ──► 明文日誌
```

## 功能特色

- **備份**：完整備份 / 增量備份（比對檔案大小與修改時間，只備份變更的檔案）
- **安全處理**：偵測並跳過鎖定中／無法讀取的檔案，重試後仍失敗會記錄到稽核日誌，不會中斷整個備份
- **壓縮＋加密＋雜湊**：tar 打包 → gzip 壓縮 → AES-256-GCM 加密 → SHA-256 雜湊
- **原子寫入（Atomic Backup）**：先寫入 `.tmp`，重新讀回驗證雜湊相符後才 rename 成正式檔案；驗證失敗的備份絕不會被標記為 `VERIFIED`
- **還原前必驗證**：還原前一定先比對雜湊，比對失敗預設會擋下還原（可用 `--force` 強制覆蓋，此操作會產生高優先權稽核紀錄）
- **還原不覆蓋正式環境**：預設還原到獨立目錄，不會動到正式的 log 目錄；要覆蓋既有檔案需明確加上 `--overwrite`
- **保留策略（Retention）**：支援依天數（`keep_days`）與/或數量（`keep_count`）自動清理過期備份，鎖定中的備份不會被刪除
- **稽核日誌**：所有關鍵操作（開始/完成/失敗、雜湊驗證、還原、刪除…）都會寫入稽核紀錄
- **崩潰復原**：`repair` 指令可找出並清理因中斷而遺留的未完成備份（`*.tmp`）
- **多語系介面**：CLI 的說明文字與輸出訊息可即時切換英文／繁體中文，支援 `--lang`、環境變數、設定檔三種切換方式（見下方「切換介面語言」）
- **互動選單**：雙擊執行檔（或在終端機不帶任何參數執行）就會進入方向鍵反白選擇的選單，不用記指令語法（見下方「互動選單」）
- **獨立執行檔**：可直接在 Windows 上跨編譯出 Windows／Linux 各自的單一自帶執行檔，不需要目標作業系統的建置環境或 Docker（見下方「建置獨立執行檔」）

## 系統需求

- [.NET 8 SDK](https://dotnet.microsoft.com/) 或以上（本機以 .NET 10 SDK 開發，可向下相容建置 net8.0 目標）

## 專案結構

```
LogBackup/
├── src/
│   ├── LogBackup.Core/            # 核心邏輯：備份、還原、保留、加密、雜湊、tar/gzip 打包
│   ├── LogBackup.Infrastructure/  # 落地實作：本機儲存、金鑰存放、SQLite 索引、稽核檔案、YAML 設定
│   └── LogBackup.CLI/             # 命令列介面（logbackup），含介面多語系
├── tests/LogBackup.Tests/         # 自動化測試（xUnit）
├── scripts/publish.ps1            # 建置獨立自帶執行檔（Windows + Linux）
├── config/                        # 設定檔存放處
├── docs/
└── LogBackup.slnx                 # 方案檔
```

## 快速開始

以下指令皆在專案根目錄（`LogBackup.slnx` 所在位置）執行。開發階段透過 `dotnet run` 呼叫 CLI：

```bash
dotnet run --project src/LogBackup.CLI -- <指令> [選項]
```

若已建置發行版執行檔，則直接執行 `logbackup <指令> [選項]`。

### 建置獨立執行檔（Windows 與 Linux）

可以直接在 Windows 機器上跨編譯出 Windows 與 Linux 各自的獨立單一執行檔 — .NET SDK 內建的跨平台發佈能力，不需要 Linux 機器、容器或 WSL：

```powershell
./scripts/publish.ps1
```

執行後會產生：

```
publish/
├── win-x64/logbackup.exe   # 直接在 Windows 上執行
└── linux-x64/logbackup     # 複製到 Linux 機器、賦予執行權限後即可執行
```

每個執行檔都已內含 .NET runtime，目標機器不需要另外安裝 .NET SDK 或 runtime。在 Linux 上複製過去後，第一次執行前記得加上執行權限：

```bash
chmod +x ./logbackup
./logbackup config init
```

若要建置其他執行環境（例如 `linux-arm64`、`osx-x64`）：

```powershell
./scripts/publish.ps1 -RuntimeIdentifiers linux-arm64,osx-x64
```

背後實際執行的是 `dotnet publish -r <rid> --self-contained true -p:PublishSingleFile=true`；完整發佈設定可參考 `LogBackup.CLI.csproj`。發佈出來的執行檔不會被提交進版本控制（`/publish/` 已在 `.gitignore` 中排除），需要時請由原始碼重新建置。

### 互動選單

在終端機不帶任何參數執行 `logbackup.exe`，或直接在檔案總管雙擊它，會開啟選單，而不是印出 `--help` 就結束：

```
LogBackup 互動選單
==================
你也可以改用命令列參數執行 logbackup.exe。
↑↓ 移動   Enter 選擇   Esc 離開

 備份已設定的日誌來源
 列出所有已知的備份
 查看單一備份的詳細資訊
 驗證備份的完整性
 還原備份
 刪除備份
 套用保留策略
 顯示目前設定
 初始化設定檔
 查看稽核紀錄
 修復（清理未完成的備份）
 語言：English
 離開
```

- **`↑` / `↓`** 移動反白列，**`Enter`** 確認選擇，**`Esc`** 離開。
- 選擇某個指令後，會依需要提示輸入相關參數（備份 ID、輸出目錄、是否覆蓋等），接著執行的是跟對應 CLI 指令完全相同的程式邏輯——選單只是包在外面的輸入層，不是另一套實作。
- **「語言」** 那一列可以立即在英文與繁體中文之間切換整個選單的顯示，不需要 `--lang` 或環境變數。這只會影響本次執行的畫面顯示；若要讓某個語言變成永久預設值，請見下方「切換介面語言」。
- 被重導向的呼叫方式（腳本、CI、`logbackup.exe --help > out.txt`）不受影響，一律直接走原本的 CLI 參數解析，不會進入選單。
- 在 Windows 上，若執行檔是被雙擊啟動（擁有自己全新開啟的 console，而不是你原本就開著的終端機），選單結束或單次指令執行完後，視窗也會停在「請按任意鍵關閉此視窗...」，輸出才不會一閃即逝。

### 切換介面語言

CLI 的說明文字（`--help`）與所有指令的輸出訊息都支援英文（`en`，預設）與繁體中文（`zh-TW`）即時切換，優先順序如下（由高到低）：

1. **`--lang` / `-l` 選項**（每次執行都可個別指定，優先權最高）
2. **`LOGBACKUP_LANG` 環境變數**（適合設定成個人／機器的預設語言）
3. **設定檔中的 `ui.language`**（適合整個團隊／專案共用同一種語言）
4. 以上皆未設定時，預設為英文

```bash
# 單次指定為繁體中文
dotnet run --project src/LogBackup.CLI -- --lang zh-TW list
dotnet run --project src/LogBackup.CLI -- -l zh-TW backup

# 透過環境變數設定預設語言（本次 shell session 皆為中文）
# PowerShell
$env:LOGBACKUP_LANG = "zh-TW"
# bash
export LOGBACKUP_LANG=zh-TW

# 或直接寫入設定檔，讓整個專案預設使用中文
```

```yaml
ui:
  language: zh-TW
```

`--lang` 選項會覆蓋環境變數與設定檔的設定；即使透過 `--help` 查看說明文字，也會依上述優先順序顯示對應語言的指令說明與選項描述。

### 1. 建置與測試

```bash
dotnet build LogBackup.slnx
dotnet test LogBackup.slnx
```

### 2. 初始化設定檔

```bash
dotnet run --project src/LogBackup.CLI -- config init
```

會在 `./config/logbackup.yaml` 產生預設設定檔（若已存在則不覆蓋）：

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

依需求編輯 `backup.sources`（要備份的日誌來源目錄）、`backup.destination`（備份存放位置）、`retention`（保留策略）等欄位。也可用 `--config <路徑>` 指定其他設定檔位置。

```bash
# 檢視目前生效的設定內容
dotnet run --project src/LogBackup.CLI -- config show
```

### 3. 設定加密金鑰（開發環境）

備份會使用 AES-256-GCM 加密，金鑰依 `key_id`（設定檔中的 `backup.encryption.key_id`，預設 `default`）解析：

- **開發環境**：可設定環境變數 `LOGBACKUP_KEY_<KEY_ID大寫>`（值為 base64 編碼的 256-bit／32-byte 金鑰）。若未設定，工具會**自動**在首次使用時於本機（Windows 用 DPAPI 加密存放於 `%LocalAppData%\LogBackup\keys`；Linux/macOS 存放為僅擁有者可讀寫的金鑰檔）產生並保存金鑰，不需手動設定即可直接使用。
- 金鑰本身絕不會寫入原始碼、Git 或備份中繼資料（`metadata.json` 只會記錄 `key_id`，不會記錄金鑰內容）。

PowerShell 範例（自行指定金鑰）：

```powershell
$env:LOGBACKUP_KEY_DEFAULT = [Convert]::ToBase64String((1..32 | ForEach-Object { Get-Random -Max 256 }))
```

### 4. 執行備份

```bash
# 完整備份（依設定檔的 backup.sources）
dotnet run --project src/LogBackup.CLI -- backup

# 增量備份：只備份自上次「已驗證」備份以來變更過的檔案
dotnet run --project src/LogBackup.CLI -- backup --incremental

# 臨時指定來源目錄（覆蓋設定檔）
dotnet run --project src/LogBackup.CLI -- backup --source ./logs
```

輸出範例：

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

### 5. 查看備份清單與詳細資訊

```bash
dotnet run --project src/LogBackup.CLI -- list
dotnet run --project src/LogBackup.CLI -- info --id <backup-id>
```

### 6. 驗證備份完整性

重新計算已存備份檔的雜湊值並與紀錄比對：

```bash
dotnet run --project src/LogBackup.CLI -- verify --id <backup-id>
```

### 7. 還原備份

```bash
# 還原到預設目錄 restore/<backup-id>/
dotnet run --project src/LogBackup.CLI -- restore --id <backup-id>

# 指定還原目的地
dotnet run --project src/LogBackup.CLI -- restore --id <backup-id> --output ./my-restore

# 目的地已有同名檔案時，需明確允許覆蓋
dotnet run --project src/LogBackup.CLI -- restore --id <backup-id> --overwrite

# 雜湊驗證失敗時，緊急強制還原（會產生高優先權稽核紀錄，非必要不要用）
dotnet run --project src/LogBackup.CLI -- restore --id <backup-id> --force
```

還原流程：先驗證雜湊 → 驗證失敗則預設中止並回傳非 0 的 exit code → 驗證成功才解密、解壓、寫出明文檔案。

### 8. 保留策略（清理舊備份）

```bash
# 依 config 中的 retention 設定執行清理
dotnet run --project src/LogBackup.CLI -- retention

# 僅預覽會刪除哪些備份，不實際刪除
dotnet run --project src/LogBackup.CLI -- retention --dry-run
```

### 9. 刪除單一備份

```bash
dotnet run --project src/LogBackup.CLI -- delete --id <backup-id>
```

預設會出現確認提示；`--yes` 可跳過確認（供自動化腳本使用），`--force` 可刪除鎖定中的備份。

### 10. 查看稽核紀錄

```bash
dotnet run --project src/LogBackup.CLI -- audit
dotnet run --project src/LogBackup.CLI -- audit --id <backup-id>
```

### 11. 崩潰復原

若備份過程中程式被中斷（斷電、當機等），可能會留下未完成的 `*.tmp` 檔案。這些檔案**不會**被視為有效備份，可用以下指令找出並清理：

```bash
dotnet run --project src/LogBackup.CLI -- repair          # 僅列出
dotnet run --project src/LogBackup.CLI -- repair --delete # 列出並刪除
```

## 指令總覽

| 指令 | 說明 |
| --- | --- |
| `config init` / `config show` | 初始化 / 檢視設定檔 |
| `backup [--source] [--incremental]` | 執行備份 |
| `list` | 列出所有備份 |
| `info --id <id>` | 顯示單一備份的詳細中繼資料 |
| `verify --id <id>` | 重新驗證備份完整性 |
| `restore --id <id> [--output] [--overwrite] [--force]` | 還原備份 |
| `delete --id <id> [--yes] [--force]` | 刪除備份 |
| `retention [--dry-run]` | 依保留策略清理過期備份 |
| `audit [--id <id>]` | 查看稽核紀錄 |
| `repair [--delete]` | 偵測並清理未完成的備份 |

所有指令皆可加上全域選項 `--config <路徑>` / `-c <路徑>` 指定要使用的設定檔，以及 `--lang <en|zh-TW>` / `-l <en|zh-TW>` 指定介面語言（見上方「切換介面語言」）。

## Exit Code

CLI 會回傳有意義的離開碼，方便整合到排程工作（cron、Task Scheduler）或 CI/CD：

| 代碼 | 意義 |
| --- | --- |
| 0 | 成功 |
| 1 | 一般錯誤 |
| 2 | 參數錯誤 |
| 3 | 找不到檔案 |
| 4 | 權限不足 |
| 5 | 加密錯誤 |
| 6 | 解密錯誤 |
| 7 | 完整性驗證失敗 |
| 8 | 找不到備份 |
| 9 | 保留策略錯誤 |
| 10 | 授權失敗 |

## 安全注意事項

- 加密金鑰絕不會寫入原始碼、Git 或備份中繼資料。
- 預設行為會**保留原始日誌不做任何脫敏處理**（因為那可能是事後鑑識用的原始證據）；設定檔中的 `sanitization` 目前僅為保留欄位，尚未實作遮蔽邏輯。
- 還原目的地預設為獨立目錄，不會覆蓋正式環境的 log。
- 詳細的安全需求與尚未實作的項目，請見 [`CLAUDE.md`](./CLAUDE.md) 的「Known gaps」段落。

