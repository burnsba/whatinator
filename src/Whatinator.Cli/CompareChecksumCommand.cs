using Whatinator.Core;
using Whatinator.Core.Checksums;

namespace Whatinator.Cli;

/// <summary>Implements the <c>compare-checksum</c> command.</summary>
internal static class CompareChecksumCommand
{
    /// <summary>Reads a folder's <c>checksum_sha256.txt</c> and compares it against what's actually there.</summary>
    /// <param name="args">Remaining arguments after the command name.</param>
    /// <returns>The process exit code: <c>0</c> if clean, <c>1</c> otherwise.</returns>
    public static int Run(string[] args)
    {
        var options = ParsedOptions.Parse(args, OptionSpec.Value("--dest"));
        if (options.HasErrors)
        {
            foreach (var error in options.Errors)
            {
                Console.Error.WriteLine(error);
            }

            return 1;
        }

        var dest = options.GetValue("--dest") ?? ".";

        ChecksumCompareResult result;
        try
        {
            result = ChecksumFile.Compare(dest);
        }
        catch (FileNotFoundException ex)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }

        foreach (var relativePath in result.Matched)
        {
            Console.WriteLine($"match: {relativePath}");
        }

        if (result.Mismatched.Count > 0)
        {
            Console.WriteLine($"Mismatched: {result.Mismatched.Count}");
            foreach (var mismatch in result.Mismatched)
            {
                Console.WriteLine($"  {mismatch.RelativePath} (expected {mismatch.Expected}, got {mismatch.Actual})");
            }
        }

        if (result.Missing.Count > 0)
        {
            Console.WriteLine($"Missing: {result.Missing.Count}");
            foreach (var relativePath in result.Missing)
            {
                Console.WriteLine($"  {relativePath}");
            }
        }

        if (result.Malformed.Count > 0)
        {
            Console.WriteLine($"Malformed manifest entries (rejected -- escape the target folder): {result.Malformed.Count}");
            foreach (var relativePath in result.Malformed)
            {
                Console.WriteLine($"  {relativePath}");
            }
        }

        if (result.Extra.Count > 0)
        {
            Console.WriteLine($"Extra (not in manifest): {result.Extra.Count}");
            foreach (var relativePath in result.Extra)
            {
                Console.WriteLine($"  {relativePath}");
            }
        }

        if (result.IsClean)
        {
            Console.WriteLine("OK -- checksum_sha256.txt matches the folder contents.");
            return 0;
        }

        return 1;
    }
}
