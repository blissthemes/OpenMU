// <copyright file="PromuNetworkKeys.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Network.Security;

using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using MUnique.OpenMU.Network.SimpleModulus;
using MUnique.OpenMU.Network.Xor;

/// <summary>
/// Loads and exposes the active Xor3 / Xor32 / SimpleModulus key set for the process.
/// </summary>
/// <remarks>
/// <para>
/// Environment selection: <c>PROMU_CRYPTO_ENV</c> = <c>local</c> | <c>test</c> | <c>prod</c> (default: local).
/// </para>
/// <para>
/// For test/prod, keys are loaded from <c>PROMU_CRYPTO_KEYS_PATH</c> (JSON file) or from individual
/// environment variables. Local always uses the public stock defaults.
/// </para>
/// </remarks>
public static class PromuNetworkKeys
{
    private static readonly object Sync = new();
    private static bool _initialized;
    private static NetworkKeyEnvironment _environment = NetworkKeyEnvironment.Local;
    private static byte[] _xor3Keys = DefaultKeys.Xor3Keys;
    private static byte[] _xor32Key = DefaultKeys.Xor32Key;
    private static SimpleModulusKeys _serverEncryptKey = PipelinedSimpleModulusEncryptor.DefaultServerKey;
    private static SimpleModulusKeys _serverDecryptKey = PipelinedSimpleModulusDecryptor.DefaultServerKey;
    private static SimpleModulusKeys _clientEncryptKey = PipelinedSimpleModulusEncryptor.DefaultClientKey;
    private static SimpleModulusKeys _clientDecryptKey = PipelinedSimpleModulusDecryptor.DefaultClientKey;

    /// <summary>
    /// Gets the active deployment environment.
    /// </summary>
    public static NetworkKeyEnvironment Environment
    {
        get
        {
            EnsureInitialized();
            return _environment;
        }
    }

    /// <summary>
    /// Gets a value indicating whether stock (public) keys are in use.
    /// </summary>
    public static bool UsesStockKeys
    {
        get
        {
            EnsureInitialized();
            return _environment == NetworkKeyEnvironment.Local;
        }
    }

    /// <summary>
    /// Gets the active 3-byte XOR key (login credentials).
    /// </summary>
    public static byte[] Xor3Keys
    {
        get
        {
            EnsureInitialized();
            return _xor3Keys;
        }
    }

    /// <summary>
    /// Gets the active 32-byte XOR key (client→server stream).
    /// </summary>
    public static byte[] Xor32Key
    {
        get
        {
            EnsureInitialized();
            return _xor32Key;
        }
    }

    /// <summary>
    /// Gets the server→client SimpleModulus encryption keys.
    /// </summary>
    public static SimpleModulusKeys ServerEncryptKey
    {
        get
        {
            EnsureInitialized();
            return _serverEncryptKey;
        }
    }

    /// <summary>
    /// Gets the server-side SimpleModulus decryption keys (client→server).
    /// </summary>
    public static SimpleModulusKeys ServerDecryptKey
    {
        get
        {
            EnsureInitialized();
            return _serverDecryptKey;
        }
    }

    /// <summary>
    /// Gets the client→server SimpleModulus encryption keys (used by the game client).
    /// </summary>
    public static SimpleModulusKeys ClientEncryptKey
    {
        get
        {
            EnsureInitialized();
            return _clientEncryptKey;
        }
    }

    /// <summary>
    /// Gets the client-side SimpleModulus decryption keys (server→client).
    /// </summary>
    public static SimpleModulusKeys ClientDecryptKey
    {
        get
        {
            EnsureInitialized();
            return _clientDecryptKey;
        }
    }

    /// <summary>
    /// Forces re-initialization (for tests).
    /// </summary>
    public static void Reset()
    {
        lock (Sync)
        {
            _initialized = false;
        }
    }

    /// <summary>
    /// Initializes keys from the process environment. Safe to call multiple times.
    /// </summary>
    public static void EnsureInitialized()
    {
        if (_initialized)
        {
            return;
        }

        lock (Sync)
        {
            if (_initialized)
            {
                return;
            }

            _environment = ParseEnvironment(System.Environment.GetEnvironmentVariable("PROMU_CRYPTO_ENV"));
            if (_environment == NetworkKeyEnvironment.Local)
            {
                ApplyStockKeys();
            }
            else
            {
                LoadCustomKeysOrThrow(_environment);
            }

            _initialized = true;
        }
    }

