using System.Text.Json.Serialization;

namespace ChatbotApp
{
    public class AnimeListResponse
    {
        [JsonPropertyName("data")]
        public AnimeData Data { get; set; }
    }

    public class AnimeData
    {
        [JsonPropertyName("Page")]
        public AnimePage Page { get; set; }
    }

    public class AnimePage
    {
        [JsonPropertyName("mediaList")]
        public List<AnimeMediaList> MediaList { get; set; }
    }

    public class AnimeMediaList
    {
        [JsonPropertyName("media")]
        public AnimeMedia Media { get; set; }
    }

    public class AnimeMedia
    {
        [JsonPropertyName("title")]
        public AnimeTitle Title { get; set; }
    }

    public class AnimeTitle
    {
        [JsonPropertyName("userPreferred")]
        public string UserPreferred { get; set; }
    }
}