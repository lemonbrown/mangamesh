using System.Text.Json.Serialization;

namespace MangaMesh.Shared.Models.MangaDex
{
    public class MangaDexResponse<T>
    {
        [JsonPropertyName("result")]
        public string Result { get; set; } = "";

        [JsonPropertyName("response")]
        public string Response { get; set; } = "";

        [JsonPropertyName("data")]
        public T Data { get; set; } = default!;

        [JsonPropertyName("limit")]
        public int Limit { get; set; }

        [JsonPropertyName("offset")]
        public int Offset { get; set; }

        [JsonPropertyName("total")]
        public int Total { get; set; }
    }

    public class MangaData
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = "";

        [JsonPropertyName("type")]
        public string Type { get; set; } = "";

        [JsonPropertyName("attributes")]
        public MangaAttributes Attributes { get; set; } = new();

        [JsonPropertyName("relationships")]
        public List<Relationship> Relationships { get; set; } = new();
    }

    public class MangaAttributes
    {
        [JsonPropertyName("title")]
        public Dictionary<string, string> Title { get; set; } = new();

        [JsonPropertyName("altTitles")]
        public List<Dictionary<string, string>> AltTitles { get; set; } = new();

        [JsonPropertyName("description")]
        public Dictionary<string, string> Description { get; set; } = new();

        [JsonPropertyName("status")]
        public string Status { get; set; } = "";

        [JsonPropertyName("year")]
        public int? Year { get; set; }

        [JsonPropertyName("contentRating")]
        public string ContentRating { get; set; } = "";
    }

    public class ChapterData
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = "";

        [JsonPropertyName("type")]
        public string Type { get; set; } = "";

        [JsonPropertyName("attributes")]
        public ChapterAttributes Attributes { get; set; } = new();

        [JsonPropertyName("relationships")]
        public List<Relationship> Relationships { get; set; } = new();
    }

    public class ChapterAttributes
    {
        [JsonPropertyName("volume")]
        public string? Volume { get; set; }

        [JsonPropertyName("chapter")]
        public string? Chapter { get; set; }

        [JsonPropertyName("title")]
        public string? Title { get; set; }

        [JsonPropertyName("translatedLanguage")]
        public string TranslatedLanguage { get; set; } = "";

        [JsonPropertyName("publishAt")]
        public DateTime? PublishAt { get; set; }

        [JsonPropertyName("pages")]
        public int Pages { get; set; }
    }

    public class Relationship
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = "";

        [JsonPropertyName("type")]
        public string Type { get; set; } = "";

        [JsonPropertyName("attributes")]
        public Dictionary<string, object>? Attributes { get; set; }
    }

    public class LoginRequest
    {
        [JsonPropertyName("username")]
        public string Username { get; set; } = "";

        [JsonPropertyName("password")]
        public string Password { get; set; } = "";
    }

    public class LoginResponse
    {
        [JsonPropertyName("token")]
        public TokenData Token { get; set; } = new();
    }

    public class TokenData
    {
        [JsonPropertyName("session")]
        public string Session { get; set; } = "";

        [JsonPropertyName("refresh")]
        public string Refresh { get; set; } = "";
    }
}
