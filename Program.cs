using Azure.AI.OpenAI;
using static System.Environment;
using OpenAI.Chat;
using Azure;
using DotNetEnv;
using System.ClientModel;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text;
using System.Net.Http.Headers;
using OpenAI.Assistants;
using Microsoft.Identity.Client;

namespace ChatbotApp
{
    class Program
    {
        private static readonly HttpClient httpClient = new HttpClient();
        
        private static readonly int maxRetries = 5;
        private static readonly int baseDelayMilliseconds = 2000;

        static async Task Main(string[] args)
        {
            Env.Load();

            string endpoint = GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT");
            string key = GetEnvironmentVariable("AZURE_OPENAI_API_KEY");
            int userId = int.Parse(GetEnvironmentVariable("ANILIST_USER_ID"));
            
            AzureOpenAIClient azureClient = new(
                new Uri(endpoint), 
                new AzureKeyCredential(key));
            ChatClient chatClient = azureClient.GetChatClient("gpt-4o-mini");

            var messages = new List<ChatMessage>()
            {
                new SystemChatMessage("You are a wise old wizard."),
            };

            while (true)
            {
                Console.Write("You: ");
                var userMessage = Console.ReadLine();

                if (userMessage != null)
                {
                    if (userMessage.ToLower() == "exit")
                    {
                        break;
                    }

                    messages.Add(new UserChatMessage(userMessage));

                    if (userMessage.ToLower().Contains("anime list"))
                    {
                        var animeListResponse = await GetAnimeListAsync(userId);

                        messages.Add(new SystemChatMessage(animeListResponse));
                    }

                    await RetryPolicy(async () =>
                    {
                        Console.Write("Wizard: ");

                        var assistantResponse = new StringBuilder();

                        await foreach (var completionUpdate in chatClient.CompleteChatStreamingAsync(messages))
                        {
                            foreach (var contentPart in completionUpdate.ContentUpdate)
                            {
                                Console.Write(contentPart.Text);
                                assistantResponse.Append(contentPart.Text);
                            }
                        }
                        
                        Console.WriteLine();
                        messages.Add(new AssistantChatMessage(assistantResponse.ToString()));
                    });

                    Console.WriteLine();
                }
            }
        }

        private static async Task RetryPolicy(Func<Task> operation)
        {
            int retries = 0;
            while (true)
            {
                try
                {
                    await operation();
                    break;
                }
                catch (ClientResultException ex) when (ex.Status == 429)
                {
                    if (retries >= maxRetries)
                    {
                        Console.WriteLine("Max retries reached. Exiting...");
                        throw;
                    }

                    int delay = baseDelayMilliseconds * (int)Math.Pow(2, retries);
                    Console.WriteLine($"Rate limit hit. Retrying in {delay / 1000} seconds...");

                    await Task.Delay(delay);
                    retries++;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Unexpected error: {ex.Message}");
                    throw;
                }
            }
        }
        private static async Task<string> GetAnimeListAsync(int userId, int page = 1)
        {
            var query = @"
            query ($userId: Int, $page: Int) {
                Page(page: $page) {
                    pageInfo {
                        hasNextPage
                    }
                    mediaList(userId: $userId, status: CURRENT, type: ANIME) {
                        media {
                            title {
                                userPreferred
                            }
                        }
                    }
                }
            }";

            var variables = new
            {
                userId = userId,
                page = page
            };

            // Create the request payload
            var requestBody = new
            {
                query = query,
                variables = variables
            };

            var content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");

            httpClient.DefaultRequestHeaders.Accept.Clear();
            httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            HttpResponseMessage response = await httpClient.PostAsync("https://graphql.anilist.co", content);

            if (response.IsSuccessStatusCode)
            {
                var responseBody = await response.Content.ReadAsStringAsync();

                var animeResponse = JsonSerializer.Deserialize<AnimeListResponse>(responseBody);

                if (animeResponse != null)
                {
                    var animeTitles = animeResponse.Data.Page.MediaList
                        .Select(m => m.Media.Title.UserPreferred)
                        .ToList();

                    return $"Here is your current anime list:\n{string.Join("\n", animeTitles)}";
                }
            }

            return "I'm sorry, I couldn't retrieve your anime list at the moment.";
        }

        private static void printList(List<ChatMessage> chatMessages) 
        {
            foreach (var message in chatMessages)
            {
                Console.WriteLine(message);
            }
        }
    }
}