using System.Text.Json;

using Couchbase.Lite;
using Couchbase.Lite.Logging;
using Couchbase.Lite.Query;
using Couchbase.Lite.Sync;

using RealmTodo.Data;
using RealmTodo.Models;

namespace RealmTodo.Services
{
    public class CouchbaseService(IAuthenticationService authenticationService)
        : IDatabaseService
    {
        //authentication service
        private readonly IAuthenticationService _authenticationService = authenticationService;

        //manage state of the service
        private bool _databaseInitialised;

        //scope and collection information
        private readonly string _scopeName = "data";
        private readonly string _collectionName = "tasks";
        private Collection? _taskCollection;

        //replicator management
        private Replicator? _replicator;
        private ListenerToken? _replicatorStatusToken;

        //database information
        private Database? _database;

        //query management
        private ListenerToken? _queryListenerToken;
        private IQuery? _queryMyTasks;
        private IQuery? _queryAllTasks;

        //used for calculating RequestChanges
        private Dictionary<string, Item> _previousItems = new Dictionary<string, Item>();

        public SubscriptionType? CurrentSubscriptionType = null; 

        //current authenticated user
        public User? CurrentUser { get; private set; }

        //Couchbase Capella App Services Configuration
        public CouchbaseAppConfig? AppConfig { get; private set; }

        /// <summary>
        /// Adds a new task to the Couchbase collection.
        /// </summary>
        /// <param name="item">The item to be added to the collection.</param>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the user is not authenticated or the collection is not initialized.
        /// </exception>
        /// <remarks>
        /// This method converts the provided <see cref="Item"/> object to a JSON string and saves it as a 
        /// <see cref="MutableDocument"/> in the Couchbase collection. The user must be authenticated and the 
        /// collection must be initialized before calling this method.
        /// </remarks>
        public void AddTask(Item item)
        {
            ValidateUserCollection();
            var editDocument = _taskCollection?.GetDocument(item.Id);
            if (editDocument != null)
            {
                var mutEditDocument = editDocument.ToMutable();
                mutEditDocument.SetString("summary", item.Summary);
                _taskCollection?.Save(mutEditDocument);
            } else
            {
                //create new document
                var jsonString = item.ToJson();
                var mutableDocument = new MutableDocument(item.Id, jsonString);
                _taskCollection?.Save(mutableDocument);    
            }
        }



        /// <summary>
        /// Deletes a task from the Couchbase collection.
        /// </summary>
        /// <param name="item">The item to be deleted from the collection.</param>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the user is not authenticated, the collection is not initialized, or the item does not belong to the current user.
        /// </exception>
        /// <remarks>
        /// This method validates the current state, retrieves the document associated with the provided <see cref="Item"/> object,
        /// and deletes it from the Couchbase collection if it exists.
        /// </remarks>
        public void DeleteTask(Item? item)
        {
            if (item != null)
            {
                ValidateState(item);
                var document = _taskCollection?.GetDocument(item.Id);
                if (document != null)
                {
                    _taskCollection?.Delete(document);
                }
            }
        }


        /// <summary>
        /// Initializes the Couchbase service by setting up logging, reading the configuration file,
        /// and deserializing the configuration into the <see cref="CouchbaseAppConfig"/> object.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the application configuration file cannot be read.
        /// </exception>
        /// <remarks>
        /// This method enables detailed logging for debugging purposes and reads the Couchbase Capella
        /// configuration from a JSON file named "capellaConfig.json" located in the application package.
        /// The configuration is then deserialized into the <see cref="CouchbaseAppConfig"/> object.
        /// </remarks>
        public async Task Init()
        {
            //check to see if the platform is Android - if so register
            
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

        /// <summary>
        /// Initializes the Couchbase database for the current authenticated user.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the user is not authenticated or the application configuration is missing.
        /// </exception>
        /// <remarks>
        /// This method sets up the database, creates the necessary collections and indexes, 
        /// configures the replicator for synchronization with Couchbase Capella, and starts the replicator.
        /// </remarks>
        public void InitDatabase()
        {
            if (_databaseInitialised)
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
            var queryMyTasks = $"{query} WHERE item.ownerId = '{CurrentUser?.Username}'";
            _queryMyTasks = _database.CreateQuery(queryMyTasks);

            //create replicator config
            var targetEndpoint = new URLEndpoint(new Uri(AppConfig.EndpointUrl));
            var replicatorConfig = new ReplicatorConfiguration(targetEndpoint)
            {
                Continuous = true,
                ReplicatorType = ReplicatorType.PushAndPull,
                //configure authentication
                Authenticator = new BasicAuthenticator(CurrentUser!.Username, CurrentUser!.Password)
            };

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

            _databaseInitialised = true;
        }

        /// <summary>
        /// Determines whether the specified task belongs to the current authenticated user.
        /// </summary>
        /// <param name="item">The task item to check ownership for.</param>
        /// <returns>
        /// true if the task belongs to the current authenticated user; otherwise, false.
        /// </returns>
        /// <remarks>
        /// This method compares the OwnerId property of the provided <paramref name="item"/> with the
        ///  Username property of the current authenticated user to determine ownership.
        /// </remarks>
        public bool IsMyTask(Item item)
        {
            return item.OwnerId == CurrentUser?.Username;
        }
        
        /// <summary>
        /// Authenticates the user with the provided email and password.
        /// </summary>
        /// <param name="email">The email address of the user.</param>
        /// <param name="password">The password of the user.</param>
        /// <returns>A task representing the asynchronous login operation.</returns>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the application configuration is missing.
        /// </exception>
        /// <exception cref="UnauthorizedAccessException">
        /// Thrown when the login fails due to invalid email or password.
        /// </exception>
        /// <remarks>
        /// This method uses the authentication service to log in the user and sets the <see cref="CurrentUser"/> property
        /// if the login is successful. The application configuration must be available before calling this method.
        /// </remarks>
        public async Task LoginAsync(string email, string password)
        {
            if (AppConfig == null)
                throw new InvalidOperationException(
                    "Application configuration is missing. Can't get Capella App Services URL.");

            var user = await _authenticationService.Login(email, password, AppConfig);

            CurrentUser = user ?? throw new UnauthorizedAccessException("Login failed. Invalid email or password.");
            InitDatabase();
        }

        /// <summary>
        /// Logs out the current user and cleans up the Couchbase service resources.
        /// </summary>
        /// <remarks>
        /// This method sets the <see cref="CurrentUser"/> property to null, removes any active listeners,
        /// stops and disposes the replicator, disposes the task collection, and closes and disposes the database.
        /// </remarks>
        public void Logout()
        {
            //clear the previous items
            CurrentUser = null;
            CurrentSubscriptionType = null; 
            _previousItems.Clear();
            
            //clean up query stuff
            _queryListenerToken?.Remove();
            _queryListenerToken = null;
            _queryAllTasks = null;
            _queryMyTasks = null;

            //close the replicator
            _replicatorStatusToken?.Remove();
            _replicatorStatusToken = null;
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

            _databaseInitialised = false;
        }

        /// <summary>
        /// Pauses the synchronization process for the Couchbase replicator.
        /// </summary>
        /// <remarks>
        /// This method stops the Couchbase replicator, which pauses the synchronization of data
        /// between the local database and the remote Couchbase server.
        /// </remarks>
        public void PauseSync()
        {
            _replicator?.Stop();     
        }
        
        /// <summary>
        /// Resumes the synchronization process for the Couchbase replicator.
        /// </summary>
        /// <remarks>
        /// This method starts the Couchbase replicator, which resumes the synchronization of data
        /// between the local database and the remote Couchbase server.
        /// </remarks>
        public void ResumeSync()
        {
            _replicator?.Start();    
        }
        
                /// <summary>
        /// Retrieves the task list from the Couchbase collection and invokes the provided callback function with the results.
        /// </summary>
        /// <param name="subscriptionType">The type of subscription to determine which tasks to query (Mine or All).</param>
        /// <param name="callback">The callback function to be invoked with the results of the query.</param>
        /// <remarks>
        /// This method sets up a query listener based on the provided <paramref name="subscriptionType"/>.
        /// It processes the query results to determine initial results, insertions, updates, and deletions,
        /// and then invokes the callback function with the appropriate results.
        /// </remarks>
        public void SetTaskLiveQuery(
            SubscriptionType subscriptionType,
            Action<IResultsChange<Item>> callback)
        {
            if (CurrentSubscriptionType == null || CurrentSubscriptionType != subscriptionType)
            {
                var query = (subscriptionType == SubscriptionType.Mine) ? _queryMyTasks : _queryAllTasks;

                //remove the previous listener to clean up memory
                _queryListenerToken?.Remove();
                _queryListenerToken = null;

                //set the listener for live query
                _queryListenerToken = query?.AddChangeListener((sender, change) =>
                {
                    var isInitial = _previousItems.Count == 0;
                    var initialResults = new InitialResults<Item>();
                    var updatedResults = new UpdatedResults<Item>();

                    // used to track the current items which will
                    // become the next previousItemMap after this is complete
                    var currentItemsMap = new Dictionary<string, Item>();

                    // used to trim out items
                    // anything left over is a deletion
                    var previousItemsKeys = _previousItems.Keys.ToHashSet();
                    foreach (var row in change.Results)
                    {
                        var json = row.GetDictionary("item")?.ToJSON();
                        if (json == null) continue;
                        var item = JsonSerializer.Deserialize<Item>(json);
                        //validate serialization worked
                        if (item == null) continue;

                        //used to add the field that isn't serialized, but used in bindings of the UI
                        item.IsMine = item.OwnerId == CurrentUser?.Username;

                        //add item to currentMap used for filling the previousItems later
                        currentItemsMap.Add(item.Id, item);
                        // if it's initial, all items are insertions
                        if (isInitial)
                        {
                            initialResults.List.Add(item);
                        }
                        else
                        {
                            //check to see if it's an update 
                            if (_previousItems.TryGetValue(item.Id, out var previousItem))
                            {
                                if (item != previousItem)
                                {
                                    updatedResults.Changes.Add(item);
                                }
                            }
                            else
                            {
                                // if it's not an update, it's an insertion 
                                updatedResults.Insertions.Add(item);
                            }

                            //remove the item from the previous items
                            //required to determine deletions
                            _previousItems.Remove(item.Id);
                        }
                    }

                    // Determine deletions
                    if (!isInitial)
                    {
                        foreach (var previousItem in _previousItems.Values)
                        {
                            updatedResults.Deletions.Add(previousItem);
                        }
                    }

                    //fill the _previousItems with the current items
                    _previousItems.Clear();
                    foreach (var item in currentItemsMap)
                    {
                        _previousItems.Add(item.Key, item.Value);
                    }

                    if (isInitial)
                    {
                        callback(initialResults);
                    }
                    else
                    {
                        callback(updatedResults);
                    }
                });
                //change the current cached subscription type
                CurrentSubscriptionType = subscriptionType;
            }
        }
        
        /// <summary>
        /// Toggles the completion status of a task in the Couchbase collection.
        /// </summary>
        /// <param name="item">The item whose completion status is to be toggled.</param>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the user is not authenticated, the collection is not initialized, or the item does not belong to the current user.
        /// </exception>
        /// <remarks>
        /// This method retrieves the document associated with the provided <see cref="Item"/> object,
        /// toggles its "isComplete" status, and saves the updated document back to the Couchbase collection.
        /// </remarks>
        public void ToggleIsComplete(Item? item)
        {
            if (item != null)
            {
                const string isCompleteKey = "isComplete";
                ValidateState(item);
                var document = _taskCollection?.GetDocument(item.Id);
                if (document == null)
                {
                    throw new InvalidOperationException("Document not found");
                }

                var mutableDocument = document.ToMutable();
                var isComplete = document.GetBoolean(isCompleteKey);
                mutableDocument.SetBoolean(isCompleteKey, !isComplete);
                _taskCollection?.Save(mutableDocument);
            }
        }

        /// <summary>
        /// Validates that the current user is authenticated and the Couchbase collection is initialized.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the user is not authenticated or the collection is not initialized.
        /// </exception>
        /// <remarks>
        /// This method ensures that the <see cref="CurrentUser"/> property is not null and that the
        /// <see cref="_taskCollection"/> is initialized before performing operations on the collection.
        /// </remarks>
        private void ValidateUserCollection()
        {
            if (CurrentUser == null){
        
                throw new InvalidOperationException("User must be authenticated");
            }

            if (_taskCollection == null)
            {
                throw new InvalidOperationException("Collection can't be null, not initialized");
            }
        }
        
        /// <summary>
        /// Validates the current state of the service, ensuring the user is authenticated,
        /// the Couchbase collection is initialized, and the item belongs to the current user.
        /// </summary>
        /// <param name="item">The item to validate.</param>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the user is not authenticated, the collection is not initialized, or the item does not belong to the current user.
        /// </exception>
        /// <remarks>
        /// This method calls <see cref="ValidateUserCollection"/> to ensure the user is authenticated and the collection is initialized.
        /// It then checks if the provided <see cref="Item"/> object's OwnerId matches the current user's username.
        /// </remarks>
        private void ValidateState(Item item)
        {
            ValidateUserCollection();
            if (item.OwnerId != CurrentUser?.Username)
            {
                throw new InvalidOperationException("Error - user can't delete tasks they don't own");
            }
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