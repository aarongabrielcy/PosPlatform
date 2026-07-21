using System.Xml.Linq;

namespace Pos.Architecture.Tests;

public class ArchitectureDependencyTests
{
    private const string SolutionFileName = "PosPlatform.sln";

    private static readonly string[] ExpectedProjectNames =
    [
        "Pos.Application",
        "Pos.Application.Tests",
        "Pos.Architecture.Tests",
        "Pos.Desktop",
        "Pos.Domain",
        "Pos.Domain.Tests",
        "Pos.Hardware",
        "Pos.Infrastructure",
        "Pos.Infrastructure.Tests",
    ];

    private static readonly Dictionary<string, string[]> ExpectedProductionReferences = new()
    {
        ["Pos.Domain"] = [],
        ["Pos.Application"] = ["Pos.Domain"],
        ["Pos.Infrastructure"] = ["Pos.Application", "Pos.Domain"],
        ["Pos.Hardware"] = ["Pos.Application", "Pos.Domain"],
        ["Pos.Desktop"] = ["Pos.Application", "Pos.Infrastructure", "Pos.Hardware"],
    };

    private static readonly Dictionary<string, string[]> ExpectedTestReferences = new()
    {
        ["Pos.Domain.Tests"] = ["Pos.Domain"],
        ["Pos.Application.Tests"] = ["Pos.Application", "Pos.Domain"],
        ["Pos.Architecture.Tests"] = [],
        ["Pos.Infrastructure.Tests"] = ["Pos.Domain", "Pos.Infrastructure"],
    };

    private static readonly string[] AllowedInfrastructurePackageReferences =
    [
        "Microsoft.EntityFrameworkCore.Design",
        "Microsoft.EntityFrameworkCore.Sqlite",
    ];

    private static readonly string[] AllowedDesktopPackageReferences =
    [
        "Microsoft.Extensions.Hosting",
    ];

    private static readonly Dictionary<string, string> ExpectedTargetFrameworks = new()
    {
        ["Pos.Domain"] = "net8.0",
        ["Pos.Application"] = "net8.0",
        ["Pos.Infrastructure"] = "net8.0",
        ["Pos.Hardware"] = "net8.0",
        ["Pos.Desktop"] = "net8.0-windows",
    };

    private static readonly string[] ProductionProjectNames =
    [
        "Pos.Domain",
        "Pos.Application",
        "Pos.Infrastructure",
        "Pos.Hardware",
        "Pos.Desktop",
    ];

    [Fact]
    public void SolutionShouldContainExactlyTheExpectedProjects()
    {
        var actual = GetSolutionProjectNames();
        var expected = ExpectedProjectNames.OrderBy(n => n, StringComparer.Ordinal).ToArray();

        Assert.True(
            expected.SequenceEqual(actual),
            $"Proyectos esperados: [{string.Join(", ", expected)}]. " +
            $"Proyectos encontrados: [{string.Join(", ", actual)}].");
    }

    [Theory]
    [InlineData("Pos.Domain")]
    [InlineData("Pos.Application")]
    [InlineData("Pos.Infrastructure")]
    [InlineData("Pos.Hardware")]
    public void ProductionProjectShouldTargetNet8(string projectName)
    {
        var actualFramework = GetTargetFramework(projectName);
        var expectedFramework = ExpectedTargetFrameworks[projectName];

        Assert.True(
            string.Equals(expectedFramework, actualFramework, StringComparison.Ordinal),
            $"Proyecto: {projectName}. TargetFramework esperado: {expectedFramework}. " +
            $"TargetFramework encontrado: {actualFramework}.");
    }

    [Fact]
    public void DesktopProjectShouldTargetNet8Windows()
    {
        var actualFramework = GetTargetFramework("Pos.Desktop");
        var expectedFramework = ExpectedTargetFrameworks["Pos.Desktop"];

        Assert.True(
            string.Equals(expectedFramework, actualFramework, StringComparison.Ordinal),
            $"Proyecto: Pos.Desktop. TargetFramework esperado: {expectedFramework}. " +
            $"TargetFramework encontrado: {actualFramework}.");
    }

