namespace Whatinator.Cli;

/// <summary>One paragraph or option row within a <see cref="HelpCommand"/>'s description, rendered in the right-hand column by <see cref="HelpFormatter"/>.</summary>
internal abstract record HelpBlock;

/// <summary>A run of prose that word-wraps to fill the available width.</summary>
/// <param name="Text">The paragraph text, unwrapped.</param>
internal sealed record HelpParagraph(string Text) : HelpBlock;

/// <summary>One <c>--flag</c> and its description, rendered with the flag left-aligned in its own sub-column and the description hanging-indented beneath it if it wraps.</summary>
/// <param name="Flag">The flag text, e.g. <c>--dest &lt;path&gt;</c>.</param>
/// <param name="Text">The flag's description.</param>
internal sealed record HelpOption(string Flag, string Text) : HelpBlock;

/// <summary>One command's help entry.</summary>
/// <param name="UsageLines">The command's usage syntax, printed verbatim (never wrapped) in the left column -- one entry per line, already carrying whatever leading spaces are needed to align a continuation under the first line's own tokens.</param>
/// <param name="Body">The command's description, wrapped to fill the right column.</param>
internal sealed record HelpCommand(IReadOnlyList<string> UsageLines, IReadOnlyList<HelpBlock> Body);

/// <summary>One titled group of commands in <c>--help</c> output (e.g. "Setup", "Catalog").</summary>
/// <param name="Title">The section heading.</param>
/// <param name="Commands">The commands listed under this heading, in display order.</param>
internal sealed record HelpSection(string Title, IReadOnlyList<HelpCommand> Commands);

