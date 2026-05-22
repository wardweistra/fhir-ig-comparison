namespace FhirIgComparison.Core.Models;

public sealed class FileEntry
{
    public required string Path { get; init; }
    public required byte[] Bytes { get; init; }
}
