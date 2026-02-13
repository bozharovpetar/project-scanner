namespace CodeScanner.Api.Services;

public class FileDiscoveryService : IFileDiscoveryService
{
    private readonly IConfiguration _config;
    private readonly ILogger<FileDiscoveryService> _logger;

    private static readonly HashSet<string> DefaultSkipDirs = new(StringComparer.OrdinalIgnoreCase)
    {
        ".git", "bin", "obj", "node_modules", "dist", "build", ".vs", ".idea",
        "__pycache__", ".venv", "venv", ".next", "coverage", "packages"
    };

    private static readonly Dictionary<string, string> ExtensionToLanguage = new(StringComparer.OrdinalIgnoreCase)
    {
        [".cs"] = "csharp", [".csx"] = "csharp",
        [".js"] = "javascript", [".jsx"] = "javascript",
        [".ts"] = "typescript", [".tsx"] = "typescript",
        [".py"] = "python", [".pyw"] = "python",
        [".java"] = "java",
        [".go"] = "go",
        [".rs"] = "rust",
        [".rb"] = "ruby",
        [".php"] = "php",
        [".c"] = "c", [".h"] = "c",
        [".cpp"] = "cpp", [".cc"] = "cpp", [".cxx"] = "cpp", [".hpp"] = "cpp",
        [".swift"] = "swift",
        [".kt"] = "kotlin", [".kts"] = "kotlin",
        [".scala"] = "scala",
        [".dart"] = "dart",
        [".lua"] = "lua",
        [".r"] = "r", [".R"] = "r",
        [".sql"] = "sql",
        [".html"] = "html", [".htm"] = "html",
        [".css"] = "css", [".scss"] = "scss", [".less"] = "less",
        [".xml"] = "xml", [".xaml"] = "xml",
        [".json"] = "json",
        [".yaml"] = "yaml", [".yml"] = "yaml",
        [".toml"] = "toml",
        [".md"] = "markdown",
        [".sh"] = "bash", [".bash"] = "bash",
        [".ps1"] = "powershell",
        [".dockerfile"] = "dockerfile",
        [".tf"] = "terraform",
        [".vue"] = "vue",
        [".svelte"] = "svelte",
    };

    public FileDiscoveryService(IConfiguration config, ILogger<FileDiscoveryService> logger)
    {
        _config = config;
        _logger = logger;
    }

    public Task<List<DiscoveredFile>> DiscoverFilesAsync(string projectPath, CancellationToken ct = default)
    {
        var maxFileSize = _config.GetValue("Scanner:MaxFileSizeBytes", 102400L);
        var extraSkipDirs = _config.GetSection("Scanner:SkipDirectories").Get<string[]>() ?? [];
        var skipSet = new HashSet<string>(DefaultSkipDirs, StringComparer.OrdinalIgnoreCase);
        foreach (var dir in extraSkipDirs)
            skipSet.Add(dir);

        var gitignorePatterns = LoadGitignorePatterns(projectPath);
        var files = new List<DiscoveredFile>();

        WalkDirectory(new DirectoryInfo(projectPath), projectPath, skipSet, gitignorePatterns, maxFileSize, files, ct);

        _logger.LogInformation("Discovered {Count} source files in {Path}", files.Count, projectPath);
        return Task.FromResult(files);
    }

    private void WalkDirectory(
        DirectoryInfo dir,
        string rootPath,
        HashSet<string> skipDirs,
        List<string> gitignorePatterns,
        long maxFileSize,
        List<DiscoveredFile> results,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        foreach (var subDir in dir.EnumerateDirectories())
        {
            if (skipDirs.Contains(subDir.Name))
                continue;

            var relativeDirPath = Path.GetRelativePath(rootPath, subDir.FullName).Replace('\\', '/');
            if (IsGitignored(relativeDirPath + "/", gitignorePatterns))
                continue;

            WalkDirectory(subDir, rootPath, skipDirs, gitignorePatterns, maxFileSize, results, ct);
        }

        foreach (var file in dir.EnumerateFiles())
        {
            var ext = file.Extension;
            if (!ExtensionToLanguage.TryGetValue(ext, out var language))
                continue;

            if (file.Length > maxFileSize)
                continue;

            var relativePath = Path.GetRelativePath(rootPath, file.FullName).Replace('\\', '/');
            if (IsGitignored(relativePath, gitignorePatterns))
                continue;

            results.Add(new DiscoveredFile(file.FullName, relativePath, language, file.Length));
        }
    }

    private static List<string> LoadGitignorePatterns(string projectPath)
    {
        var gitignorePath = Path.Combine(projectPath, ".gitignore");
        if (!File.Exists(gitignorePath))
            return [];

        return File.ReadAllLines(gitignorePath)
            .Where(line => !string.IsNullOrWhiteSpace(line) && !line.TrimStart().StartsWith('#'))
            .Select(line => line.Trim())
            .ToList();
    }

    private static bool IsGitignored(string relativePath, List<string> patterns)
    {
        foreach (var pattern in patterns)
        {
            if (MatchesGitignorePattern(relativePath, pattern))
                return true;
        }
        return false;
    }

    private static bool MatchesGitignorePattern(string path, string pattern)
    {
        var p = pattern;

        // Handle negation (we skip negated patterns for simplicity)
        if (p.StartsWith('!'))
            return false;

        // Remove leading slash (anchored to root)
        if (p.StartsWith('/'))
            p = p[1..];

        // Simple pattern matching: support *, **, and directory patterns
        // This is a simplified implementation covering common cases
        if (p.EndsWith('/'))
        {
            // Directory pattern - match if any path segment matches
            var dirName = p.TrimEnd('/');
            return path.Split('/').Any(segment =>
                SimpleWildcardMatch(segment, dirName));
        }

        if (p.Contains('/'))
        {
            // Path pattern - match from root
            return SimpleWildcardMatch(path, p);
        }

        // Filename pattern - match against filename or any path segment
        var fileName = path.Split('/').Last();
        return SimpleWildcardMatch(fileName, p);
    }

    private static bool SimpleWildcardMatch(string text, string pattern)
    {
        if (pattern == "*") return true;
        if (pattern == "**") return true;

        // Handle *.ext patterns
        if (pattern.StartsWith('*') && !pattern[1..].Contains('*'))
        {
            return text.EndsWith(pattern[1..], StringComparison.OrdinalIgnoreCase);
        }

        // Handle prefix* patterns
        if (pattern.EndsWith('*') && !pattern[..^1].Contains('*'))
        {
            return text.StartsWith(pattern[..^1], StringComparison.OrdinalIgnoreCase);
        }

        // Exact match
        return string.Equals(text, pattern, StringComparison.OrdinalIgnoreCase);
    }
}