    [Fact]
    public void DesktopProjectShouldRemainWpfExecutable()
    {
        var document = LoadProjectXml("Pos.Desktop");

        var outputType = document.Descendants("OutputType").Select(e => e.Value.Trim()).FirstOrDefault();
        var useWpf = document.Descendants("UseWPF").Select(e => e.Value.Trim()).FirstOrDefault();

        Assert.True(
            string.Equals(outputType, "WinExe", StringComparison.Ordinal),
            $"Proyecto: Pos.Desktop. OutputType esperado: WinExe. OutputType encontrado: {outputType ?? "(ninguno)"}.");

        Assert.True(
            string.Equals(useWpf, "true", StringComparison.OrdinalIgnoreCase),
            $"Proyecto: Pos.Desktop. UseWPF esperado: true. UseWPF encontrado: {useWpf ?? "(ninguno)"}.");
    }

    [Theory]
    [InlineData("Pos.Domain")]
    [InlineData("Pos.Application")]
    [InlineData("Pos.Infrastructure")]
    [InlineData("Pos.Hardware")]
    [InlineData("Pos.Desktop")]
    public void ProductionProjectShouldHaveExactlyExpectedReferences(string projectName)
    {
        AssertReferencesMatch(projectName, ExpectedProductionReferences[projectName]);
    }

    [Theory]
    [InlineData("Pos.Domain.Tests")]
    [InlineData("Pos.Application.Tests")]
    [InlineData("Pos.Architecture.Tests")]
    [InlineData("Pos.Infrastructure.Tests")]
    public void TestProjectShouldHaveExactlyExpectedReferences(string projectName)
    {
        AssertReferencesMatch(projectName, ExpectedTestReferences[projectName]);
    }

    [Fact]
    public void ArchitectureTestsShouldNotReferenceProductionProjectsDirectly()
    {
        var actual = GetProjectReferenceNames("Pos.Architecture.Tests");
        var productionReferencesFound = actual.Intersect(ProductionProjectNames, StringComparer.Ordinal).ToArray();

        Assert.True(
            productionReferencesFound.Length == 0,
            $"Proyecto: Pos.Architecture.Tests. No debe referenciar proyectos de producción. " +
            $"Referencias de producción encontradas: [{string.Join(", ", productionReferencesFound)}].");
    }

    [Fact]
    public void ProductionProjectsShouldNotContainCircularReferences()
    {
        var graph = ProductionProjectNames.ToDictionary(
            name => name,
            name => GetProjectReferenceNames(name).Intersect(ProductionProjectNames, StringComparer.Ordinal).ToArray());

        foreach (var projectName in ProductionProjectNames)
        {
            var visited = new HashSet<string>(StringComparer.Ordinal);
            var path = new List<string>();

            Assert.False(
                HasCycle(projectName, graph, visited, path),
                $"Se detectó una referencia circular que involucra a {projectName}: {string.Join(" -> ", path)}.");
        }
    }

    [Theory]
    [InlineData("Pos.Domain")]
    [InlineData("Pos.Application")]
    [InlineData("Pos.Hardware")]
    public void ProductionProjectsShouldNotContainPackageReferences(string projectName)
    {
        var document = LoadProjectXml(projectName);
        var packageReferences = document.Descendants("PackageReference")
            .Select(e => e.Attribute("Include")?.Value ?? "(sin nombre)")
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToArray();

        Assert.True(
            packageReferences.Length == 0,
            $"Proyecto: {projectName}. No debe contener PackageReference. " +
            $"PackageReference encontrados: [{string.Join(", ", packageReferences)}].");
    }

    [Fact]
    public void InfrastructureProjectShouldOnlyContainAllowedEfCorePackageReferences()
    {
        var document = LoadProjectXml("Pos.Infrastructure");
        var packageNames = document.Descendants("PackageReference")
            .Select(e => e.Attribute("Include")?.Value ?? "(sin nombre)")
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToArray();

        var expected = AllowedInfrastructurePackageReferences.OrderBy(n => n, StringComparer.Ordinal).ToArray();

        Assert.True(
            expected.SequenceEqual(packageNames),
            $"Proyecto: Pos.Infrastructure. PackageReference esperados: [{string.Join(", ", expected)}]. " +
            $"PackageReference encontrados: [{string.Join(", ", packageNames)}].");
    }

    [Fact]
    public void DesktopProjectShouldOnlyContainAllowedHostingPackageReferences()
    {
        var document = LoadProjectXml("Pos.Desktop");
        var packageNames = document.Descendants("PackageReference")
            .Select(e => e.Attribute("Include")?.Value ?? "(sin nombre)")
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToArray();

        var expected = AllowedDesktopPackageReferences.OrderBy(n => n, StringComparer.Ordinal).ToArray();

        Assert.True(
            expected.SequenceEqual(packageNames),
            $"Proyecto: Pos.Desktop. PackageReference esperados: [{string.Join(", ", expected)}]. " +
            $"PackageReference encontrados: [{string.Join(", ", packageNames)}].");
    }

