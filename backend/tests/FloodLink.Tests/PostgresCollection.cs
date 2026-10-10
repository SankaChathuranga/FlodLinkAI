using Xunit;

namespace FloodLink.Tests;

/// <summary>
/// Test classes that drop and recreate the shared floodlink_test database must not run in
/// parallel with each other, so they all join this collection.
/// </summary>
[CollectionDefinition(Name)]
public sealed class PostgresCollection
{
    public const string Name = "Postgres";
}
