using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.AI;
using Qdrant.Client;
using Qdrant.Client.Grpc;
using Scalar.AspNetCore;
using TheArchitect.ApiService;

var builder = WebApplication.CreateBuilder(args);

// Add service defaults & Aspire client integrations.
builder.AddServiceDefaults();

// Add services to the container.
builder.Services.AddProblemDetails();

// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

builder.AddQdrantClient(connectionName: "qdrant");


builder.AddOllamaApiClient("ollama-embedding").AddEmbeddingGenerator();
builder.AddOllamaApiClient("ollama-chat").AddChatClient();


var app = builder.Build();



// Configure the HTTP request pipeline.
app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapScalarApiReference();

Dictionary<Guid,List<ChatMessage>> chatHistories = new();

app.MapPost("chat", async (IChatClient chatClient,IEmbeddingGenerator<string,Embedding<float>> embeddingGenerator,QdrantClient qdrantClient, string question) =>
{
    var thread = Guid.CreateVersion7();
    ChatMessage systemMessage = new ChatMessage
    {
        Role = ChatRole.System,
        Contents
         =  [ new TextContent( "You are a helpful assistant for software architects. You will be provided with a question about azure well architected framework and should rephrase the question to be a search query that can be used to find relevant information in a vector database. You should only rephrase the question and not provide an answer.")]
    };
    ChatMessage userMessage = new ChatMessage
    {
        Role = ChatRole.User,
        Contents
         =  [ new TextContent( question)]
    };
    
    chatHistories[thread] = [systemMessage,userMessage];
    var response = await chatClient.GetResponseAsync( chatHistories[thread]);
    
    const string collection = "architect";
    var embedding = await embeddingGenerator.GenerateAsync(response.Text);
    var vector = embedding.Vector.ToArray();
    var searchResult = await qdrantClient.SearchAsync(collection, vector, limit: 3);

    var dataMessages = searchResult.Select(point=>
         new ChatMessage
        {
            Role = ChatRole.Tool,
            Contents
             =  [ new TextContent( $"File: {point.Payload["file"]} Score: {point.Score}. Content: {File.ReadAllText(point.Payload["file"].StringValue)}") ]
        });

    var summaries = dataMessages.Select(dm =>
    {
        var systemMsg = new ChatMessage
        {
            Role = ChatRole.System,
            Contents =
            [
                new TextContent(
                    "Summarize the following content to be concise and relevant to the question asked by the user. If the content is not relevant, say that it is not relevant.")
            ]
        };
        return chatClient.GetResponseAsync([systemMsg, userMessage, dm]);
    });
    var summaryMessages = await Task.WhenAll(summaries);
    
    
        chatHistories[thread].AddRange(summaryMessages.Select(sm=> sm.Messages.First()));
        
        var finalSystemMessage = new ChatMessage
        {
            Role = ChatRole.System,
            Contents = [ new TextContent("Based on the retrieved information based on RAG vector searches, Summarize the content and provide guidance to the user. Focus on the documents that are most relevant to the query ") ]
                };
        chatHistories[thread].Add(finalSystemMessage);
        
            var finalResponse = await chatClient.GetResponseAsync( [..summaryMessages.Select(sm=> sm.Messages.First()), finalSystemMessage]);
            chatHistories[thread].Add(finalResponse.Messages.First());
    
    return Results.Ok(new ChatReply(thread,finalResponse.Text, searchResult.Select(r=> r.Payload["file"].StringValue).ToArray()));
});


app.MapPost("ingest", async (QdrantClient qdrantClient,IEmbeddingGenerator<string,Embedding<float>> embeddingGenerator) =>
{
    const string collection = "architect";
    const int vectorSize = 768;
    if (!await qdrantClient.CollectionExistsAsync(collection))
    {
        await qdrantClient.CreateCollectionAsync(collection, new VectorParams
        {
            Size = (uint)vectorSize,
            Distance = Distance.Cosine
        });
    }

    var files = Directory.EnumerateFiles("well-architected/well-architected", "*.md", SearchOption.AllDirectories);
    var output = new StringBuilder();
    foreach (var file in files)
    {
        var length = new FileInfo(file).Length;
        output.Append("File: " + file + " Size: " + length);
        output.AppendLine();
        var chunks = Chunker.Chunk(File.ReadAllText(file));
        foreach (var chunk in chunks)
        {
            var embedding = await embeddingGenerator.GenerateAsync(chunk);
            Vectors vector = embedding.Vector.ToArray();
            await qdrantClient.UpsertAsync(collection, [new PointStruct
            {
                Id = Guid.NewGuid(),
                Vectors = vector,
                Payload =
                {
                    ["file"] = file,
                }
            }]);
        }
    }
    return Results.Ok(output.ToString());
});

app.MapGet("search", async (QdrantClient qdrantClient,IEmbeddingGenerator<string,Embedding<float>> embeddingGenerator,[FromQuery]string query ) =>
{
    const string collection = "architect";
    var embedding = await embeddingGenerator.GenerateAsync(query);
    var vector = embedding.Vector.ToArray();
    var searchResult = await qdrantClient.SearchAsync(collection, vector, limit: 5);
    return Results.Ok(searchResult);
});

app.MapGet("embed", async (IEmbeddingGenerator<string,Embedding<float>> embeddingGenerator,[FromQuery]string input ) =>
{
    var embedding = await embeddingGenerator.GenerateAsync(input);
    return Results.Ok(embedding);
});

app.MapDefaultEndpoints();

app.Run();


public record ChatReply(Guid Thread, string Text, string[] Sources);