using System;
using System.Buffers;
using System.Collections.Generic;
using System.Text;

namespace MauiMixTube.Helpers
{
    public static class FuzzySearch
    {
        private static int ToLowerAndFilterSpaces(ReadOnlySpan<char> source, Span<char> destination)
        {
            source.ToLowerInvariant(destination);

            int writeIndex = 0;
            for (int i = 0; i < source.Length; i++)
            {
                char c = destination[i];

                if (char.IsWhiteSpace(c))
                    continue;

                destination[writeIndex++] = c;
            }

            return writeIndex; 
        }


        public static double HybridScoreSimilarity(ReadOnlySpan<char> input, ReadOnlySpan<char> target)
        {
            const int StackThreshold = 256;

            char[]? inputRent = null;
            char[]? targetRent = null;

            Span<char> inputLower = input.Length <= StackThreshold
                    ? stackalloc char[input.Length]
                    : (inputRent = ArrayPool<char>.Shared.Rent(input.Length));

            Span<char> targetLower = target.Length <= StackThreshold
                    ? stackalloc char[target.Length]
                    : (targetRent = ArrayPool<char>.Shared.Rent(target.Length));

            try
            {
                int inputLowerLen = ToLowerAndFilterSpaces(input, inputLower);
                int targetLowerLen = ToLowerAndFilterSpaces(target, targetLower);

                if (inputLower.Length > inputLowerLen)
                    inputLower = inputLower[..inputLowerLen];
                if (targetLower.Length > targetLowerLen)
                    targetLower = targetLower[..targetLowerLen];

                double confidence = 0d;

                if (targetLower.StartsWith(inputLower, StringComparison.Ordinal))
                    confidence += 1.0d + (double)inputLower.Length / targetLower.Length;

                int index = targetLower.IndexOf(inputLower, StringComparison.Ordinal);
                if (index != -1)
                    confidence += Math.Max(0d, 0.8d - (index * 0.01d));

                return 0.5d * confidence + LevenshteinSimilarity(inputLower, targetLower);
            }
            finally
            {
                if (inputRent != null) ArrayPool<char>.Shared.Return(inputRent);
                if (targetRent != null) ArrayPool<char>.Shared.Return(targetRent);
            }
        }

        public static double LevenshteinSimilarity(ReadOnlySpan<char> s1, ReadOnlySpan<char> s2)
        {
            const int StackThreshold = 512;
            int len1 = s1.Length;
            int len2 = s2.Length;

            if (len1 == 0 && len2 == 0) return 1.0;
            if (len1 == 0 || len2 == 0) return 0.0;

            int rowLen = len2 + 1;

            int[]? prevRent = null;
            int[]? currRent = null;

            Span<int> prevRow = rowLen <= StackThreshold
                ? stackalloc int[rowLen]
                : (prevRent = ArrayPool<int>.Shared.Rent(rowLen));

            Span<int> currRow = rowLen <= StackThreshold
                ? stackalloc int[rowLen]
                : (currRent = ArrayPool<int>.Shared.Rent(rowLen));

            try
            {
                if (prevRow.Length > rowLen)
                    prevRow = prevRow[..rowLen];
                if (currRow.Length > rowLen)
                    currRow = currRow[..rowLen];

                for (int j = 0; j <= len2; j++) prevRow[j] = j;

                for (int i = 1; i <= len1; i++)
                {
                    currRow[0] = i;
                    for (int j = 1; j <= len2; j++)
                    {
                        int cost = (s1[i - 1] == s2[j - 1]) ? 0 : 1;
                        int insert = currRow[j - 1] + 1;
                        int delete = prevRow[j] + 1;
                        int replace = prevRow[j - 1] + cost;
                        currRow[j] = Math.Min(insert, Math.Min(delete, replace));
                    }

                    var temp = prevRow;
                    prevRow = currRow;
                    currRow = temp;
                }

                int distance = prevRow[len2];
                int maxLen = Math.Max(len1, len2);
                return 1.0 - (double)distance / maxLen;
            }
            finally
            {
                if (prevRent != null) ArrayPool<int>.Shared.Return(prevRent);
                if (currRent != null) ArrayPool<int>.Shared.Return(currRent);
            }
        }
    }
}
