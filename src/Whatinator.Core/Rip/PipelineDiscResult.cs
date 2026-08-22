using Whatinator.Core.Flac;
using Whatinator.Core.Mp3;

namespace Whatinator.Core.Rip;

/// <summary>The outcome of one disc's <see cref="PipelineRunner.RunDiscAsync"/> call.</summary>
/// <param name="RawRipDirectory">
/// Where this disc was ripped to. Still on disk (and still holding the
/// ripped FLAC files) only when <see cref="PipelineDiscOptions.SkipFlacPackaging"/>
/// was <see langword="true"/> -- otherwise <see cref="FlacPackager"/> already
/// moved everything useful out of it and <see cref="PipelineRunner"/> deleted
/// what remained.
/// </param>
/// <param name="RipResult">This disc's rip outcome.</param>
/// <param name="FlacResult"><see langword="null"/> if <see cref="PipelineDiscOptions.SkipFlacPackaging"/> was <see langword="true"/>.</param>
/// <param name="Mp3Result"><see langword="null"/> if <see cref="PipelineDiscOptions.CreateMp3"/> was <see langword="false"/>.</param>
public sealed record PipelineDiscResult(
    string RawRipDirectory,
    WhatinatorRipResult RipResult,
    FlacPackageResult? FlacResult,
    Mp3PackageResult? Mp3Result);
