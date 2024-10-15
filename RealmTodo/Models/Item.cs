using System.Text.Json;
using System.Text.Json.Serialization;
using CommunityToolkit.Mvvm.ComponentModel;
using RealmTodo.Services;

namespace RealmTodo.Models
{
    public partial class Item : ObservableObject
    {
        [JsonPropertyName("id")] public string Id { get; init; } = Guid.NewGuid().ToString();
        
        [JsonPropertyName("ownerId")] public string OwnerId { get; init; } = string.Empty;

        [JsonIgnore] private string _summary; 
        [JsonPropertyName("summary")] public string Summary
        {
            get => _summary;
            set
            {
                SetProperty(ref _summary, value);
                OnPropertyChanged("Summary");
            }
        }

        [JsonIgnore] private bool _isComplete;

        [JsonPropertyName("isComplete")]
        public bool IsComplete
        {
            get => _isComplete; 
            set 
            {
                SetProperty(ref _isComplete, value);
                OnPropertyChanged("IsComplete"); 
            }
        }
        
        [JsonIgnore] public bool IsMine { get; set; } = false;

        public string ToJson()
        {
            return JsonSerializer.Serialize(this);     
        }
    }
}

