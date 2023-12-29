using System.Numerics;
using System.Collections.Generic;
using Dalamud.Utility;

namespace TickTracker.Structs;

// What do you mean passing 8 arguments in 3 different functions twice is unhinged and should be a struct instead.
public struct TableStruct
{
    /// <summary>
    ///     The ID of the table that's used by ImGui.
    /// </summary>
    public string Id { get; set; }
    /// <summary>
    ///     The header text of the first column.
    /// </summary>
    public string Column1Header { get; set; }
    /// <summary>
    ///     The header text of the second column.
    /// </summary>
    public string Column2Header { get; set; }
    /// <summary>
    ///     The width of the first column, set according to the biggest member.
    /// </summary>
    public float Column1Width { get; set; }
    /// <summary>
    ///     The width of the second column, set according to the biggest member.
    /// </summary>
    public float Column2Width { get; set; }
    /// <summary>
    ///     The <see cref="IReadOnlyList{T}"/> that's parsed to draw the first column.
    /// </summary>
    public IReadOnlyList<string> Column1Content { get; set; }
    /// <summary>
    ///     The <see cref="IReadOnlyList{T}"/> that's parsed to draw the second column.
    /// </summary>
    public IReadOnlyList<string> Column2Content { get; set; }
    /// <summary>
    ///     The size of the overall table, this is used for the table's border to allow scrollable columns with a fixed header.
    /// </summary>
    public Vector2? Size { get; set; }

    /// <summary>
    ///     Check if every property is properly populated before the <see langword="struct"/> is accessed to create the table.
    /// </summary>
    public readonly bool IsValid()
    {
        return !Id.IsNullOrWhitespace()
            && !Column1Header.IsNullOrWhitespace()
            && !Column2Header.IsNullOrWhitespace()
            && Column1Width != 0
            && Column2Width != 0
            && Column1Content != null
            && Column2Content != null
            && Size.HasValue;
    }

    /// <summary>
    ///     Assign the <paramref name="newSize"/> to the struct's Size if it's not populated, or is different than <paramref name="newSize"/>.
    /// </summary>
    public void ResizeIfNeeded(Vector2 newSize)
    {
        if (!Size.HasValue || Size.Value != newSize)
        {
            Size = newSize;
        }
    }
}