    [Fact]
    public void InfrastructureProjectEfCoreDesignPackageShouldBePrivateAssetsAll()
    {
        var document = LoadProjectXml("Pos.Infrastructure");
        var designPackageReference = document.Descendants("PackageReference")
            .FirstOrDefault(e => string.Equals(
                e.Attribute("Include")?.Value,
                "Microsoft.EntityFrameworkCore.Design",
                StringComparison.Ordinal));

        Assert.True(
            designPackageReference is not null,
            "Proyecto: Pos.Infrastructure. No se encontró el PackageReference Microsoft.EntityFrameworkCore.Design.");

        var privateAssets = designPackageReference!.Element("PrivateAssets")?.Value.Trim()
            ?? designPackageReference.Attribute("PrivateAssets")?.Value.Trim();

        Assert.True(
            string.Equals(privateAssets, "all", StringComparison.OrdinalIgnoreCase),
            "Proyecto: Pos.Infrastructure. Microsoft.EntityFrameworkCore.Design debe tener PrivateAssets=\"all\". " +
            $"Valor encontrado: {privateAssets ?? "(ninguno)"}.");
    }

    private static bool HasCycle(
        string projectName,
        Dictionary<string, string[]> graph,
        HashSet<string> visited,
        List<string> path)
    {
        if (path.Contains(projectName, StringComparer.Ordinal))
        {
            path.Add(projectName);
            return true;
        }

        if (!visited.Add(projectName))
        {
            return false;
        }

        path.Add(projectName);

        foreach (var dependency in graph[projectName])
        {
            if (HasCycle(dependency, graph, visited, path))
            {
                return true;
            }
        }

        path.RemoveAt(path.Count - 1);
        return false;
    }

    private static void AssertReferencesMatch(string projectName, string[] expectedReferences)
    {
        var actual = GetProjectReferenceNames(projectName);
        var expected = expectedReferences.OrderBy(n => n, StringComparer.Ordinal).ToArray();

        Assert.True(
            expected.SequenceEqual(actual),
            $"Proyecto: {projectName}. Referencias esperadas: [{string.Join(", ", expected)}]. " +
            $"Referencias encontradas: [{string.Join(", ", actual)}].");
    }

    private static string[] GetProjectReferenceNames(string projectName)
    {
        var document = LoadProjectXml(projectName);

        return document.Descendants("ProjectReference")
            .Select(e => e.Attribute("Include")?.Value ?? string.Empty)
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Select(path => Path.GetFileNameWithoutExtension(path.Replace('\\', Path.DirectorySeparatorChar)))
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToArray();
    }

    private static string? GetTargetFramework(string projectName)
    {
        var document = LoadProjectXml(projectName);
        return document.Descendants("TargetFramework").Select(e => e.Value.Trim()).FirstOrDefault();
    }

    private static XDocument LoadProjectXml(string projectName)
    {
        var projectPath = Path.Combine(GetRepositoryRoot(), projectName, $"{projectName}.csproj");

        Assert.True(File.Exists(projectPath), $"No se encontró el archivo de proyecto: {projectPath}.");

        return XDocument.Load(projectPath);
    }

    private static string[] GetSolutionProjectNames()
    {
        var solutionPath = Path.Combine(GetRepositoryRoot(), SolutionFileName);

        Assert.True(File.Exists(solutionPath), $"No se encontró el archivo de solución: {solutionPath}.");

        var projectNames = new List<string>();

        foreach (var line in File.ReadAllLines(solutionPath))
        {
            if (!line.StartsWith("Project(", StringComparison.Ordinal))
            {
                continue;
            }

            var parts = line.Split('"');

            if (parts.Length >= 4)
            {
                projectNames.Add(parts[3]);
            }
        }

        return projectNames.OrderBy(n => n, StringComparer.Ordinal).ToArray();
    }

    private static string GetRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, SolutionFileName);

            if (File.Exists(candidate))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException(
            $"No se pudo localizar {SolutionFileName} recorriendo hacia arriba desde {AppContext.BaseDirectory}.");
    }
}
