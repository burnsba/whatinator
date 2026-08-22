namespace Whatinator.Core.Metadata;

/// <summary>The outcome of a successful <see cref="MetadataUpdater.Apply"/> call.</summary>
/// <param name="FinalDirectory">The release folder's path after the update -- differs from the folder passed in only when <paramref name="FolderRenamed"/> is <see langword="true"/>.</param>
/// <param name="BackupPath">Where the folder's previous <c>releaseinfo.json</c> was saved before being overwritten.</param>
/// <param name="FolderRenamed">Whether the container folder was renamed (its computed name no longer matched, e.g. after a year or title correction).</param>
/// <param name="ChecksumFilePath">Where the recalculated <c>checksum_sha256.txt</c> was written.</param>
/// <param name="ChecksumFileCount">How many audio files were hashed into <paramref name="ChecksumFilePath"/>.</param>
public sealed record MetadataUpdateResult(
    string FinalDirectory,
    string BackupPath,
    bool FolderRenamed,
    string ChecksumFilePath,
    int ChecksumFileCount);
