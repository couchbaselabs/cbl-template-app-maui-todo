using System.Text.Json;
using Couchbase.Lite;
using Couchbase.Lite.Logging;
using Couchbase.Lite.Query;
using Couchbase.Lite.Sync;
using RealmTodo.Models;

namespace RealmTodo.Services
{
    public class CouchbaseService(IAuthenticationService authenticationService)
        : IDatabaseService
    {
        //authentication service
        private readonly IAuthenticationService _authenticationService = authenticationService;

        //manage state of the service
        private bool _serviceInitialised;

        //scope and collection information
        private readonly string _scopeName = "data";
        private readonly string _collectionName = "tasks";
        private Collection? _taskCollection = null;

        //replicator management
        private Replicator? _replicator = null;
        private ListenerToken? _replicatorStatusToken = null;

        //database information
        private Database? _database = null;

        //query management
        private ListenerToken? _queryListenerToken = null;
        private IQuery? _queryMyTasks = null;
        private IQuery? _queryAllTasks = null;

        //current authenticated user
        public User? CurrentUser { get; private set; }

        //Couchbase Capella App Services Configuration
        public CouchbaseAppConfig? AppConfig { get; private set; }
        
        public void AddTask(Item item)
        {
            throw new NotImplementedException();
        }


        public void DeleteTask(Item item)
        {
            throw new NotImplementedException();
        }

        public async Task Init()
        {
            //turn on debugging - for production apps you should probably turn this off
            //https://docs.couchbase.com/couchbase-lite/current/csharp/troubleshooting-logs.html
            Database.Log.Console.Level = LogLevel.Debug;
            Database.Log.Console.Domains = LogDomain.All;

            //get the config from disk
            await using var fileStream = await FileSystem.Current.OpenAppPackageFileAsync("capellaConfig.json");
            using StreamReader reader = new(fileStream);
            var fileContent = await reader.ReadToEndAsync();
            AppConfig = JsonSerializer.Deserialize<CouchbaseAppConfig>(fileContent,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }

        public void InitDatabase()
        {
            if (_serviceInitialised)
            {
                return;
            }

            //
            //check state to validate we have a valid user and valid config for sync with Capella App Services
            //
            if (CurrentUser == null)
            {
                throw new InvalidOperationException("User must be authenticated before initializing the service");
            }

            if (AppConfig == null)
            {
                throw new InvalidOperationException(
                    "Can't read application configuration file, can't get Capella App Services URL without reading in config.");
            }

            //calculate the database name for the current logged-in user
            var username = CurrentUser?.Username.Replace("@", "-").Replace(".", "-");
            var databaseName = $"tasks-{username}";

            //set up the database
            var databaseConfig = new DatabaseConfiguration
            {
                Directory = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)
            };
            _database = new Database(databaseName, databaseConfig);
            _taskCollection = _database.CreateCollection(_collectionName, _scopeName);

            //create index
            var indexConfig = new ValueIndexConfiguration(["ownerId"]);
            _taskCollection.CreateIndex("idxTasksOwnerId", indexConfig);

            //create cache queries
            var query = "SELECT * FROM data.tasks  as item ";
            _queryAllTasks = _database.CreateQuery(query);
            var queryMyTasks = $"{query} WHERE item.ownerId = {CurrentUser?.Username}";
            _queryMyTasks = _database.CreateQuery(queryMyTasks);

            //create replicator config
            var targetEndpoint = new URLEndpoint(new Uri(AppConfig.EndpointUrl));
            var replicatorConfig = new ReplicatorConfiguration(targetEndpoint);
            replicatorConfig.Continuous = true;
            replicatorConfig.ReplicatorType = ReplicatorType.PushAndPull;

            //configure authentication
            replicatorConfig.Authenticator = new BasicAuthenticator(CurrentUser!.Username, CurrentUser!.Password);

            //configure the collection
            var collectionConfig = new CollectionConfiguration();
            replicatorConfig.AddCollection(_taskCollection, collectionConfig);

            //create the replicator
            _replicator = new Replicator(replicatorConfig);

            //write status to the console log right now for debugging 
            _replicatorStatusToken = _replicator.AddChangeListener((sender, change) =>
            {
                Console.WriteLine(change.Status.Error != null
                    ? $"Replicator error: {change.Status.Error}"
                    : $"Replicator status: {change.Status.Activity}");
            });

            _replicator.Start();

            _serviceInitialised = true;
        }

        public async Task LoginAsync(string email, string password)
        {
            if (AppConfig == null)
                throw new InvalidOperationException(
                    "Application configuration is missing. Can't get Capella App Services URL.");

            var user = await _authenticationService.Login(email, password, AppConfig);

            CurrentUser = user ?? throw new UnauthorizedAccessException("Login failed. Invalid email or password.");
        }

        public void Logout()
        {
            CurrentUser = null;
            _replicatorStatusToken?.Remove();
            _replicatorStatusToken = null;
            _queryListenerToken?.Remove();
            _queryListenerToken = null;

            //close the replicator
            _replicator?.Stop();
            _replicator?.Dispose();
            _replicator = null;

            //close the collection
            _taskCollection?.Dispose();
            _taskCollection = null;

            //close the database
            _database?.Close();
            _database?.Dispose();
            _database = null;
        }

        public void ToggleIsComplete(Item item)
        {
            throw new NotImplementedException();
        }

        public async Task SetSubscription(SubscriptionType subType)
        {
            realm.Subscriptions.Update(() =>
            {
                realm.Subscriptions.RemoveAll(true);

                var (query, queryName) = GetQueryForSubscriptionType(realm, subType);

                realm.Subscriptions.Add(query, new SubscriptionOptions { Name = queryName });
            });

            //There is no need to wait for synchronization if we are disconnected
            if (realm.SyncSession.ConnectionState != ConnectionState.Disconnected)
            {
                await realm.Subscriptions.WaitForSynchronizationAsync();
            }
        }

        public SubscriptionType GetCurrentSubscriptionType(Realm realm)
        {
            var activeSubscription = realm.Subscriptions.FirstOrDefault();

            return activeSubscription.Name switch
            {
                "all" => SubscriptionType.All,
                "mine" => SubscriptionType.Mine,
                _ => throw new InvalidOperationException("Unknown subscription type")
            };
        }

        private (IQueryable<Item> Query, string Name) GetQueryForSubscriptionType(Realm realm,
            SubscriptionType subType)
        {
            IQueryable<Item> query = null;
            string queryName = null;

            if (subType == SubscriptionType.Mine)
            {
                query = realm.All<Item>().Where(i => i.OwnerId == CurrentUser.Id);
                queryName = "mine";
            }
            else if (subType == SubscriptionType.All)
            {
                query = realm.All<Item>();
                queryName = "all";
            }
            else
            {
                throw new ArgumentException("Unknown subscription type");
            }

            return (query, queryName);
        }
    }

    public enum SubscriptionType
    {
        Mine,
        All,
    }

    public class CouchbaseAppConfig
    {
        public string EndpointUrl { get; init; } = "";
        public string CapellaUrl { get; init; } = "";
    }
}