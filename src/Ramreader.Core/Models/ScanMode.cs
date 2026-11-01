namespace Ramreader.Core.Models;

/// <summary>Cara menyaring hasil pada scan lanjutan.</summary>
public enum ScanMode
{
    Exact,
    Changed,
    Unchanged,
    Increased,
    Decreased,
    Between
}