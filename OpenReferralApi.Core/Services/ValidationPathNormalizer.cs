using System;

namespace OpenReferralApi.Core.Services;

internal static class ValidationPathNormalizer
{
    public static string NormalizeArrayIndexes(string? input)
    {
        if (string.IsNullOrEmpty(input))
        {
            return string.Empty;
        }

        int firstBracket = input.IndexOf('[');
        if (firstBracket < 0)
        {
            return input;
        }

        ReadOnlySpan<char> span = input.AsSpan();
        int digitsRemoved = 0;

        // First pass: calculate the exact number of digits that will be removed
        for (int i = firstBracket; i < span.Length; i++)
        {
            if (span[i] == '[')
            {
                int j = i + 1;
                while (j < span.Length && char.IsAsciiDigit(span[j])) j++;

                // If we found at least one digit and ended on a closing bracket
                if (j > i + 1 && j < span.Length && span[j] == ']')
                {
                    digitsRemoved += (j - i - 1);
                    i = j; // Skip to the closing bracket
                }
            }
        }

        if (digitsRemoved == 0)
        {
            return input;
        }

        // Second pass: construct the new string exactly to size without intermediate allocations
        return string.Create(input.Length - digitsRemoved, input, (dest, state) =>
        {
            int srcIdx = 0, destIdx = 0;
            ReadOnlySpan<char> src = state.AsSpan();
            
            while (srcIdx < src.Length)
            {
                dest[destIdx++] = src[srcIdx];

                if (src[srcIdx] == '[')
                {
                    int j = srcIdx + 1;
                    while (j < src.Length && char.IsAsciiDigit(src[j])) j++;
                    
                    if (j > srcIdx + 1 && j < src.Length && src[j] == ']')
                    {
                        dest[destIdx++] = ']';
                        srcIdx = j + 1;
                        continue;
                    }
                }
                srcIdx++;
            }
        });
    }
}
