using System.Text;

namespace MonoFlixel;

/// <summary>
/// Generates caves with a cellular automaton (port of flixel-addons' FlxCaveGenerator, originally by Eddie Lee).
/// Cells are 1 = wall, 0 = empty. The result can be loaded straight into an FlxTilemap:
/// <code>
/// var cave = FlxCaveGenerator.GenerateCaveString(80, 60, smoothingIterations: 6, wallRatio: 0.5f);
/// tilemap.LoadMapFromCsv(cave, "cave/caveWallTiles.png", 8, 8, FlxAutoTiling.Auto);
/// </code>
/// Uses FlxG.Random, so seeding it makes caves repeatable.
/// </summary>
public static class FlxCaveGenerator
{
    /// <summary>Generates a cave as a [row, column] matrix.</summary>
    /// <param name="smoothingIterations">How many times to smooth the cave: higher is smoother (and slower).</param>
    /// <param name="wallRatio">Chance for a starting cell to be a wall: nearer 1.0 means more walls.</param>
    public static int[,] GenerateCaveMatrix(int columns, int rows, int smoothingIterations = 6, float wallRatio = 0.5f)
    {
        var matrix = new int[rows, columns];
        var matrix2 = new int[rows, columns];

        for (int y = 0; y < rows; y++)
            for (int x = 0; x < columns; x++)
                matrix[y, x] = FlxG.Random.Float() < wallRatio ? 1 : 0;

        for (int i = 0; i < smoothingIterations; i++)
        {
            RunCellularAutomata(matrix, matrix2);
            (matrix, matrix2) = (matrix2, matrix);
        }
        return matrix;
    }

    /// <summary>Generates a cave as a flat row-major array, ready for FlxTilemap.LoadMapFromArray.</summary>
    public static int[] GenerateCaveArray(int columns, int rows, int smoothingIterations = 6, float wallRatio = 0.5f)
    {
        var m = GenerateCaveMatrix(columns, rows, smoothingIterations, wallRatio);
        var data = new int[columns * rows];
        for (int y = 0; y < rows; y++)
            for (int x = 0; x < columns; x++)
                data[y * columns + x] = m[y, x];
        return data;
    }

    /// <summary>Generates a cave as CSV text for FlxTilemap.LoadMapFromCsv.</summary>
    public static string GenerateCaveString(int columns, int rows, int smoothingIterations = 6, float wallRatio = 0.5f) =>
        ConvertMatrixToString(GenerateCaveMatrix(columns, rows, smoothingIterations, wallRatio));

    /// <summary>Converts a matrix from GenerateCaveMatrix to CSV text (one row per line).</summary>
    public static string ConvertMatrixToString(int[,] matrix)
    {
        var sb = new StringBuilder();
        for (int y = 0; y < matrix.GetLength(0); y++)
        {
            for (int x = 0; x < matrix.GetLength(1); x++)
                sb.Append(matrix[y, x]).Append(',');
            sb.Append('\n');
        }
        return sb.ToString();
    }

    /// <summary>Number of wall cells in the (2*distance+1)^2 block around a cell, the cell itself included. Cells outside the matrix don't count.</summary>
    static int CountWallNeighbors(int[,] matrix, int posX, int posY, int distance = 1)
    {
        int rows = matrix.GetLength(0), columns = matrix.GetLength(1);
        int count = 0;
        for (int y = -distance; y <= distance; y++)
            for (int x = -distance; x <= distance; x++)
            {
                int cx = posX + x, cy = posY + y;
                if (cx < 0 || cx >= columns || cy < 0 || cy >= rows) continue;
                if (matrix[cy, cx] != 0) count++;
            }
        return count;
    }

    /// <summary>The "4-5 rule": a cell becomes a wall if 5 or more cells around it (itself included) are walls.</summary>
    static void RunCellularAutomata(int[,] input, int[,] output)
    {
        int rows = input.GetLength(0), columns = input.GetLength(1);
        for (int y = 0; y < rows; y++)
            for (int x = 0; x < columns; x++)
                output[y, x] = CountWallNeighbors(input, x, y) >= 5 ? 1 : 0;
    }
}
