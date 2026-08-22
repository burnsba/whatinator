namespace Whatinator.Core.Drive;

/// <summary>An optical drive discovered on the system.</summary>
/// <param name="DevicePath">The block device path, e.g. <c>/dev/sr1</c>.</param>
/// <param name="Vendor">The drive's reported vendor string (e.g. <c>ASUS</c>), or <see langword="null"/> if unavailable.</param>
/// <param name="Model">The drive's reported model string (e.g. <c>DRW-24F1ST</c>), or <see langword="null"/> if unavailable.</param>
/// <param name="Release">
/// The drive's reported firmware revision (e.g. <c>1.00</c>), or
/// <see langword="null"/> if unavailable -- phase 016, read from the same
/// sysfs device directory as <paramref name="Vendor"/>/<paramref name="Model"/>.
/// The third component of <see cref="Whatinator.Core.WhatinatorConfig.DriveKey"/>.
/// </param>
public sealed record OpticalDrive(string DevicePath, string? Vendor, string? Model, string? Release = null);
