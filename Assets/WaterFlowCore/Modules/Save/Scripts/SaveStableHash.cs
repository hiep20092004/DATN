namespace WaterFlow.Core
{
    /// <summary>
    /// Deterministic 32-bit FNV-1a hash for save-system identifiers.
    /// IMPORTANT: Unlike <c>string.GetHashCode()</c> – which Microsoft explicitly
    /// documents as unstable across runtime versions, scripting backends (Mono vs
    /// IL2CPP), 32-bit vs 64-bit and even individual app launches in some
    /// configurations – this hash is byte-for-byte identical on every platform
    /// and Unity version. All new <see cref="SavedDataContainer"/> hashes are
    /// produced through this function so that updating Unity / switching the
    /// scripting backend never invalidates a user’s save.
    /// </summary>
    public static class SaveStableHash
    {
        // Standard FNV-1a 32-bit constants.
        private const uint FNV_OFFSET_BASIS = 2166136261u;
        private const uint FNV_PRIME = 16777619u;

        public static int Compute(string value)
        {
            if (string.IsNullOrEmpty(value)) return 0;

            uint hash = FNV_OFFSET_BASIS;
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                // Hash the low byte then the high byte so the result is
                // identical on little-endian and big-endian platforms.
                hash ^= (byte)(c & 0xFF);
                hash *= FNV_PRIME;
                hash ^= (byte)((c >> 8) & 0xFF);
                hash *= FNV_PRIME;
            }

            return unchecked((int)hash);
        }
    }
}
