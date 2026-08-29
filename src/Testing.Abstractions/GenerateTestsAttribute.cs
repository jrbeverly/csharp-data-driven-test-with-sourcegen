using System;

namespace Testing.Abstractions;

[AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = false)]
public sealed class GenerateTestsAttribute : Attribute
{
    public string DatasetId { get; }

    public GenerateTestsAttribute(string datasetId)
    {
        DatasetId = datasetId;
    }
}
