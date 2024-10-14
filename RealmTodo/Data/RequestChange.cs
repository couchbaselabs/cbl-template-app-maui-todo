namespace RealmTodo.Data;

public interface IResultsChange<T>;

internal class InitialResults<T> : IResultsChange<T>
{
    public IList<T> List { get; set; } = new List<T>();
}

internal class UpdatedResults<T> : IResultsChange<T> {
    public IList<T> Insertions { get; set; } = new List<T>();
    public IList<T> Deletions { get; set; } = new List<T>();
    public IList<T> Changes { get; set; } = new List<T>();
}