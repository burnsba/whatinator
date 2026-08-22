namespace Whatinator.Core.Flac;

/// <summary>The outcome of a successful <see cref="FlacPackager.PackageAsync"/> call.</summary>
/// <param name="ContainerDirectory">The release's container folder (holds <c>cd1/</c>/<c>cd2/</c> for multi-disc releases, or the FLAC files directly for single-disc).</param>
/// <param name="DiscDirectory">The directory this disc's FLAC files and log were moved into (same as <paramref name="ContainerDirectory"/> for single-disc releases).</param>
/// <param name="MovedFlacFileCount">How many <c>.flac</c> files were moved from the source directory.</param>
/// <param name="LogFilePath">
/// Where the rip's <c>.log</c> file was moved to, or <see langword="null"/>
/// if the source directory had none -- <see cref="Whatinator.Core.Rip.WhatinatorRipRunner"/>
/// doesn't produce one yet (phase 016 adds the EAC-style rip log).
/// </param>
/// <param name="CoverArtPath">Where cover art was saved this run, or <see langword="null"/> if none was found/fetched (or one already existed).</param>
public sealed record FlacPackageResult(
    string ContainerDirectory,
    string DiscDirectory,
    int MovedFlacFileCount,
    string? LogFilePath,
    string? CoverArtPath);
