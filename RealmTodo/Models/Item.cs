using System.Text.Json;
using System.Text.Json.Serialization; 
using RealmTodo.Services;

namespace RealmTodo.Models
{
    public record Item
    {
        public string Id { get; init; } = Guid.NewGuid().ToString();
        public string OwnerId { get; init; } = string.Empty;
        public string Summary { get; init; } = string.Empty;
        public bool IsComplete { get; init; }

        [JsonIgnore] 
        public bool IsMine { get; set; } = false; 

        public string ToJson()
        {
            return JsonSerializer.Serialize(this);     
        }
    }
}

