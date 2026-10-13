namespace HR.Employee.API.Application.Ai;

using System.Text.Json;
using System.Text.Json.Serialization;

/// <summary>
/// Model se baat ki apni chhoti shakal — baqi code SDK ke types nahi jaanta (test mein FakeLlm, asal mein AnthropicLlm).
/// Block types: text, thinking (signature ke saath, bilkul waisa hi wapas jata hai), redacted_thinking, tool_use, tool_result.
/// </summary>
public sealed record LlmBlock
{
    public required string Type { get; init; }
    public string? Text { get; init; }
    public string? Signature { get; init; }
    public string? Data { get; init; }
    public string? Id { get; init; }
    public string? Name { get; init; }
    public JsonElement? Input { get; init; }
    public string? ToolUseId { get; init; }
    public bool IsError { get; init; }

    [JsonIgnore] public bool IsToolUse => Type == "tool_use";

    public static LlmBlock FromText(string text) => new() { Type = "text", Text = text };

    public static LlmBlock ToolResult(string toolUseId, string content, bool isError)
        => new() { Type = "tool_result", ToolUseId = toolUseId, Text = content, IsError = isError };
}

public sealed record LlmMessage(string Role, IReadOnlyList<LlmBlock> Content)
{
    public static LlmMessage User(string text) => new("user", [LlmBlock.FromText(text)]);
}

/// <summary>Tool ki definition (JSON schema). Strict = model ke arguments schema se bilkul milte hain.</summary>
public sealed record LlmTool(string Name, string Description, JsonElement Schema);

/// <summary>
/// SystemStatic cache hota hai (tools ke saath); SystemDynamic (aaj ki tareekh, user ka naam) cache ke baad.
/// Document = PDF (base64) jo pehle user message ke saath jata hai (CV screening).
/// </summary>
public sealed record LlmRequest(
    string SystemStatic,
    string? SystemDynamic,
    IReadOnlyList<LlmMessage> Messages,
    IReadOnlyList<LlmTool> Tools,
    int MaxTokens = 16000,
    string Effort = "low",
    JsonElement? OutputSchema = null,
    string? PdfBase64 = null);

public sealed record LlmResponse(IReadOnlyList<LlmBlock> Content, string StopReason, int InputTokens, int OutputTokens, int CacheReadTokens)
{
    public string Text => string.Join("\n\n", Content.Where(b => b.Type == "text" && !string.IsNullOrWhiteSpace(b.Text)).Select(b => b.Text!.Trim()));
}

public interface ILlmClient
{
    /// <summary>API key mili hui hai? Nahi to koi call nahi hoti — page "AI is not set up" dikhata hai.</summary>
    bool IsConfigured { get; }

    string Model { get; }

    Task<LlmResponse> CompleteAsync(LlmRequest request, CancellationToken ct);
}

/// <summary>Model / provider ka masla (key ghalat, had, server). User ko seedha, bina andar ki tafseel ke.</summary>
public sealed class LlmException(string message, Exception? inner = null) : Exception(message, inner);

public sealed class AiOptions
{
    public const string Section = "Ai";

    /// <summary>Key Vault "AnthropicApiKey" se Program.cs bharta hai. Khaali = AI band.</summary>
    public string? ApiKey { get; set; }
    public string Model { get; set; } = "claude-opus-5-5";
    /// <summary>Payroll API (service discovery naam). Tools isi par user ke token ke saath GET/POST karte hain.</summary>
    public string PayrollUrl { get; set; } = "https+http://hr-payroll";
    /// <summary>Ek sawal mein zyada se zyada tool rounds — is ke baad model ko jawab dena hi hai.</summary>
    public int MaxToolRounds { get; set; } = 8;
}

internal static class LlmJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public static JsonElement Schema(object shape) => JsonSerializer.SerializeToElement(shape, Options);
}
