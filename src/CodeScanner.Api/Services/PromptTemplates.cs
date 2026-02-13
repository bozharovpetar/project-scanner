namespace CodeScanner.Api.Services;

public static class PromptTemplates
{
    public static string BuildAnalysisPrompt(string filePath, string language, string fileContent)
    {
        return $$"""
            You are a code analysis tool. Analyze the following {{language}} file for issues.

            File: {{filePath}}

            Analyze for ALL of the following categories:
            1. **SecurityVulnerability** - SQL injection, XSS, path traversal, hardcoded secrets, insecure crypto, etc.
            2. **DeadCode** - Unused variables, unreachable code, unused imports, unused functions/methods.
            3. **LintError** - Style violations, naming conventions, missing null checks, code smells.
            4. **Bug** - Logic errors, off-by-one errors, null reference risks, race conditions, resource leaks.

            Return a JSON object with this exact structure:
            {
              "findings": [
                {
                  "category": "SecurityVulnerability|DeadCode|LintError|Bug",
                  "severity": "Info|Low|Medium|High|Critical",
                  "title": "Short title",
                  "description": "Detailed explanation of the issue",
                  "lineStart": 10,
                  "lineEnd": 12,
                  "codeSnippet": "the problematic code",
                  "suggestion": "How to fix it"
                }
              ]
            }

            Rules:
            - If no issues found, return {"findings": []}
            - Be precise with line numbers
            - Only report real issues, not style preferences
            - Severity levels: Info (cosmetic), Low (minor), Medium (should fix), High (important), Critical (fix immediately)

            ```{{language}}
            {{fileContent}}
            ```
            """;
    }
}
