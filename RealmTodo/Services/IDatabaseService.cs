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
    Task LoginAsync(string email, string password);
    void Logout();
    void ToggleIsComplete(Item item);

}