using BillApp.Core.Models;

namespace BillApp.Core.Interfaces.Services;

/// <summary>
/// Service for importing data from external files.
/// </summary>
public interface IImportService
{
    /// <summary>
    /// Parses and validates a CSV file without making any changes.
    /// </summary>
    /// <param name="filePath">Path to the CSV file.</param>
    /// <param name="mode">Import mode affects which accounts are considered "new".</param>
    /// <returns>Validation result with parsed data and any warnings/errors.</returns>
    Task<ImportValidationResult> ValidateFileAsync(string filePath, ImportMode mode);

    /// <summary>
    /// Executes the import based on a validated result.
    /// </summary>
    /// <param name="validationResult">The validated import data.</param>
    /// <param name="mode">Whether to add to existing data or replace all.</param>
    /// <returns>Result of the import execution.</returns>
    Task<ImportExecutionResult> ExecuteImportAsync(ImportValidationResult validationResult, ImportMode mode);
}
