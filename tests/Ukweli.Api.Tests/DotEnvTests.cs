namespace Ukweli.Api.Tests;

/// <summary>
/// The .env reader has to handle everything <c>.env.example</c> actually
/// contains — comments, blank lines, a base64 secret ending in '=', inline
/// comments — and must never override a variable the environment already set.
/// </summary>
public class DotEnvTests
{
    [Theory]
    [InlineData("PORT=8787", "PORT", "8787")]
    [InlineData("  PORT = 8787  ", "PORT", "8787")]
    [InlineData("export PORT=8787", "PORT", "8787")]
    [InlineData("APP_URL=http://localhost:5173", "APP_URL", "http://localhost:5173")]
    [InlineData("QUOTED=\"with spaces\"", "QUOTED", "with spaces")]
    [InlineData("SINGLE='with spaces'", "SINGLE", "with spaces")]
    [InlineData("EMPTY=", "EMPTY", "")]
    [InlineData("WITH_COMMENT=value # trailing note", "WITH_COMMENT", "value")]
    public void ParsesTheFormsUsedByEnvExample(string line, string expectedKey, string expectedValue)
    {
        Assert.True(DotEnv.TryParseLine(line, out var key, out var value));
        Assert.Equal(expectedKey, key);
        Assert.Equal(expectedValue, value);
    }

    [Fact]
    public void KeepsABase64SecretIntact()
    {
        // openssl rand -base64 32 produces values containing '=' and '/'.
        const string secret = "Zm9vYmFyL2Jhei9xdXV4L2NvcmdlL2dyYXVsdA==";

        Assert.True(DotEnv.TryParseLine($"BETTER_AUTH_SECRET={secret}", out var key, out var value));
        Assert.Equal("BETTER_AUTH_SECRET", key);
        Assert.Equal(secret, value);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("# a comment")]
    [InlineData("   # an indented comment")]
    [InlineData("no-equals-sign")]
    [InlineData("=novalue")]
    public void SkipsLinesThatAreNotAssignments(string line)
    {
        Assert.False(DotEnv.TryParseLine(line, out _, out _));
    }

    [Fact]
    public void DoesNotTouchTheProcessEnvironment()
    {
        var name = $"UKWELI_TEST_{Guid.NewGuid():N}";
        var file = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.env");

        try
        {
            File.WriteAllText(file, $"{name}=from-dotenv\n");

            var values = DotEnv.Read(file);

            // The file is a configuration source, not a way to rewrite the
            // process. Mutating the environment would make .env outrank a real
            // deployment variable and leak between hosts in one test process.
            Assert.Equal("from-dotenv", values[name]);
            Assert.Null(Environment.GetEnvironmentVariable(name));
        }
        finally
        {
            File.Delete(file);
        }
    }

    [Fact]
    public void ReadsEveryAssignmentInAFile()
    {
        var file = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.env");

        try
        {
            File.WriteAllText(file, "# comment\n\nFIRST=one\nexport SECOND=two\nTHIRD=\n");

            var values = DotEnv.Read(file);

            Assert.Equal("one", values["FIRST"]);
            Assert.Equal("two", values["SECOND"]);
            Assert.Equal(string.Empty, values["THIRD"]);
            Assert.Equal(3, values.Count);
        }
        finally
        {
            File.Delete(file);
        }
    }

    [Fact]
    public void LetsALaterLineWinOverAnEarlierOne()
    {
        var file = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.env");

        try
        {
            File.WriteAllText(file, "PORT=1111\nPORT=8787\n");

            Assert.Equal("8787", DotEnv.Read(file)["PORT"]);
        }
        finally
        {
            File.Delete(file);
        }
    }

    [Fact]
    public void ReturnsNullWhenNoEnvFileExistsAbove()
    {
        var isolated = Directory.CreateTempSubdirectory("ukweli-dotenv-");

        try
        {
            // maxDepth 1 keeps the search inside the temp directory, so the
            // repository's own .env cannot make this pass by accident.
            Assert.Null(DotEnv.FindFile(isolated.FullName, maxDepth: 1));
        }
        finally
        {
            isolated.Delete(recursive: true);
        }
    }

    [Fact]
    public void FindsAnEnvFileInAParentDirectory()
    {
        var root = Directory.CreateTempSubdirectory("ukweli-dotenv-");
        var nested = Directory.CreateDirectory(Path.Combine(root.FullName, "bin", "Debug", "net10.0"));

        try
        {
            var expected = Path.Combine(root.FullName, ".env");
            File.WriteAllText(expected, "FOUND=yes\n");

            var found = DotEnv.FindFile(nested.FullName);

            Assert.NotNull(found);
            Assert.Equal("yes", DotEnv.Read(found)["FOUND"]);
        }
        finally
        {
            root.Delete(recursive: true);
        }
    }
}
