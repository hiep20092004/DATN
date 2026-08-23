namespace WaterFlow.Core
{
    /// <summary>
    /// Optional companion interface to <see cref="ISaveObject"/>. When the save
    /// system fails to locate a container by stable key / hash / legacy hash
    /// (e.g. the user upgraded Unity and <c>string.GetHashCode()</c> changed),
    /// it falls back to scanning every unmatched container’s JSON payload and
    /// asks each candidate type whether the JSON looks like its data.
    ///
    /// Implementations should be conservative: only return true when the JSON
    /// clearly matches the schema of this save object (e.g. several distinctive
    /// field names are present). False positives can permanently mis-assign
    /// another save object’s data to this one.
    /// </summary>
    public interface IRecoverableSaveObject
    {
        bool LooksLikeMyData(string json);
    }
}
