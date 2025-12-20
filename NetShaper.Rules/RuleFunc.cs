using NetShaper.Abstractions;

namespace NetShaper.Rules
{
    /// <summary>
    /// Rule function delegate. No closures allowed.
    /// </summary>
    /// <remarks>
    /// <para><b>IMPORTANT:</b> Do NOT modify <c>result.Mask</c> directly.</para>
    /// <para>Return the action mask; the pipeline will OR it into <c>result.Mask</c>.</para>
    /// <para>Use <c>result.Accumulate()</c> for numeric fields (DelayTicks, DuplicateCount).</para>
    /// </remarks>
    /// <param name="packet">Raw packet data.</param>
    /// <param name="meta">Packet metadata.</param>
    /// <param name="state">Per-rule preallocated state.</param>
    /// <param name="result">Accumulated result - use Accumulate(), never set Mask.</param>
    /// <returns>Action mask for this rule (will be OR'd by pipeline).</returns>
    public delegate ActionMask RuleFunc(
        ReadOnlySpan<byte> packet,
        ref PacketMetadata meta,
        ref RuleState state,
        ref ActionResult result);
}
