namespace ReportPoc.Models;

public sealed class ReportCatalogEntry
{
    public string? ReportCode { get; set; }
    public string? Name { get; set; }
    public string? Family { get; set; }
    public string? Variant { get; set; }
    public string? RootEntity { get; set; }
    public string? Module { get; set; }
    public string? ActiveVersion { get; set; }
    public string? PaperSize { get; set; }
    public string? Orientation { get; set; }
    public string? DefinitionMode { get; set; }
}
