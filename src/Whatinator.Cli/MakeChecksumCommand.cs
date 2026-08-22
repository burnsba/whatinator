using Whatinator.Core;
using Whatinator.Core.Checksums;

namespace Whatinator.Cli;

/// <summary>Implements the <c>make-checksum</c> command.</summary>
internal static class MakeChecksumCommand
{
    /// <summary>Generates <c>checksum_sha256.txt</c> from a folder's current contents.</summary>
    /// <param name="args">Remaining arguments after the command name.</param>
    /// <returns>The process exit code.</returns>
    public static int Run(string[] args)
    {
        var dest = CommandLineOptions.GetValue(args, "--dest") ?? ".";

        if (!Directory.Exists(dest))
        {
            Console.Error.WriteLine($"Directory not found: '{dest}'.");
            return 1;
        }

        var count = ChecksumFile.Generate(dest);
        Console.WriteLine($"Hashed {count} file(s) into {Path.Combine(dest, "checksum_sha256.txt")}");
        return 0;
    }
}
