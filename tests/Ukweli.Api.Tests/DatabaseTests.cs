namespace Ukweli.Api.Tests;

/// <summary>
/// Tests that touch Postgres share this xUnit collection so they do not run in
/// parallel against the same database.
/// </summary>
[CollectionDefinition("database")]
public sealed class DatabaseTests;
