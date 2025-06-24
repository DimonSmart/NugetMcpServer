namespace NuGetMcpServer.Common;

public static class PromptConstants
{
    public const string PackageSearchPrompt = @"Task:
Generate up to {0} (less is possible) User Request specific descriptive keywords.
Keywords should help with highly relevant to User Request NuGet package finding.
Use synonyms, variations of the original term.

Keyword Rules:

Keyword should be short, ideally one word
Two-word terms are allowed if they are common enough
Restrictions:

Exclude broad terms or general categories.
Do not mention NuGet.
Do not provide irrelevant keywords, If no more relevant - just return less results.
Output format:
Only keywords, one per line, no extra text, no numbering, no explanations.

User Request:
'{1}'";
}
