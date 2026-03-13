using System.Net.Http.Json;
using System.Text.Json;

namespace TheArchitect.Web;

public class ArchitectApiClient(HttpClient httpClient)
{
    public async Task<ChatReply?> StartChatAsync(string question, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.PostAsync(
            $"/chat?question={Uri.EscapeDataString(question)}", null, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ChatReply>(cancellationToken: cancellationToken);
    }

    public async Task<ChatReply?> ContinueChatAsync(Guid thread, string question, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.PostAsync(
            $"/chat/{thread}?question={Uri.EscapeDataString(question)}", null, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ChatReply>(cancellationToken: cancellationToken);
    }

    public async Task<List<SearchResultItem>> SearchAsync(string query, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.GetAsync(
            $"/search?query={Uri.EscapeDataString(query)}", cancellationToken);
        response.EnsureSuccessStatusCode();

        var results = new List<SearchResultItem>();
        var json = await response.Content.ReadFromJsonAsync<JsonElement[]>(cancellationToken: cancellationToken);

        if (json is null) return results;

        foreach (var item in json)
        {
            var score = item.TryGetProperty("score", out var scoreEl) ? scoreEl.GetSingle() : 0f;
            var file = string.Empty;

            if (item.TryGetProperty("payload", out var payload) &&
                payload.TryGetProperty("file", out var fileEl) &&
                fileEl.TryGetProperty("stringValue", out var stringValueEl))
            {
                file = stringValueEl.GetString() ?? string.Empty;
            }

            results.Add(new SearchResultItem(file, score));
        }

        return results;
    }

    public async Task<string?> IngestAsync(CancellationToken cancellationToken = default)
    {
        var response = await httpClient.PostAsync("/ingest", null, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(cancellationToken);
    }
}

public record ChatReply(Guid Thread, string Text, string[] Sources);

public record SearchResultItem(string File, float Score);
