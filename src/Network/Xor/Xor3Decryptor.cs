// <copyright file="Xor3Decryptor.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Network.Xor;

/// <summary>
/// A decryptor which XOR-decrypts data using a 3-byte key.
/// </summary>
public class Xor3Decryptor : Xor3Encryptor, ISpanDecryptor
{
    /// <summary>
    /// Initializes a new instance of the <see cref="Xor3Decryptor"/> class.
    /// </summary>
    /// <param name="startOffset">The start offset.</param>
    public Xor3Decryptor(int startOffset)
        : base(startOffset)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Xor3Decryptor"/> class.
    /// </summary>
    /// <param name="startOffset">The start offset.</param>
    /// <param name="xor3Keys">The 3-byte XOR key.</param>
    public Xor3Decryptor(int startOffset, byte[] xor3Keys)
        : base(startOffset, xor3Keys)
    {
    }

    /// <inheritdoc/>
    public void Decrypt(Span<byte> packet)
    {
        this.Encrypt(packet);
    }
}