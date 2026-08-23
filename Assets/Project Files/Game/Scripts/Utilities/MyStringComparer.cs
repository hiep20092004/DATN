using System;

public static class MyStringComparer
{
    public static bool Compare(string s1, string s2, MyStringComparison comparison)
    {
        switch (comparison)
        {
            case MyStringComparison.Ordinal:
                return s1.Equals(s2, StringComparison.Ordinal);
            case MyStringComparison.OrdinalIgnoreCase:
                return s1.Equals(s2, StringComparison.OrdinalIgnoreCase);
            case MyStringComparison.Contain:
                return s1.Contains(s2, StringComparison.Ordinal);
            case MyStringComparison.ContainIgnoreCase:
                return s1.Contains(s2, StringComparison.OrdinalIgnoreCase);
            default:
                return s1.Equals(s2, StringComparison.Ordinal);
        }
    }
}
