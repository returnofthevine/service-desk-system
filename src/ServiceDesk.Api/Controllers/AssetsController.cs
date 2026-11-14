using System.Globalization;
using System.Text;
using CsvHelper;
using CsvHelper.Configuration;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using ServiceDesk.Api.Data;
using ServiceDesk.Api.Domain;

namespace ServiceDesk.Api.Controllers;

[ApiController]
[Authorize(Roles = "Technician")]
[Route("api/assets")]
public sealed class AssetsController(ServiceDeskDbContext db) : ControllerBase
{
    private const int MaxFileBytes = 1_048_576;
    private const int MaxDataRecords = 1_000;
    private const int SqliteConstraintUnique = 2067;
    private static readonly string[] Header =
        ["assetTag", "hostname", "bitLockerState", "tpmState", "lastCheckedAtUtc"];

    [HttpPost("import")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(2_097_152)]
    public async Task<IActionResult> Import([FromForm] IFormFile file, CancellationToken cancellationToken)
    {
        if (file.Length > MaxFileBytes)
        {
            return Problem(statusCode: StatusCodes.Status413PayloadTooLarge, detail: "The CSV file must be 1 MiB or smaller.");
        }

        // Parse into a pending batch and collect all row errors before writing anything.
        var assets = new List<Asset>();
        var errors = new List<ImportError>();
        var seenTags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        try
        {
            await using var stream = file.OpenReadStream();
            var encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: true, throwOnInvalidBytes: true);
            using var text = new StreamReader(stream, encoding, detectEncodingFromByteOrderMarks: false);
            using var csv = new CsvReader(text, new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                ExceptionMessagesContainRawData = false,
            });

            if (!await csv.ReadAsync() || !csv.ReadHeader() || csv.HeaderRecord is not { } header || !header.SequenceEqual(Header))
            {
                return Problem(statusCode: StatusCodes.Status400BadRequest, detail: $"Use this CSV header: {string.Join(',', Header)}.");
            }

            var existingTags = await db.Assets.AsNoTracking().Select(asset => asset.AssetTag).ToListAsync(cancellationToken);
            var registeredTags = new HashSet<string>(existingTags, StringComparer.OrdinalIgnoreCase);
            var row = 1;
            while (await csv.ReadAsync())
            {
                cancellationToken.ThrowIfCancellationRequested();
                row++;
                if (row > MaxDataRecords + 1)
                {
                    return Problem(statusCode: StatusCodes.Status413PayloadTooLarge, detail: "The CSV file must have 1,000 data records or fewer.");
                }

                // Materialize the fields so malformed quoting is detected even if the column count differs.
                var fields = csv.Parser.Record ?? [];
                if (fields.Length != Header.Length)
                {
                    errors.Add(new ImportError(row, "record", "Each record must contain five fields."));
                    continue;
                }

                var asset = ValidateRecord(fields, row, seenTags, registeredTags, errors);
                if (asset is not null)
                {
                    assets.Add(asset);
                }
            }
        }
        catch (Exception exception) when (exception is CsvHelperException or DecoderFallbackException)
        {
            return Problem(statusCode: StatusCodes.Status400BadRequest, detail: "The upload could not be read as a UTF-8 CSV file.");
        }

        if (errors.Count > 0)
        {
            return UnprocessableEntity(new
            {
                Detail = "Correct the listed rows and upload again.",
                CreatedCount = 0,
                Errors = errors,
            });
        }

        // Save the validated batch together. SQLite's unique index also catches concurrent imports.
        db.Assets.AddRange(assets);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (exception.InnerException is SqliteException
            { SqliteExtendedErrorCode: SqliteConstraintUnique })
        {
            return Problem(statusCode: StatusCodes.Status409Conflict, detail: "An asset tag was registered during this import. Check the tags and upload again.");
        }

        return StatusCode(StatusCodes.Status201Created, new
        {
            CreatedCount = assets.Count,
            Assets = assets.Select(asset => new { asset.Id, asset.AssetTag }).ToArray(),
        });
    }

    private static Asset? ValidateRecord(
        string[] fields,
        int row,
        HashSet<string> seenTags,
        HashSet<string> registeredTags,
        List<ImportError> errors)
    {
        // Normalize the identifier and state values before comparing or saving them.
        var previousErrorCount = errors.Count;
        var assetTag = fields[0].Trim();
        var hostname = fields[1].Trim();
        var bitLocker = fields[2].Trim().ToLowerInvariant();
        var tpm = fields[3].Trim().ToLowerInvariant();

        if (assetTag.Length is < 1 or > 24 || !assetTag.All(character => char.IsAsciiLetterOrDigit(character) || character == '-'))
        {
            errors.Add(new ImportError(row, "assetTag", "Use 1–24 ASCII letters, digits, or hyphens."));
        }
        else
        {
            assetTag = assetTag.ToUpperInvariant();
            if (!seenTags.Add(assetTag))
            {
                errors.Add(new ImportError(row, "assetTag", $"Duplicate tag {assetTag} in this file."));
            }
            else if (registeredTags.Contains(assetTag))
            {
                errors.Add(new ImportError(row, "assetTag", $"Tag {assetTag} is already registered."));
            }
        }

        if (hostname.Length is < 1 or > 63)
        {
            errors.Add(new ImportError(row, "hostname", "Use 1–63 characters."));
        }

        if (bitLocker is not ("protected" or "suspended" or "off" or "unknown"))
        {
            errors.Add(new ImportError(row, "bitLockerState", "Use protected, suspended, off, or unknown."));
        }

        if (tpm is not ("ready" or "not-ready" or "missing" or "unknown"))
        {
            errors.Add(new ImportError(row, "tpmState", "Use ready, not-ready, missing, or unknown."));
        }

        if (!DateTime.TryParseExact(fields[4].Trim(), "yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var lastCheckedAtUtc))
        {
            errors.Add(new ImportError(row, "lastCheckedAtUtc", "Use a UTC timestamp such as 2026-11-01T13:30:00Z."));
        }

        return errors.Count == previousErrorCount ? new Asset
        {
            AssetTag = assetTag,
            Hostname = hostname,
            BitLockerState = bitLocker,
            TpmState = tpm,
            LastCheckedAtUtc = lastCheckedAtUtc,
        } : null;
    }

    private sealed record ImportError(int Row, string Field, string Message);
}
