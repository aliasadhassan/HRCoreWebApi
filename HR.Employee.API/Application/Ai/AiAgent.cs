namespace HR.Employee.API.Application.Ai;

using System.Text.Json;
using HR.Employee.API.Domain.Ai;
using Microsoft.Extensions.Options;

/// <summary>Is request ka token aur Employee API ka andar wala pata (HTTP context se — Infrastructure banata hai).</summary>
public interface IAiCallContextProvider
{
    AiCallContext Get();
}

/// <summary>Ek turn ka nateeja: model ki nayi guftagu (payload), aakhri jawab, kaun se tools chale, tokens.</summary>
public sealed record AgentResult(
    IReadOnlyList<LlmMessage> Turn, string Answer, IReadOnlyList<string> ToolsUsed, AiUsage Usage, string StopReason);

/// <summary>
/// Tool loop: model ko history + sawal → tool_use aaye to tools chalao (user ke token se) → results wapas → jab tak jawab na aaye.
/// MaxToolRounds ke baad model ko kaha jata hai ke jo mila us se jawab de.
/// </summary>
public sealed class AiAgent(ILlmClient llm, AiToolRunner runner, IOptions<AiOptions> options)
{
    public async Task<AgentResult> RunAsync(
        LlmRequest request, IReadOnlyList<LlmMessage> history, LlmMessage question,
        IReadOnlyList<AiToolDef> tools, AiCallContext ctx, AiAreas areas, CancellationToken ct)
    {
        var turn = new List<LlmMessage> { question };
        var used = new List<string>();
        int input = 0, output = 0, cache = 0;
        var defs = tools.Select(t => new LlmTool(t.Name, t.Description, LlmJson.Schema(t.Schema))).ToList();
        var maxRounds = Math.Max(1, options.Value.MaxToolRounds);

        var round = 0;
        while (true)
        {
            round++;
            var res = await llm.CompleteAsync(request with { Messages = [.. history, .. turn], Tools = defs }, ct);
            input += res.InputTokens;
            output += res.OutputTokens;
            cache += res.CacheReadTokens;
            turn.Add(new LlmMessage("assistant", res.Content));

            var calls = res.Content.Where(b => b.IsToolUse).ToList();
            if (res.StopReason != "tool_use" || calls.Count == 0)
            {
                var answer = res.StopReason switch
                {
                    "refusal" => "I can't help with that request.",
                    "max_tokens" when !string.IsNullOrWhiteSpace(res.Text) => res.Text + "\n\n_(The answer was cut short. Ask for a smaller part.)_",
                    _ => res.Text,
                };
                if (string.IsNullOrWhiteSpace(answer))
                    answer = "I could not find an answer to that. Try asking it another way.";
                return new AgentResult(turn, answer, used.Distinct().ToList(), new AiUsage(input, output, cache), res.StopReason);
            }

            // Saare tool calls ek saath, saare results ek hi user message mein (model parallel calls seekhta rahe)
            var results = await Task.WhenAll(calls.Select(async call =>
            {
                var def = tools.FirstOrDefault(t => t.Name == call.Name);
                if (def is null)
                    return LlmBlock.ToolResult(call.Id!, "This tool is not available.", true);
                var (content, isError) = await runner.RunAsync(def, call.Input ?? default, ctx, areas, ct);
                lock (used)
                    used.Add(Label(def, call.Input));
                return LlmBlock.ToolResult(call.Id!, content, isError);
            }));

            var blocks = new List<LlmBlock>(results);
            if (round >= maxRounds)
                blocks.Add(LlmBlock.FromText("Tool limit reached. Answer now with what you found, and say what is missing."));
            turn.Add(new LlmMessage("user", blocks));
        }
    }

    /// <summary>UI chip: "Ran a report · employee:attendance".</summary>
    private static string Label(AiToolDef def, JsonElement? input)
    {
        if (def.Name == "run_report" && input is { ValueKind: JsonValueKind.Object } i
            && i.TryGetProperty("source", out var s) && s.ValueKind == JsonValueKind.String)
            return $"{def.Label} · {AiToolCatalog.Key(s.GetString()!)}";
        return def.Label;
    }

    public static string Serialize(IReadOnlyList<LlmMessage> turn) => JsonSerializer.Serialize(turn, LlmJson.Options);

    public static IReadOnlyList<LlmMessage> Deserialize(string? payload)
    {
        if (string.IsNullOrWhiteSpace(payload))
            return [];
        try
        {
            return JsonSerializer.Deserialize<List<LlmMessage>>(payload, LlmJson.Options) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }
}
