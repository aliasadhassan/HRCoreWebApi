namespace HR.Employee.API.Application.Ai;

using System.Text.Json;
using Anthropic;
using Anthropic.Exceptions;
using Anthropic.Models.Messages;
using Microsoft.Extensions.Options;

/// <summary>
/// Claude API (official Anthropic SDK). Key na ho to IsConfigured = false aur koi call nahi hoti (zero kharcha).
/// Prompt caching: tools + SystemStatic par cache; tareekh / naam SystemDynamic mein (cache ke baad).
/// </summary>
public sealed class AnthropicLlmClient : ILlmClient
{
    private readonly AnthropicClient? client;
    private readonly ILogger<AnthropicLlmClient> logger;

    public AnthropicLlmClient(IOptions<AiOptions> options, ILogger<AnthropicLlmClient> logger)
    {
        this.logger = logger;
        Model = options.Value.Model;
        if (!string.IsNullOrWhiteSpace(options.Value.ApiKey))
            client = new AnthropicClient { ApiKey = options.Value.ApiKey };
    }

    public bool IsConfigured => client is not null;

    public string Model { get; }

    public async Task<LlmResponse> CompleteAsync(LlmRequest request, CancellationToken ct)
    {
        if (client is null)
            throw new LlmException("The AI assistant is not set up yet.");

        var m = await SendAsync(BuildParams(request), ct);
        return new LlmResponse(
            ToBlocks(m.Content),
            m.StopReason?.Raw() ?? "end_turn",
            (int)m.Usage.InputTokens + (int)(m.Usage.CacheCreationInputTokens ?? 0),
            (int)m.Usage.OutputTokens,
            (int)(m.Usage.CacheReadInputTokens ?? 0));
    }

    private MessageCreateParams BuildParams(LlmRequest request)
    {
        var system = new List<TextBlockParam> { new() { Text = request.SystemStatic, CacheControl = new CacheControlEphemeral() } };
        if (!string.IsNullOrWhiteSpace(request.SystemDynamic))
            system.Add(new TextBlockParam { Text = request.SystemDynamic });

        var tools = new List<ToolUnion>();
        for (var i = 0; i < request.Tools.Count; i++)
        {
            var t = request.Tools[i];
            tools.Add(new Tool
            {
                Name = t.Name,
                Description = t.Description,
                InputSchema = Schema(t.Schema),
                // Aakhri tool par breakpoint: saare tools ek saath cache
                CacheControl = i == request.Tools.Count - 1 ? new CacheControlEphemeral() : null,
            });
        }

        var p = new MessageCreateParams
        {
            Model = Model,
            MaxTokens = request.MaxTokens,
            System = system,
            Messages = request.Messages.Select((m, i) => ToParam(m, i == 0 ? request.PdfBase64 : null)).ToList(),
            OutputConfig = new OutputConfig
            {
                Effort = request.Effort switch { "high" => Effort.High, "medium" => Effort.Medium, _ => Effort.Low },
                Format = request.OutputSchema is { } s ? new JsonOutputFormat { Schema = Dict(s) } : null,
            },
        };
        if (tools.Count > 0)
            p = p with { Tools = tools };
        return p;

    }

    private async Task<Message> SendAsync(MessageCreateParams p, CancellationToken ct)
    {
        try
        {
            return await client!.Messages.Create(p, ct);
        }
        catch (AnthropicRateLimitException ex)
        {
            throw new LlmException("The AI service is busy right now. Try again in a minute.", ex);
        }
        catch (AnthropicUnauthorizedException ex)
        {
            logger.LogError(ex, "Anthropic API key rejected");
            throw new LlmException("The AI service rejected the API key. Ask your admin to check it.", ex);
        }
        catch (AnthropicApiException ex)
        {
            logger.LogError(ex, "Anthropic API call failed");
            throw new LlmException("The AI service could not answer. Try again.", ex);
        }
        catch (AnthropicException ex)
        {
            logger.LogError(ex, "Anthropic call failed");
            throw new LlmException("The AI service could not be reached. Try again.", ex);
        }

    }

    private static List<LlmBlock> ToBlocks(IEnumerable<ContentBlock> content)
    {
        var blocks = new List<LlmBlock>();
        foreach (var b in content)
        {
            if (b.TryPickText(out TextBlock? text))
                blocks.Add(LlmBlock.FromText(text.Text));
            else if (b.TryPickThinking(out ThinkingBlock? th))
                blocks.Add(new LlmBlock { Type = "thinking", Text = th.Thinking, Signature = th.Signature });
            else if (b.TryPickRedactedThinking(out RedactedThinkingBlock? rt))
                blocks.Add(new LlmBlock { Type = "redacted_thinking", Data = rt.Data });
            else if (b.TryPickToolUse(out ToolUseBlock? tu))
                blocks.Add(new LlmBlock { Type = "tool_use", Id = tu.ID, Name = tu.Name, Input = JsonSerializer.SerializeToElement(tu.Input) });
        }

        return blocks;
    }

    private static MessageParam ToParam(LlmMessage m, string? pdf)
    {
        var content = new List<ContentBlockParam>();
        if (pdf is not null)
            content.Add(new DocumentBlockParam { Source = new Base64PdfSource { Data = pdf } });
        foreach (var b in m.Content)
        {
            switch (b.Type)
            {
                case "text":
                    content.Add(new TextBlockParam { Text = b.Text ?? "" });
                    break;
                case "thinking":
                    content.Add(new ThinkingBlockParam { Thinking = b.Text ?? "", Signature = b.Signature ?? "" });
                    break;
                case "redacted_thinking":
                    content.Add(new RedactedThinkingBlockParam { Data = b.Data ?? "" });
                    break;
                case "tool_use":
                    content.Add(new ToolUseBlockParam { ID = b.Id!, Name = b.Name!, Input = Dict(b.Input) });
                    break;
                case "tool_result":
                    content.Add(new ToolResultBlockParam { ToolUseID = b.ToolUseId!, Content = b.Text ?? "", IsError = b.IsError });
                    break;
            }
        }
        return new MessageParam { Role = m.Role == "assistant" ? Role.Assistant : Role.User, Content = content };
    }

    private static InputSchema Schema(JsonElement schema)
    {
        var props = schema.TryGetProperty("properties", out var p) ? Dict(p) : new Dictionary<string, JsonElement>();
        var required = schema.TryGetProperty("required", out var r)
            ? r.EnumerateArray().Select(x => x.GetString()!).ToList()
            : [];
        return new InputSchema { Properties = props, Required = required };
    }

    private static Dictionary<string, JsonElement> Dict(JsonElement? e)
        => e is { ValueKind: JsonValueKind.Object } o
            ? o.EnumerateObject().ToDictionary(x => x.Name, x => x.Value.Clone())
            : new Dictionary<string, JsonElement>();
}
