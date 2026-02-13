# Code Scanner API

A local API tool that scans project directories for security vulnerabilities, dead code, lint errors, and bugs using a locally-running LLM via [Ollama](https://ollama.com).

The scanner indexes all source files in a project, analyzes each one through the `qwen2.5-coder:7b` model, and produces structured reports with findings, severity levels, line numbers, and fix suggestions.

---

## Prerequisites

Before setting up Code Scanner, make sure the following are installed on your machine:

| Requirement | Minimum Version | Check command |
|---|---|---|
| [.NET SDK](https://dotnet.microsoft.com/download) | 9.0 | `dotnet --version` |
| [Ollama](https://ollama.com/download) | 0.1+ | `ollama --version` |
| [Git](https://git-scm.com/downloads) | Any | `git --version` |

---

## Step 1 — Install Ollama

Ollama runs LLMs locally on your machine. Download and install it for your OS:

### Windows

1. Go to https://ollama.com/download/windows
2. Download the installer and run it
3. Follow the installation wizard — defaults are fine
4. Ollama will start automatically as a background service

### macOS

```bash
# Using Homebrew
brew install ollama

# Start the Ollama service
ollama serve
```

### Linux

```bash
curl -fsSL https://ollama.com/install.sh | sh

# Start the Ollama service
ollama serve
```

### Verify Ollama is running

Open a terminal and run:

```bash
ollama --version
```

You should see a version number. Ollama's API runs on `http://localhost:11434` by default. Verify it's reachable:

```bash
curl http://localhost:11434
```

Expected output: `Ollama is running`

---

## Step 2 — Download the AI model

Code Scanner uses the `qwen2.5-coder:7b` model (7.6 billion parameters, ~4.7 GB download). Pull it:

```bash
ollama pull qwen2.5-coder:7b
```

This will download the model. It may take several minutes depending on your internet connection.

### Verify the model is available

```bash
ollama list
```

You should see `qwen2.5-coder:7b` in the list:

```
NAME                 ID            SIZE    MODIFIED
qwen2.5-coder:7b    dae161e27b0e  4.7 GB  ...
```

### Test the model works

Run a quick test to make sure the model responds:

```bash
ollama run qwen2.5-coder:7b "What is a null pointer exception?"
```

You should get an explanation back. Press `Ctrl+D` or type `/bye` to exit.

---

## Step 3 — Install the .NET 9 SDK

### Windows

```bash
# Using winget (Windows Package Manager)
winget install Microsoft.DotNet.SDK.9

# Or download manually from https://dotnet.microsoft.com/download/dotnet/9.0
```

After installation, **close and reopen your terminal** so the PATH is updated.

### macOS

```bash
# Using Homebrew
brew install dotnet-sdk

# Or download from https://dotnet.microsoft.com/download/dotnet/9.0
```

### Linux (Ubuntu/Debian)

```bash
# Add Microsoft package repository
sudo apt-get update
sudo apt-get install -y dotnet-sdk-9.0
```

### Verify .NET is installed

```bash
dotnet --version
```

Expected output: `9.0.x` (any 9.0 patch version)

---

## Step 4 — Clone and build the project

```bash
# Clone the repository
git clone <your-repo-url> code-scanner
cd code-scanner

# Restore dependencies and build
dotnet build
```

The build should complete with `0 Warning(s)` and `0 Error(s)`.

---

## Step 5 — Run the API

```bash
cd src/CodeScanner.Api
dotnet run --urls "http://localhost:5000"
```

You should see output like:

```
info: Microsoft.Hosting.Lifetime[14]
      Now listening on: http://localhost:5000
info: CodeScanner.Api.BackgroundServices.ScanProcessorService[0]
      Scan processor service started
```

### Open Swagger UI

Navigate to http://localhost:5000/swagger in your browser.

This gives you an interactive UI to explore and test all API endpoints.

---

## Step 6 — Run your first scan

### Create a scan

Send a POST request with the absolute path to the project you want to scan:

```bash
curl -X POST http://localhost:5000/api/scans \
  -H "Content-Type: application/json" \
  -d '{"projectPath": "/path/to/your/project"}'
```

On Windows, use forward slashes in the path:

```bash
curl -X POST http://localhost:5000/api/scans ^
  -H "Content-Type: application/json" ^
  -d "{\"projectPath\": \"C:/Users/you/projects/my-app\"}"
```

Response (HTTP 202 Accepted):

```json
{
  "id": 1,
  "projectPath": "C:/Users/you/projects/my-app",
  "status": "Queued",
  "totalFiles": 25,
  "processedFiles": 0
}
```

### Monitor progress

Check the scan status by polling:

```bash
curl http://localhost:5000/api/scans/1
```

Or stream real-time progress via Server-Sent Events:

```bash
curl -N http://localhost:5000/api/scans/1/progress
```

SSE events look like:

```
data: {"type":"file_completed","scanId":1,"filePath":"src/Program.cs","processedFiles":1,"totalFiles":25,"message":"Analyzed src/Program.cs — 3 findings"}

data: {"type":"file_completed","scanId":1,"filePath":"src/Service.cs","processedFiles":2,"totalFiles":25,"message":"Analyzed src/Service.cs — 0 findings"}

data: {"type":"scan_completed","scanId":1,"filePath":null,"processedFiles":25,"totalFiles":25,"message":"Scan completed"}
```

### View findings

Once the scan completes (or while it's running), query the findings:

```bash
# All findings
curl http://localhost:5000/api/scans/1/findings

# Filter by category
curl "http://localhost:5000/api/scans/1/findings?category=SecurityVulnerability"

# Filter by severity
curl "http://localhost:5000/api/scans/1/findings?severity=High"

# Combine filters with pagination
curl "http://localhost:5000/api/scans/1/findings?category=Bug&severity=Critical&page=1&pageSize=10"
```

Example finding:

```json
{
  "id": 1,
  "filePath": "src/AuthService.cs",
  "category": "SecurityVulnerability",
  "severity": "High",
  "title": "Hardcoded Secret",
  "description": "API key is hardcoded in source code, which could be exposed in version control.",
  "lineStart": 14,
  "lineEnd": 14,
  "codeSnippet": "private const string ApiKey = \"sk-abc123...\";",
  "suggestion": "Move secrets to environment variables or a secure vault like Azure Key Vault."
}
```

### View summary

Get aggregate counts by category and severity:

```bash
curl http://localhost:5000/api/scans/1/summary
```

```json
{
  "totalFindings": 12,
  "byCategory": {
    "SecurityVulnerability": 3,
    "DeadCode": 4,
    "LintError": 2,
    "Bug": 3
  },
  "bySeverity": {
    "Critical": 1,
    "High": 3,
    "Medium": 5,
    "Low": 2,
    "Info": 1
  }
}
```

---

## API Reference

### Scan Management

| Method | Endpoint | Description |
|---|---|---|
| `POST` | `/api/scans` | Create a new scan. Body: `{ "projectPath": "..." }` |
| `GET` | `/api/scans` | List all scans. Query: `?page=1&pageSize=20` |
| `GET` | `/api/scans/{id}` | Get scan details with summary |
| `DELETE` | `/api/scans/{id}` | Delete a scan and all its data |
| `GET` | `/api/scans/{id}/progress` | Stream progress via SSE |

### Reports

| Method | Endpoint | Description |
|---|---|---|
| `GET` | `/api/scans/{id}/findings` | List findings. Query: `?category=Bug&severity=High&page=1&pageSize=50` |
| `GET` | `/api/scans/{id}/files` | List scanned files. Query: `?hasFindings=true` |
| `GET` | `/api/scans/{id}/files/{fileId}` | Get a file with all its findings |
| `GET` | `/api/scans/{id}/summary` | Aggregate counts by category and severity |

### Finding Categories

| Category | What it detects |
|---|---|
| `SecurityVulnerability` | SQL injection, XSS, path traversal, hardcoded secrets, insecure crypto |
| `DeadCode` | Unused variables, unreachable code, unused imports, unused functions |
| `LintError` | Style violations, naming conventions, code smells |
| `Bug` | Logic errors, off-by-one, null reference risks, race conditions, resource leaks |

### Severity Levels

| Severity | Meaning |
|---|---|
| `Critical` | Fix immediately — active security risk or data loss |
| `High` | Important — significant bug or vulnerability |
| `Medium` | Should fix — real issue that affects code quality |
| `Low` | Minor — small improvement opportunity |
| `Info` | Cosmetic — informational observation |

---

## Configuration

All settings are in `src/CodeScanner.Api/appsettings.json`:

```json
{
  "ConnectionStrings": {
    "Default": "Data Source=codescanner.db"
  },
  "Ollama": {
    "BaseUrl": "http://localhost:11434",
    "Model": "qwen2.5-coder:7b"
  },
  "Scanner": {
    "MaxFileSizeBytes": 102400,
    "MaxContentChars": 80000,
    "SkipDirectories": [".git", "bin", "obj", "node_modules", "dist", "build", ".vs", ".idea"]
  }
}
```

| Setting | Default | Description |
|---|---|---|
| `Ollama:BaseUrl` | `http://localhost:11434` | Ollama API address |
| `Ollama:Model` | `qwen2.5-coder:7b` | LLM model to use for analysis |
| `Scanner:MaxFileSizeBytes` | `102400` (100 KB) | Skip files larger than this |
| `Scanner:MaxContentChars` | `80000` | Split files into chunks above this threshold |
| `Scanner:SkipDirectories` | See above | Directories to always skip during indexing |

### Using a different model

You can swap models by changing the `Ollama:Model` setting. Any Ollama-compatible model works:

```bash
# Pull a different model
ollama pull codellama:13b

# Update appsettings.json Ollama:Model to "codellama:13b"
```

Larger models produce better results but are slower. The `qwen2.5-coder:7b` model provides a good balance of quality and speed for local use.

---

## Project Structure

```
code-scanner/
├── CodeScanner.sln
├── src/CodeScanner.Api/
│   ├── Program.cs                          # App entry point, DI, middleware
│   ├── appsettings.json                    # Configuration
│   ├── Data/
│   │   └── AppDbContext.cs                 # EF Core context (SQLite)
│   ├── Models/
│   │   ├── Entities/                       # Scan, ScanFile, Finding
│   │   ├── Dtos/                           # Request/response records
│   │   └── Enums/                          # ScanStatus, FindingCategory, Severity
│   ├── Services/
│   │   ├── FileDiscoveryService.cs         # Walks directories, respects .gitignore
│   │   ├── OllamaClient.cs                # HTTP client for Ollama REST API
│   │   ├── FileAnalyzer.cs                 # Prompt building, response parsing
│   │   ├── ScanService.cs                  # Scan lifecycle management
│   │   ├── ScanProgressBroadcaster.cs      # In-memory SSE pub/sub
│   │   └── PromptTemplates.cs              # LLM prompt construction
│   ├── BackgroundServices/
│   │   └── ScanProcessorService.cs         # Async scan queue processor
│   └── Endpoints/
│       ├── ScanEndpoints.cs                # POST/GET/DELETE scans, SSE
│       └── ReportEndpoints.cs              # Findings, files, summary
```

---

## How It Works

1. **You POST a project path** to `/api/scans`
2. **File discovery** walks the directory tree, respects `.gitignore`, filters to known source file extensions, and skips binary/large files
3. **A scan record** is created in SQLite with all discovered files
4. **The scan is queued** via an in-memory channel to a background service
5. **For each file**, the background service:
   - Reads the file content
   - Builds a prompt asking the LLM to analyze for vulnerabilities, dead code, lint errors, and bugs
   - Sends the prompt to Ollama (with `format: "json"` for structured output)
   - Parses the JSON response into Finding entities
   - Saves to the database
   - Broadcasts an SSE event to any connected clients
6. **Once all files are processed**, the scan is marked as completed
7. **You query the results** via the reporting endpoints

---

## Troubleshooting

### "Connection refused" when starting a scan

Ollama is not running. Start it:

```bash
# Windows: Ollama runs as a service — check the system tray
# macOS / Linux:
ollama serve
```

### Scan is stuck at 0 processed files

The LLM is processing the first file. Depending on your hardware and the file size, a single file can take 30-120 seconds on the 7B model. Check the terminal running the API for log output showing analysis progress.

### "Directory does not exist" error

Make sure you use the full absolute path. On Windows with curl, use forward slashes:

```bash
# Correct
"C:/Users/you/projects/my-app"

# Incorrect (JSON escaping issues in curl)
"C:\Users\you\projects\my-app"
```

### Model not found

Pull the model first:

```bash
ollama pull qwen2.5-coder:7b
```

### Out of memory

The 7B model needs approximately 6-8 GB of RAM. If you're running low on memory, try a smaller model:

```bash
ollama pull qwen2.5-coder:1.5b
```

Then update `Ollama:Model` in `appsettings.json` to `qwen2.5-coder:1.5b`.

---

## Supported File Types

The scanner recognizes and analyzes files with these extensions:

| Language | Extensions |
|---|---|
| C# | `.cs` |
| JavaScript | `.js`, `.mjs` |
| TypeScript | `.ts`, `.tsx` |
| Python | `.py` |
| Java | `.java` |
| Go | `.go` |
| Rust | `.rs` |
| C/C++ | `.c`, `.h`, `.cpp`, `.hpp` |
| Ruby | `.rb` |
| PHP | `.php` |
| Swift | `.swift` |
| Kotlin | `.kt` |
| HTML | `.html`, `.htm` |
| CSS | `.css`, `.scss` |
| SQL | `.sql` |
| YAML | `.yml`, `.yaml` |
| JSON | `.json` |
| XML | `.xml`, `.csproj` |
| Markdown | `.md` |
| Shell | `.sh`, `.bash` |
| PowerShell | `.ps1` |
| Dockerfile | `Dockerfile` |

Files not matching any known extension are skipped.
