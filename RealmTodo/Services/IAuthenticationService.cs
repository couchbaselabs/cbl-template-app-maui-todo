using RealmTodo.Models;

namespace RealmTodo.Services;

public interface IAuthenticationService
{
    Task<User?> Login(
        string username, 
        string password, 
        CouchbaseAppConfig config);
}