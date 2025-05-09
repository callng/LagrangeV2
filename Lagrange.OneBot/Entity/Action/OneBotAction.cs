using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace Lagrange.OneBot.Entity.Action;

[Serializable]
public class OneBotAction
{
    [JsonPropertyName("action")] public string Action { get; set; } = "";
    
    [JsonPropertyName("params")] public JsonNode? Params { get; set; }

    [JsonPropertyName("echo")] public JsonNode? Echo { get; set; } 
}