/// <summary>The static content shown by <c>whatinator --help</c> -- see <see cref="HelpFormatter"/> for how it's laid out.</summary>
internal static class HelpContent
{
    /// <summary>Every section, in display order.</summary>
    public static IReadOnlyList<HelpSection> Sections { get; } =
    [
        new HelpSection("Setup", [
            new HelpCommand(["list-device"], [new HelpParagraph("List available optical drives")]),
            new HelpCommand(["offset-find [--device <path>]"], [
                new HelpParagraph("Auto-detect the drive's sample read offset against the inserted disc (must be a disc already in the AccurateRip database) and save it to the per-drive config map"),
            ]),
        ]),
        new HelpSection("Catalog", [
            new HelpCommand(["disc-info [--device <path>]", "          [--ask]"], [
                new HelpParagraph("Read a disc's TOC and MusicBrainz disc ID, and (best-effort) its artist/title/track listing (default device: from config, else /dev/sr1)"),
                new HelpOption("--ask", "Prompt to pick among multiple MusicBrainz matches instead of using the first automatically"),
                new HelpParagraph("See also: toc, for the frame-accurate technical read rip/pipeline use internally"),
            ]),
            new HelpCommand(["toc [--device <path>]", "    [--full]"], [
                new HelpParagraph("Read a disc's frame-accurate TOC via cdrdao (fast by default: track start/length only; --full also scans for pregaps, much slower)"),
                new HelpParagraph("See also: disc-info, for a human-facing MusicBrainz artist/title/track lookup"),
            ]),
            new HelpCommand(["make-releaseinfo [options]"], [
                new HelpParagraph("Look up disc metadata on MusicBrainz (or load a supplied file), best-effort enrich with a Discogs match, and write releaseinfo.json"),
                new HelpOption("--device <path>", "Device to read (as above)"),
                new HelpOption("--releaseinfo <path>", "Use this file's content instead of doing a fresh MusicBrainz/Discogs lookup"),
                new HelpOption("--dest <path>", "Output folder (default: .)"),
            ]),
            new HelpCommand(["update-metadata", "  --releaseinfo <path>", "  --dest <path>"], [
                new HelpParagraph("Apply a corrected releaseinfo.json to an already-packaged FLAC/MP3 release folder: backs up the folder's previous releaseinfo.json to releaseinfo.bak, refreshes id.txt/checksum_sha256.txt, and renames the folder if its computed name changed (e.g. year correction). Prompts for confirmation if the artist or title differs from what's there."),
            ]),
            new HelpCommand(["id-txt --releaseinfo <path>", "       [--dest <path>]"], [
                new HelpParagraph("Generate id.txt from a saved releaseinfo.json"),
                new HelpOption("--dest <path>", "Output folder (default: .)"),
            ]),
        ]),
        new HelpSection("Convert", [
            new HelpCommand(["pipeline [options]"], [
                new HelpParagraph("Full rip -> FLAC-packaging -> MP3 pipeline in one command: resolves metadata itself, loops over every disc of a multi-disc release, and runs the same rip step the standalone rip command does for each one. Use the standalone rip/flac/mp3 commands instead only to run one stage alone."),
                new HelpOption("--releaseinfo <path>", "Use this file instead of looking up on MusicBrainz"),
                new HelpOption("--device <path>", "Device to read"),
                new HelpOption("--dest <path>", "Output folder (default: .)"),
                new HelpOption("--multi <start>-<end>", "Disc range for this run (default: every disc on the release)"),
                new HelpOption("--no-flac", "Skip FLAC packaging; the raw rip is kept, never deleted"),
                new HelpOption("--no-mp3", "Skip MP3 encoding for this run"),
                new HelpOption("--keep-wav", "Keep each track's WAV alongside its FLAC instead of deleting it"),
            ]),
            new HelpCommand(["rip --releaseinfo <path>", "    [options]"], [
                new HelpParagraph("Rip-only, one disc: cdrdao TOC read + cd-paranoia test/copy reads + flac --verify encode + AccurateRip database verification, plus an EAC-style rip log. No FLAC/MP3 packaging (run flac/mp3 after) and no metadata lookup (needs --releaseinfo). For the full rip -> FLAC -> MP3 workflow in one command, use pipeline instead."),
                new HelpOption("--device <path>", "Device to read (as above)"),
                new HelpOption("--dest <path>", "Output folder (default: .)"),
                new HelpOption("--disc <N>", "Disc number (required if the release has more than one disc)"),
                new HelpOption("--keep-wav", "Keep each track's WAV alongside its FLAC instead of deleting it after encode"),
            ]),
            new HelpCommand(["flac --releaseinfo <path>", "     --source <path>", "     [options]"], [
                new HelpParagraph("Package a rip's FLAC output into the project's standard folder layout (FLAC + WAV files if --keep-wav was used, plus id.txt/checksum_sha256.txt/.m3u/releaseinfo.json copy/cover art)"),
                new HelpOption("--source <path>", "The rip command's --dest"),
                new HelpOption("--dest <path>", "Parent folder for the release's container folder (default: .)"),
                new HelpOption("--disc <N>", "Disc number (required if the release has more than one disc)"),
            ]),
            new HelpCommand(["mp3 --releaseinfo <path>", "    --source <path>", "    [options]"], [
                new HelpParagraph("Encode a FLAC folder (from the flac command's output) to V0 MP3 via lame, in the project's standard folder layout (id.txt/checksum_sha256.txt/.m3u/releaseinfo.json copy, cover art copied from the FLAC folder, plus its own log)"),
                new HelpOption("--source <path>", "A FLAC disc/container folder (from the flac command's --dest)"),
                new HelpOption("--dest <path>", "Parent folder for the release's MP3 container folder (default .)"),
                new HelpOption("--disc <N>", "Disc number (required if the release has more than one disc)"),
            ]),
        ]),
        new HelpSection("Verify", [
            new HelpCommand(["make-checksum [--dest <path>]"], [
                new HelpParagraph("Create checksum_sha256.txt from a folder's current contents (default dest: .)"),
            ]),
            new HelpCommand(["compare-checksum [--dest", "                  <path>]"], [
                new HelpParagraph("Compare a folder's checksum_sha256.txt against its current contents; exits 1 if anything listed is mismatched, missing, or malformed (a manifest entry escaping the target folder) (extra unlisted files are reported but don't affect the exit code) (default dest: .)"),
            ]),
        ]),
        new HelpSection("Info", [
            new HelpCommand(["help / --help / -h"], [new HelpParagraph("Show this help")]),
            new HelpCommand(["--version / -v"], [new HelpParagraph("Show the current version")]),
            new HelpCommand(["<command> --debug"], [
                new HelpParagraph("Print the full stack trace for an unhandled exception instead of a one-line message (same effect as setting WHATINATOR_DEBUG); must come after the command name"),
            ]),
        ]),
    ];
}
