namespace Cascade.UI;

/// <summary>
/// Allows a type to provide custom serialization for <see cref="LocalStorage"/>.
/// Implement as a static abstract interface on the stored type.
/// </summary>
/// <typeparam name="T">The type being serialized.</typeparam>
public interface IStorageSerializable<T>
{
    /// <summary>
    /// Serializes a value to a byte array for storage.
    /// </summary>
    static abstract byte[] Serialize(T value);

    /// <summary>
    /// Deserializes a byte array back to the stored type.
    /// </summary>
    static abstract T Deserialize(byte[] data);
}
