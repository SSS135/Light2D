namespace com.SSS135.EditorAutomation.Recorder
{
    /// <summary>
    /// Variant discriminator for a recorded value. Stored as a byte to keep
    /// per-change-point memory small in long sessions.
    /// </summary>
    public enum ValueKind : byte
    {
        Float,
        Vec2,
        Vec3,
        Vec4,
        Quat,
        Int,
        Bool,
        Color,
        Enum,
        String
    }
}
