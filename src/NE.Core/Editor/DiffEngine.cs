namespace NEManager.Core.Editor;

/// <summary>
/// 文本/二进制差异比较引擎。
/// </summary>
public class DiffEngine
{
    public DiffResult DiffText(string textA, string textB)
    {
        var linesA = textA.Split('\n');
        var linesB = textB.Split('\n');
        return DiffLines(linesA, linesB);
    }

    public DiffResult DiffFiles(string pathA, string pathB)
    {
        var linesA = File.ReadAllLines(pathA);
        var linesB = File.ReadAllLines(pathB);
        return DiffLines(linesA, linesB);
    }

    public List<(long offset, byte oldByte, byte newByte)> DiffBinary(byte[] a, byte[] b)
    {
        var diffs = new List<(long offset, byte oldByte, byte newByte)>();
        long maxLen = Math.Max(a.Length, b.Length);
        for (long i = 0; i < maxLen; i++)
        {
            byte ba = i < a.Length ? a[i] : (byte)0;
            byte bb = i < b.Length ? b[i] : (byte)0;
            if (ba != bb)
                diffs.Add((i, ba, bb));
        }
        return diffs;
    }

    private static DiffResult DiffLines(string[] linesA, string[] linesB)
    {
        int m = linesA.Length;
        int n = linesB.Length;
        var dp = new int[m + 1, n + 1];

        for (int i = 1; i <= m; i++)
        {
            for (int j = 1; j <= n; j++)
            {
                if (linesA[i - 1] == linesB[j - 1])
                    dp[i, j] = dp[i - 1, j - 1] + 1;
                else
                    dp[i, j] = Math.Max(dp[i - 1, j], dp[i, j - 1]);
            }
        }

        var result = new DiffResult();
        int ii = m, jj = n;
        var stack = new Stack<DiffLine>();

        while (ii > 0 || jj > 0)
        {
            if (ii > 0 && jj > 0 && linesA[ii - 1] == linesB[jj - 1])
            {
                stack.Push(new DiffLine { LineNumberA = ii, LineNumberB = jj, Text = linesA[ii - 1], DiffType = DiffType.Same });
                ii--;
                jj--;
            }
            else if (jj > 0 && (ii == 0 || dp[ii, jj - 1] >= dp[ii - 1, jj]))
            {
                stack.Push(new DiffLine { LineNumberA = -1, LineNumberB = jj, Text = linesB[jj - 1], DiffType = DiffType.Added });
                jj--;
            }
            else if (ii > 0)
            {
                stack.Push(new DiffLine { LineNumberA = ii, LineNumberB = -1, Text = linesA[ii - 1], DiffType = DiffType.Removed });
                ii--;
            }
        }

        var tempLines = stack.ToList();
        tempLines.Reverse();

        for (int k = 0; k < tempLines.Count; k++)
        {
            if (tempLines[k].DiffType == DiffType.Removed)
            {
                int addedStart = k + 1;
                int removedCount = 1;
                while (addedStart < tempLines.Count && tempLines[addedStart].DiffType == DiffType.Removed)
                {
                    removedCount++;
                    addedStart++;
                }

                int addedCount = 0;
                int addedEnd = addedStart;
                while (addedEnd < tempLines.Count && tempLines[addedEnd].DiffType == DiffType.Added)
                {
                    addedCount++;
                    addedEnd++;
                }

                int minCount = Math.Min(removedCount, addedCount);
                for (int p = 0; p < minCount; p++)
                {
                    tempLines[k + p].DiffType = DiffType.Modified;
                    tempLines[addedStart + p].DiffType = DiffType.Modified;
                }
            }
        }

        result.Lines = tempLines;
        return result;
    }
}

public class DiffResult
{
    public List<DiffLine> Lines { get; set; } = new();
}

public class DiffLine
{
    public int LineNumberA { get; set; }
    public int LineNumberB { get; set; }
    public string Text { get; set; } = string.Empty;
    public DiffType DiffType { get; set; }
}

public enum DiffType
{
    Same,
    Added,
    Removed,
    Modified
}
