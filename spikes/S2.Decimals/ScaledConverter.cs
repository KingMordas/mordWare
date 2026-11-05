using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace S2.Decimals;

/// <summary>
/// One converter for every scaled quantity (design D3): the scale comes from the type, never from the mapping.
/// MordWare.Persistence in production.
/// </summary>
public sealed class ScaledConverter<T>() : ValueConverter<T, long>(v => v.Scaled, v => Factory.From(v))
    where T : struct, IScaled<T>
{
    /// <summary>
    /// An expression tree cannot call a static abstract interface member (CS8927), so the converter calls this
    /// ordinary static method, which can.
    /// </summary>
    public static class Factory
    {
        public static T From(long scaled) => T.FromScaled(scaled);
    }
}
