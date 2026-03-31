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

        var results = await response.Content.ReadFromJsonAsync<SearchResultItem[]>(
            cancellationToken: cancellationToken);

        return results?.ToList() ?? [];
    }

    public async Task<string?> GetDocumentAsync(string path, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.GetAsync(
            $"/document?path={Uri.EscapeDataString(path)}", cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(cancellationToken);
    }

    public async Task<string?> IngestAsync(CancellationToken cancellationToken = default)
    {
        var response = await httpClient.PostAsync("/ingest", null, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(cancellationToken);
    }
}

public record ChatReply(Guid Thread, string Text, DocumentSource[] Sources);
public record DocumentSource(string File, string Title);
public record SearchResultItem(string File, string Title, float Score);
