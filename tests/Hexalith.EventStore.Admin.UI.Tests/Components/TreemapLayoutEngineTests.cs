using Hexalith.EventStore.Admin.UI.Components;

namespace Hexalith.EventStore.Admin.UI.Tests.Components;

/// <summary>
/// Unit tests for the squarified treemap layout algorithm.
/// </summary>
public class TreemapLayoutEngineTests
{
    [Fact]
    public void ComputeLayout_ReturnsCorrectNumberOfRectangles()
    {
        // Arrange
        List<(string Label, long Value)> items =
        [
            ("A", 60),
            ("B", 30),
            ("C", 10),
        ];

        // Act
        IReadOnlyList<TreemapLayoutEngine.TreemapRect> result = TreemapLayoutEngine.ComputeLayout(items, 0, 0, 1600, 900);

        // Assert
        result.Count.ShouldBe(3);
    }

    [Fact]
    public void ComputeLayout_RectangleAreasAreProportionalToValues()
    {
        // Arrange
        List<(string Label, long Value)> items =
        [
            ("A", 60),
            ("B", 30),
            ("C", 10),
        ];

        // Act
        IReadOnlyList<TreemapLayoutEngine.TreemapRect> result = TreemapLayoutEngine.ComputeLayout(items, 0, 0, 1600, 900);

        // Assert — area proportions should roughly match value proportions
        double totalArea = result.Sum(r => r.Width * r.Height);
        double areaA = result.First(r => r.Label == "A").Width * result.First(r => r.Label == "A").Height;
        double areaB = result.First(r => r.Label == "B").Width * result.First(r => r.Label == "B").Height;
        double areaC = result.First(r => r.Label == "C").Width * result.First(r => r.Label == "C").Height;

        (areaA / totalArea).ShouldBe(0.6, 0.05);
        (areaB / totalArea).ShouldBe(0.3, 0.05);
        (areaC / totalArea).ShouldBe(0.1, 0.05);
    }

    [Fact]
    public void ComputeLayout_AreaConservation_SumEqualsContainerArea()
    {
        // Arrange
        List<(string Label, long Value)> items =
        [
            ("A", 50),
            ("B", 30),
            ("C", 15),
            ("D", 5),
        ];

        // Act
        IReadOnlyList<TreemapLayoutEngine.TreemapRect> result = TreemapLayoutEngine.ComputeLayout(items, 0, 0, 1600, 900);

        // Assert
        double totalArea = result.Sum(r => r.Width * r.Height);
        double containerArea = 1600.0 * 900.0;
        totalArea.ShouldBe(containerArea, 1.0); // tolerance of 1 pixel^2
    }

    [Fact]
    public void ComputeLayout_NoOverlappingRectangles()
    {
        // Arrange
        List<(string Label, long Value)> items =
        [
            ("A", 40),
            ("B", 30),
            ("C", 20),
            ("D", 10),
        ];

        // Act
        IReadOnlyList<TreemapLayoutEngine.TreemapRect> result = TreemapLayoutEngine.ComputeLayout(items, 0, 0, 1600, 900);

        // Assert — no two rectangles overlap
        for (int i = 0; i < result.Count; i++)
        {
            for (int j = i + 1; j < result.Count; j++)
            {
                TreemapLayoutEngine.TreemapRect a = result[i];
                TreemapLayoutEngine.TreemapRect b = result[j];

                // Two rectangles overlap if none of the non-overlap conditions is true
                bool noOverlap = a.X + a.Width <= b.X + 0.01
                    || b.X + b.Width <= a.X + 0.01
                    || a.Y + a.Height <= b.Y + 0.01
                    || b.Y + b.Height <= a.Y + 0.01;
                noOverlap.ShouldBeTrue($"Rectangles '{a.Label}' and '{b.Label}' overlap");
            }
        }
    }

    [Fact]
    public void ComputeLayout_HandlesZeroValueItems()
    {
        // Arrange — zero-value items should be filtered out
        List<(string Label, long Value)> items =
        [
            ("A", 50),
            ("Zero", 0),
            ("B", 50),
            ("Negative", -10),
        ];

        // Act
        IReadOnlyList<TreemapLayoutEngine.TreemapRect> result = TreemapLayoutEngine.ComputeLayout(items, 0, 0, 1600, 900);

        // Assert — only positive items
        result.Count.ShouldBe(2);
        result.ShouldAllBe(r => !double.IsNaN(r.Width) && !double.IsNaN(r.Height));
        result.ShouldAllBe(r => !double.IsInfinity(r.Width) && !double.IsInfinity(r.Height));
    }

    [Fact]
    public void ComputeLayout_EmptyInput_ReturnsEmptyList()
    {
        // Act
        IReadOnlyList<TreemapLayoutEngine.TreemapRect> result = TreemapLayoutEngine.ComputeLayout([], 0, 0, 1600, 900);

        // Assert
        result.ShouldBeEmpty();
    }

    [Fact]
    public void ComputeLayout_SingleItem_FillsEntireSpace()
    {
        // Arrange
        List<(string Label, long Value)> items = [("Only", 100)];

        // Act
        IReadOnlyList<TreemapLayoutEngine.TreemapRect> result = TreemapLayoutEngine.ComputeLayout(items, 0, 0, 1600, 900);

        // Assert
        result.Count.ShouldBe(1);
        result[0].X.ShouldBe(0);
        result[0].Y.ShouldBe(0);
        result[0].Width.ShouldBe(1600);
        result[0].Height.ShouldBe(900);
    }

    [Fact]
    public void ComputeLayout_AllRectanglesWithinBounds()
    {
        // Arrange
        List<(string Label, long Value)> items =
        [
            ("A", 100),
            ("B", 80),
            ("C", 60),
            ("D", 40),
            ("E", 20),
        ];

        // Act
        IReadOnlyList<TreemapLayoutEngine.TreemapRect> result = TreemapLayoutEngine.ComputeLayout(items, 10, 20, 800, 600);

        // Assert — all rectangles should be within bounds
        foreach (TreemapLayoutEngine.TreemapRect rect in result)
        {
            rect.X.ShouldBeGreaterThanOrEqualTo(10 - 0.01);
            rect.Y.ShouldBeGreaterThanOrEqualTo(20 - 0.01);
            (rect.X + rect.Width).ShouldBeLessThanOrEqualTo(810 + 0.01);
            (rect.Y + rect.Height).ShouldBeLessThanOrEqualTo(620 + 0.01);
        }
    }

    [Fact]
    public void ComputeLayout_NullInput_ReturnsEmptyList()
    {
        // Act
        IReadOnlyList<TreemapLayoutEngine.TreemapRect> result = TreemapLayoutEngine.ComputeLayout(null!, 0, 0, 1600, 900);

        // Assert
        result.ShouldBeEmpty();
    }

    [Fact]
    public void ComputeLayout_ZeroDimensions_ReturnsEmptyList()
    {
        // Arrange
        List<(string Label, long Value)> items = [("A", 100)];

        // Act
        IReadOnlyList<TreemapLayoutEngine.TreemapRect> resultW = TreemapLayoutEngine.ComputeLayout(items, 0, 0, 0, 900);
        IReadOnlyList<TreemapLayoutEngine.TreemapRect> resultH = TreemapLayoutEngine.ComputeLayout(items, 0, 0, 1600, 0);

        // Assert
        resultW.ShouldBeEmpty();
        resultH.ShouldBeEmpty();
    }
}