    private static NetworkKeyEnvironment ParseEnvironment(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return NetworkKeyEnvironment.Local;
        }

        return value.Trim().ToLowerInvariant() switch
        {
            "local" => NetworkKeyEnvironment.Local,
            "test" => NetworkKeyEnvironment.Test,
            "prod" or "production" => NetworkKeyEnvironment.Prod,
            _ => throw new InvalidOperationException(
                $"Unknown PROMU_CRYPTO_ENV '{value}'. Expected local, test, or prod."),
        };
    }

    private static void ApplyStockKeys()
    {
        _xor3Keys = DefaultKeys.Xor3Keys;
        _xor32Key = DefaultKeys.Xor32Key;
        _serverEncryptKey = PipelinedSimpleModulusEncryptor.DefaultServerKey;
        _serverDecryptKey = PipelinedSimpleModulusDecryptor.DefaultServerKey;
        _clientEncryptKey = PipelinedSimpleModulusEncryptor.DefaultClientKey;
        _clientDecryptKey = PipelinedSimpleModulusDecryptor.DefaultClientKey;
    }

    private static void LoadCustomKeysOrThrow(NetworkKeyEnvironment environment)
    {
        var path = System.Environment.GetEnvironmentVariable("PROMU_CRYPTO_KEYS_PATH");
        PromuNetworkKeyFile? file = null;
        if (!string.IsNullOrWhiteSpace(path))
        {
            if (!File.Exists(path))
            {
                throw new FileNotFoundException(
                    $"PROMU_CRYPTO_KEYS_PATH points to a missing file for environment '{environment}'.", path);
            }

            var json = File.ReadAllText(path);
            file = JsonSerializer.Deserialize<PromuNetworkKeyFile>(json, JsonOptions())
                   ?? throw new InvalidOperationException($"Failed to deserialize crypto keys from '{path}'.");
        }

        _xor3Keys = ParseByteArray(
            file?.Xor3Keys,
            System.Environment.GetEnvironmentVariable("PROMU_XOR3_KEYS"),
            3,
            "Xor3");
        _xor32Key = ParseByteArray(
            file?.Xor32Key,
            System.Environment.GetEnvironmentVariable("PROMU_XOR32_KEY"),
            32,
            "Xor32");

        var serverEncrypt = ParseUIntArray(
            file?.ServerEncryptKey,
            System.Environment.GetEnvironmentVariable("PROMU_SM_SERVER_ENCRYPT"),
            12,
            "ServerEncrypt");
        var serverDecrypt = ParseUIntArray(
            file?.ServerDecryptKey,
            System.Environment.GetEnvironmentVariable("PROMU_SM_SERVER_DECRYPT"),
            12,
            "ServerDecrypt");
        var clientEncrypt = ParseUIntArray(
            file?.ClientEncryptKey,
            System.Environment.GetEnvironmentVariable("PROMU_SM_CLIENT_ENCRYPT"),
            12,
            "ClientEncrypt");
        var clientDecrypt = ParseUIntArray(
            file?.ClientDecryptKey,
            System.Environment.GetEnvironmentVariable("PROMU_SM_CLIENT_DECRYPT"),
            12,
            "ClientDecrypt");

        if (LooksLikeStock(serverEncrypt, serverDecrypt, clientEncrypt, clientDecrypt, _xor3Keys, _xor32Key))
        {
            throw new InvalidOperationException(
                $"Custom crypto keys for '{environment}' must differ from the public stock OpenMU defaults.");
        }

        _serverEncryptKey = SimpleModulusKeys.CreateEncryptionKeys(serverEncrypt);
        _serverDecryptKey = SimpleModulusKeys.CreateDecryptionKeys(serverDecrypt);
        _clientEncryptKey = SimpleModulusKeys.CreateEncryptionKeys(clientEncrypt);
        _clientDecryptKey = SimpleModulusKeys.CreateDecryptionKeys(clientDecrypt);
    }

    private static bool LooksLikeStock(
        uint[] serverEncrypt,
        uint[] serverDecrypt,
        uint[] clientEncrypt,
        uint[] clientDecrypt,
        byte[] xor3,
        byte[] xor32)
    {
        return SequenceEqual(xor3, DefaultKeys.Xor3Keys)
               && SequenceEqual(xor32, DefaultKeys.Xor32Key)
               && SequenceEqual(serverEncrypt, new uint[] { 73326, 109989, 98843, 171058, 13169, 19036, 35482, 29587, 62004, 64409, 35374, 64599 })
               && SequenceEqual(serverDecrypt, new uint[] { 128079, 164742, 70235, 106898, 31544, 2047, 57011, 10183, 48413, 46165, 15171, 37433 })
               && SequenceEqual(clientEncrypt, new uint[] { 128079, 164742, 70235, 106898, 23489, 11911, 19816, 13647, 48413, 46165, 15171, 37433 })
               && SequenceEqual(clientDecrypt, new uint[] { 73326, 109989, 98843, 171058, 18035, 30340, 24701, 11141, 62004, 64409, 35374, 64599 });
    }

    private static bool SequenceEqual(byte[] a, byte[] b)
    {
        if (a.Length != b.Length)
        {
            return false;
        }

        for (var i = 0; i < a.Length; i++)
        {
            if (a[i] != b[i])
            {
                return false;
            }
        }

        return true;
    }

    private static bool SequenceEqual(uint[] a, uint[] b)
    {
        if (a.Length != b.Length)
        {
            return false;
        }

        for (var i = 0; i < a.Length; i++)
        {
            if (a[i] != b[i])
            {
                return false;
            }
        }

        return true;
    }

    private static byte[] ParseByteArray(string? fromFile, string? fromEnv, int expectedLength, string name)
    {
        var raw = !string.IsNullOrWhiteSpace(fromFile) ? fromFile : fromEnv;
        if (string.IsNullOrWhiteSpace(raw))
        {
            throw new InvalidOperationException(
                $"Missing {name} keys. Set PROMU_CRYPTO_KEYS_PATH or the matching PROMU_* env var.");
        }

        var parts = raw.Split(new[] { ',', ' ', ';', '|' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length != expectedLength)
        {
            throw new InvalidOperationException($"{name} must contain {expectedLength} bytes, got {parts.Length}.");
        }

        var result = new byte[expectedLength];
        for (var i = 0; i < expectedLength; i++)
        {
            result[i] = ParseByte(parts[i], name, i);
        }

        return result;
    }

    private static uint[] ParseUIntArray(string? fromFile, string? fromEnv, int expectedLength, string name)
    {
        var raw = !string.IsNullOrWhiteSpace(fromFile) ? fromFile : fromEnv;
        if (string.IsNullOrWhiteSpace(raw))
        {
            throw new InvalidOperationException(
                $"Missing {name} SimpleModulus keys. Set PROMU_CRYPTO_KEYS_PATH or the matching PROMU_* env var.");
        }

        var parts = raw.Split(new[] { ',', ' ', ';', '|' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length != expectedLength)
        {
            throw new InvalidOperationException($"{name} must contain {expectedLength} uints, got {parts.Length}.");
        }

        var result = new uint[expectedLength];
        for (var i = 0; i < expectedLength; i++)
        {
            if (!uint.TryParse(parts[i], NumberStyles.Integer, CultureInfo.InvariantCulture, out result[i]))
            {
                throw new InvalidOperationException($"{name}[{i}] is not a valid uint: '{parts[i]}'.");
            }
        }

        return result;
    }

    private static byte ParseByte(string value, string name, int index)
    {
        if (value.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            if (byte.TryParse(value.AsSpan(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var hex))
            {
                return hex;
            }
        }
        else if (byte.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var dec))
        {
            return dec;
        }

        throw new InvalidOperationException($"{name}[{index}] is not a valid byte: '{value}'.");
    }

    private static JsonSerializerOptions JsonOptions() => new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    private sealed class PromuNetworkKeyFile
    {
        [JsonPropertyName("xor3Keys")]
        public string? Xor3Keys { get; set; }

        [JsonPropertyName("xor32Key")]
        public string? Xor32Key { get; set; }

        [JsonPropertyName("serverEncryptKey")]
        public string? ServerEncryptKey { get; set; }

        [JsonPropertyName("serverDecryptKey")]
        public string? ServerDecryptKey { get; set; }

        [JsonPropertyName("clientEncryptKey")]
        public string? ClientEncryptKey { get; set; }

        [JsonPropertyName("clientDecryptKey")]
        public string? ClientDecryptKey { get; set; }
    }
}
