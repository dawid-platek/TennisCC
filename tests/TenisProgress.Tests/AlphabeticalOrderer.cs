using Xunit.Abstractions;
using Xunit.Sdk;

namespace TenisProgress.Tests;

/// <summary>Runs tests in a class by method name so a scenario can build on earlier steps.</summary>
public sealed class AlphabeticalOrderer : ITestCaseOrderer
{
    public IEnumerable<TTestCase> OrderTestCases<TTestCase>(IEnumerable<TTestCase> testCases) where TTestCase : ITestCase =>
        testCases.OrderBy(t => t.TestMethod.Method.Name, StringComparer.Ordinal);
}
