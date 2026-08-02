using System.Text.Json.Serialization;

namespace Localcel_WinUI3.Models
{
    public class AppConfig
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("port")]
        public int Port { get; set; }

        [JsonPropertyName("entry")]
        public string Entry { get; set; } = "server.js";

        [JsonPropertyName("domain")]
        public string? Domain { get; set; }

        [JsonPropertyName("app_type")]
        public string AppType { get; set; } = "node"; // "node", "static_cf", "static_gh"

        [JsonPropertyName("github_repo")]
        public string? GithubRepo { get; set; }

        [JsonPropertyName("gh_pages_deployed")]
        public bool GhPagesDeployed { get; set; }

        [JsonIgnore]
        public string DisplayTitle => AppType switch
        {
            "static_gh" => $"{Name} (GitHub Pages)",
            "static_cf" => $"{Name} (CF Tunnel)",
            _ => Name
        };
    }
}
