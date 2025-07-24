namespace ProjectDiff.Tests.Utils;

public sealed class SetupResult<T> : IDisposable
{
    public SetupResult(T result, TestRepository repository)
    {
        Result = result;
        Repository = repository;
    }

    public T Result { get; }
    public TestRepository Repository { get; }

    public void Deconstruct(out T result, out TestRepository repository)
    {
        result = Result;
        repository = Repository;
    }

    public void Dispose()
    {
        Repository.Dispose();
    }
}
