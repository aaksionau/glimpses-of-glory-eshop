using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Http;

namespace GlimpsesOfGlory.UnitTests;

// ISession double backed by a dictionary, for page models that use the checkout session store.
internal sealed class InMemorySession : ISession
{
    private readonly Dictionary<string, byte[]> _values = [];

    public bool IsAvailable => true;

    public string Id => "test-session";

    public IEnumerable<string> Keys => _values.Keys;

    public void Clear() => _values.Clear();

    public Task CommitAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task LoadAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public void Remove(string key) => _values.Remove(key);

    public void Set(string key, byte[] value) => _values[key] = value;

    public bool TryGetValue(string key, [NotNullWhen(true)] out byte[]? value) => _values.TryGetValue(key, out value);
}
