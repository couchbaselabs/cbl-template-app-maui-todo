using RealmTodo.Data;
using RealmTodo.Models;

namespace RealmTodo.Services;

public interface IDatabaseService
{
    //current authenticated user
    User? CurrentUser { get; }

    //Couchbase Capella App Services Configuration
    CouchbaseAppConfig? AppConfig { get; }

    void AddTask(Item item);
    void DeleteTask(Item item);
    Task Init();
    void InitDatabase();
    bool IsMyTask(Item item);
    Task LoginAsync(string email, string password);
    void Logout();
    void PauseSync();
    void ResumeSync();
    void SetTaskLiveQuery(SubscriptionType subscriptionType, Action<IResultsChange<Item>> callback);
    void ToggleIsComplete(Item item);

}