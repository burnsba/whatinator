using System.Runtime.InteropServices;

namespace Whatinator.LibDiscId;

/// <summary>
/// Owns a native <c>DiscId*</c> allocated by <c>discid_new</c>, guaranteeing
/// it is released via <c>discid_free</c> exactly once, even if an exception
/// is thrown between allocation and use.
/// </summary>
internal sealed partial class DiscIdSafeHandle : SafeHandle
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DiscIdSafeHandle"/> class.
    /// Populated by the P/Invoke marshaller when returned from <c>discid_new</c>.
    /// </summary>
    public DiscIdSafeHandle()
        : base(IntPtr.Zero, ownsHandle: true)
    {
    }

    /// <inheritdoc />
    public override bool IsInvalid => handle == IntPtr.Zero;

    /// <inheritdoc />
    protected override bool ReleaseHandle()
    {
        NativeMethods.discid_free(handle);
        return true;
    }
}
