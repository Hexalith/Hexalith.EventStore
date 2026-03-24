namespace Hexalith.EventStore.Admin.UI.Components;

/// <summary>
/// Squarified treemap layout algorithm (Bruls, Huizing, van Wijk 2000).
/// Pure function: input items + bounding rectangle → output rectangles.
/// </summary>
public static class TreemapLayoutEngine
{
    /// <summary>
    /// Represents a single rectangle in the treemap layout.
    /// </summary>
    /// <param name="Label">The label for this rectangle.</param>
    /// <param name="Value">The original value.</param>
    /// <param name="X">X position.</param>
    /// <param name="Y">Y position.</param>
    /// <param name="Width">Rectangle width.</param>
    /// <param name="Height">Rectangle height.</param>
    public record TreemapRect(string Label, long Value, double X, double Y, double Width, double Height);

    /// <summary>
    /// Computes a squarified treemap layout for the given items within the specified bounding rectangle.
    /// Items with value &lt;= 0 are filtered out. Items are sorted by value descending.
    /// </summary>
    /// <param name="items">The items to lay out, each with a label and numeric value.</param>
    /// <param name="x">Bounding rectangle X.</param>
    /// <param name="y">Bounding rectangle Y.</param>
    /// <param name="width">Bounding rectangle width.</param>
    /// <param name="height">Bounding rectangle height.</param>
    /// <returns>A list of positioned rectangles.</returns>
    public static IReadOnlyList<TreemapRect> ComputeLayout(
        IReadOnlyList<(string Label, long Value)> items,
        double x,
        double y,
        double width,
        double height)
    {
        if (items is null || items.Count == 0 || width <= 0 || height <= 0)
        {
            return [];
        }

        // Filter out non-positive values and sort descending
        List<(string Label, long Value)> sorted = items
            .Where(i => i.Value > 0)
            .OrderByDescending(i => i.Value)
            .ToList();

        if (sorted.Count == 0)
        {
            return [];
        }

        double totalValue = sorted.Sum(i => (double)i.Value);
        double totalArea = width * height;

        List<TreemapRect> result = [];
        Squarify(sorted, 0, x, y, width, height, totalValue, totalArea, result);
        return result;
    }

    private static void Squarify(
        List<(string Label, long Value)> items,
        int startIndex,
        double x,
        double y,
        double width,
        double height,
        double totalValue,
        double totalArea,
        List<TreemapRect> result)
    {
        if (startIndex >= items.Count || width <= 0 || height <= 0 || totalValue <= 0)
        {
            return;
        }

        // Only one item left — fill the remaining space
        if (startIndex == items.Count - 1)
        {
            result.Add(new TreemapRect(items[startIndex].Label, items[startIndex].Value, x, y, width, height));
            return;
        }

        // Determine the shorter side
        bool isHorizontal = width >= height;
        double shortSide = isHorizontal ? height : width;

        // Find the best row of items that minimizes worst aspect ratio
        int bestEnd = startIndex + 1;
        double bestWorstRatio = double.MaxValue;
        double rowValue = 0;

        for (int i = startIndex; i < items.Count; i++)
        {
            rowValue += (double)items[i].Value;
            double rowArea = (rowValue / totalValue) * totalArea;
            double worst = WorstAspectRatio(items, startIndex, i + 1, totalValue, totalArea, shortSide);

            if (worst <= bestWorstRatio)
            {
                bestWorstRatio = worst;
                bestEnd = i + 1;
            }
            else
            {
                // Aspect ratios are getting worse — stop
                break;
            }
        }

        // Layout the row
        double rowTotal = 0;
        for (int i = startIndex; i < bestEnd; i++)
        {
            rowTotal += (double)items[i].Value;
        }

        double rowAreaPixels = (rowTotal / totalValue) * totalArea;
        double rowLength = rowAreaPixels / shortSide;

        if (double.IsNaN(rowLength) || double.IsInfinity(rowLength))
        {
            rowLength = 0;
        }

        double offset = 0;
        for (int i = startIndex; i < bestEnd; i++)
        {
            double itemFraction = (double)items[i].Value / rowTotal;
            double itemLength = itemFraction * shortSide;

            if (double.IsNaN(itemLength) || double.IsInfinity(itemLength))
            {
                itemLength = 0;
            }

            if (isHorizontal)
            {
                result.Add(new TreemapRect(items[i].Label, items[i].Value, x, y + offset, rowLength, itemLength));
            }
            else
            {
                result.Add(new TreemapRect(items[i].Label, items[i].Value, x + offset, y, itemLength, rowLength));
            }

            offset += itemLength;
        }

        // Recurse with remaining space
        double remainingValue = totalValue - rowTotal;
        if (isHorizontal)
        {
            double remainingWidth = width - rowLength;
            double remainingArea = remainingWidth * height;
            Squarify(items, bestEnd, x + rowLength, y, remainingWidth, height, remainingValue, remainingArea, result);
        }
        else
        {
            double remainingHeight = height - rowLength;
            double remainingArea = width * remainingHeight;
            Squarify(items, bestEnd, x, y + rowLength, width, remainingHeight, remainingValue, remainingArea, result);
        }
    }

    private static double WorstAspectRatio(
        List<(string Label, long Value)> items,
        int start,
        int end,
        double totalValue,
        double totalArea,
        double shortSide)
    {
        double rowTotal = 0;
        for (int i = start; i < end; i++)
        {
            rowTotal += (double)items[i].Value;
        }

        double rowArea = (rowTotal / totalValue) * totalArea;
        double rowLength = rowArea / shortSide;

        if (rowLength <= 0 || double.IsNaN(rowLength) || double.IsInfinity(rowLength))
        {
            return double.MaxValue;
        }

        double worst = 0;
        for (int i = start; i < end; i++)
        {
            double itemFraction = (double)items[i].Value / rowTotal;
            double itemLength = itemFraction * shortSide;

            if (itemLength <= 0)
            {
                continue;
            }

            double ratio = Math.Max(rowLength / itemLength, itemLength / rowLength);
            if (ratio > worst)
            {
                worst = ratio;
            }
        }

        return worst;
    }
}
