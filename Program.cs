using System.Runtime.InteropServices;
using System.Text;

namespace BAReTool;

internal static class Program
{
    private const string DefaultLicense = "OmNpZDowMDFWSjAwMDAwY3pzaVlZQVE6cGxhdGZvcm06MjY6ZXhwaXJlOm5ldmVyOnZlcnNpb246MTpsaWJ2ZXI6NC4xMC4wOmhtYWM6ODQ1Y2JkMzQ0MDc3YjIxNmRlYTgyOWI3OTIyMzRkM2UwYmUyMzNhYw==";
    private const string DefaultKeyHex = "efa143094711b6563ec2132d4d6bbe8533d4e291ed4820bdb515b26bb57bb3f0";

    private static int Main(string[] args)
    {
        try
        {
            if (args.Length == 0 || IsHelp(args[0]))
            {
                PrintHelp();
                return 0;
            }

            var command = args[0].ToLowerInvariant();
            var options = ParseOptions(args.Skip(1).ToArray());

            return command switch
            {
                "zip-password" => RunZipPassword(options),
                "export-exceldb" => RunExportExcelDb(options),
                "verify-sqlite" => RunVerifySqlite(options),
                _ => Fail($"Unknown command: {args[0]}")
            };
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("ERROR: " + ex.Message);
            return 1;
        }
    }

    private static int RunZipPassword(Dictionary<string, string> options)
    {
        var bundle = GetOption(options, "bundle");
        var zipPath = GetOption(options, "zip");

        if (string.IsNullOrWhiteSpace(bundle) && !string.IsNullOrWhiteSpace(zipPath))
        {
            bundle = Path.GetFileName(zipPath);
        }

        if (string.IsNullOrWhiteSpace(bundle))
        {
            return Fail("zip-password requires --bundle <Archive.zip> or --zip <path-to-zip>.");
        }

        var password = ZipPassword.Create(bundle);
        Console.WriteLine($"Bundle:   {bundle}");
        Console.WriteLine($"Password: {password}");
        return 0;
    }

    private static int RunExportExcelDb(Dictionary<string, string> options)
    {
        var gameDir = GetOption(options, "game-dir");
        var sourceDb = GetOption(options, "source-db");
        var outputDb = GetOption(options, "output");
        var keyHex = GetOption(options, "key-hex") ?? DefaultKeyHex;
        var license = GetOption(options, "license") ?? DefaultLicense;

        if (string.IsNullOrWhiteSpace(gameDir))
        {
            gameDir = FindGameDir();
        }

        if (string.IsNullOrWhiteSpace(gameDir))
        {
            return Fail("Game directory was not found. Pass --game-dir \"D:\\Steam\\steamapps\\common\\BlueArchive\".");
        }

        if (!IsHex(keyHex))
        {
            return Fail("--key-hex must contain only hexadecimal characters.");
        }

        gameDir = Path.GetFullPath(gameDir);
        sourceDb ??= Path.Combine(gameDir, "BlueArchive_Data", "StreamingAssets", "PUB", "Resource", "Preload", "TableBundles", "ExcelDB.db");
        var sqlCipherDll = Path.Combine(gameDir, "BlueArchive_Data", "Plugins", "x86_64", "sqlcipher.dll");
        outputDb ??= Path.Combine(AppContext.BaseDirectory, "out", "ExcelDB_plain.db");

        if (!File.Exists(sourceDb))
        {
            return Fail("ExcelDB.db was not found: " + sourceDb);
        }

        if (!File.Exists(sqlCipherDll))
        {
            return Fail("sqlcipher.dll was not found: " + sqlCipherDll);
        }

        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outputDb))!);
        if (File.Exists(outputDb))
        {
            File.Delete(outputDb);
        }

        var workDir = Path.Combine(AppContext.BaseDirectory, "work");
        Directory.CreateDirectory(workDir);
        var workCopy = Path.Combine(workDir, "ExcelDB_encrypted_workcopy.db");
        if (File.Exists(workCopy))
        {
            File.Delete(workCopy);
        }

        File.Copy(sourceDb, workCopy, overwrite: true);

        using var sqlite = new SqlCipher(sqlCipherDll);
        sqlite.Open(workCopy, readWrite: true, create: true);
        sqlite.Exec($"PRAGMA cipher_license = '{EscapeSql(license)}';", "cipher_license");
        sqlite.Exec($"PRAGMA key = \"x'{keyHex}'\";", "key");
        sqlite.Exec("SELECT count(*) FROM sqlite_master;", "verify");

        var outputSql = EscapeSql(Path.GetFullPath(outputDb).Replace('\\', '/'));
        sqlite.Exec($"ATTACH DATABASE '{outputSql}' AS plaintext KEY '';", "attach plaintext");
        sqlite.Exec("SELECT sqlcipher_export('plaintext');", "sqlcipher_export");
        sqlite.Exec("DETACH DATABASE plaintext;", "detach plaintext");

        Console.WriteLine("Exported plaintext SQLite DB:");
        Console.WriteLine(Path.GetFullPath(outputDb));
        Console.WriteLine($"Size: {new FileInfo(outputDb).Length} bytes");
        return 0;
    }

    private static int RunVerifySqlite(Dictionary<string, string> options)
    {
        var dbPath = GetOption(options, "db");
        if (string.IsNullOrWhiteSpace(dbPath))
        {
            return Fail("verify-sqlite requires --db <path-to-db>.");
        }

        using var stream = File.Open(dbPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        var header = new byte[16];
        var read = stream.Read(header, 0, header.Length);
        var text = read == 16 ? Encoding.ASCII.GetString(header) : "";
        var isSqlite = text == "SQLite format 3\0";

        Console.WriteLine($"File: {Path.GetFullPath(dbPath)}");
        Console.WriteLine($"Header: {text.Replace("\0", "\\0")}");
        Console.WriteLine($"Plain SQLite: {isSqlite}");
        return isSqlite ? 0 : 2;
    }

    private static Dictionary<string, string> ParseOptions(string[] args)
    {
        var options = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            if (!arg.StartsWith("--", StringComparison.Ordinal))
            {
                continue;
            }

            var key = arg[2..];
            if (i + 1 >= args.Length || args[i + 1].StartsWith("--", StringComparison.Ordinal))
            {
                options[key] = "true";
            }
            else
            {
                options[key] = args[++i];
            }
        }

        return options;
    }

    private static string? GetOption(Dictionary<string, string> options, string name)
    {
        return options.TryGetValue(name, out var value) ? value : null;
    }

    private static bool IsHelp(string value)
    {
        return value is "-h" or "--help" or "help" or "/?";
    }

    private static string? FindGameDir()
    {
        var candidates = new[]
        {
            @"D:\Steam\steamapps\common\BlueArchive",
            @"C:\Program Files (x86)\Steam\steamapps\common\BlueArchive",
            @"C:\Program Files\Steam\steamapps\common\BlueArchive"
        };

        return candidates.FirstOrDefault(path => File.Exists(Path.Combine(path, "BlueArchive.exe")));
    }

    private static int Fail(string message)
    {
        Console.Error.WriteLine("ERROR: " + message);
        return 1;
    }

    private static string EscapeSql(string value)
    {
        return value.Replace("'", "''", StringComparison.Ordinal);
    }

    private static bool IsHex(string value)
    {
        return value.Length > 0 && value.All(static c =>
            c is >= '0' and <= '9' ||
            c is >= 'a' and <= 'f' ||
            c is >= 'A' and <= 'F');
    }

    private static void PrintHelp()
    {
        Console.WriteLine("""
BA-ReTool

Commands:
  BA-ReTool.exe zip-password --bundle Excel.zip
  BA-ReTool.exe zip-password --zip "D:\...\Excel.zip"
  BA-ReTool.exe export-exceldb --game-dir "D:\Steam\steamapps\common\BlueArchive"
  BA-ReTool.exe verify-sqlite --db ".\out\ExcelDB_plain.db"

Output:
  export-exceldb writes .\out\ExcelDB_plain.db by default.

Notes:
  This build is intended for personal/research use.
  Current SQLCipher key/license are built in.
  Runtime key capture tools are not included.
""");
    }
}

internal static class ZipPassword
{
    public static string Create(string bundleName)
    {
        var seed = XxHash32(Encoding.UTF8.GetBytes(bundleName));
        var mt = new MersenneTwister(seed);
        return Convert.ToBase64String(mt.NextBytes(15));
    }

    private static uint XxHash32(byte[] data)
    {
        const uint prime1 = 2654435761U;
        const uint prime2 = 2246822519U;
        const uint prime3 = 3266489917U;
        const uint prime4 = 668265263U;
        const uint prime5 = 374761393U;

        uint h32;
        var index = 0;
        var length = data.Length;

        if (length >= 16)
        {
            var limit = length - 16;
            var v1 = unchecked(prime1 + prime2);
            var v2 = prime2;
            uint v3 = 0;
            var v4 = unchecked(0U - prime1);

            while (index <= limit)
            {
                v1 = Round(v1, ReadUInt32(data, index)); index += 4;
                v2 = Round(v2, ReadUInt32(data, index)); index += 4;
                v3 = Round(v3, ReadUInt32(data, index)); index += 4;
                v4 = Round(v4, ReadUInt32(data, index)); index += 4;
            }

            h32 = RotateLeft(v1, 1) + RotateLeft(v2, 7) + RotateLeft(v3, 12) + RotateLeft(v4, 18);
        }
        else
        {
            h32 = prime5;
        }

        h32 += (uint)length;

        while (index <= length - 4)
        {
            h32 += ReadUInt32(data, index) * prime3;
            h32 = RotateLeft(h32, 17) * prime4;
            index += 4;
        }

        while (index < length)
        {
            h32 += data[index] * prime5;
            h32 = RotateLeft(h32, 11) * prime1;
            index++;
        }

        h32 ^= h32 >> 15;
        h32 *= prime2;
        h32 ^= h32 >> 13;
        h32 *= prime3;
        h32 ^= h32 >> 16;
        return h32;

        static uint Round(uint acc, uint input)
        {
            acc += input * prime2;
            acc = RotateLeft(acc, 13);
            acc *= prime1;
            return acc;
        }
    }

    private static uint ReadUInt32(byte[] data, int offset)
    {
        return (uint)(data[offset] | (data[offset + 1] << 8) | (data[offset + 2] << 16) | (data[offset + 3] << 24));
    }

    private static uint RotateLeft(uint value, int count)
    {
        return (value << count) | (value >> (32 - count));
    }
}

internal sealed class MersenneTwister
{
    private readonly uint[] _state = new uint[624];
    private int _index = 624;

    public MersenneTwister(uint seed)
    {
        _state[0] = seed;
        for (var i = 1; i < _state.Length; i++)
        {
            _state[i] = unchecked(1812433253U * (_state[i - 1] ^ (_state[i - 1] >> 30)) + (uint)i);
        }
    }

    public byte[] NextBytes(int count)
    {
        var output = new byte[count];
        var offset = 0;
        while (offset < count)
        {
            var value = NextInt31();
            for (var i = 0; i < 4 && offset < count; i++)
            {
                output[offset++] = (byte)(value >> (8 * i));
            }
        }

        return output;
    }

    private uint NextInt31()
    {
        return ExtractNumber() >> 1;
    }

    private uint ExtractNumber()
    {
        if (_index >= 624)
        {
            Twist();
        }

        var y = _state[_index++];
        y ^= y >> 11;
        y ^= (y << 7) & 0x9D2C5680U;
        y ^= (y << 15) & 0xEFC60000U;
        y ^= y >> 18;
        return y;
    }

    private void Twist()
    {
        for (var i = 0; i < 624; i++)
        {
            var y = (_state[i] & 0x80000000U) + (_state[(i + 1) % 624] & 0x7FFFFFFFU);
            _state[i] = _state[(i + 397) % 624] ^ (y >> 1);
            if ((y & 1) != 0)
            {
                _state[i] ^= 0x9908B0DFU;
            }
        }

        _index = 0;
    }
}

internal sealed class SqlCipher : IDisposable
{
    private const int SqliteOk = 0;
    private readonly nint _library;
    private readonly sqlite3_open_v2 _open;
    private readonly sqlite3_close _close;
    private readonly sqlite3_exec _exec;
    private readonly sqlite3_errmsg _errmsg;
    private readonly sqlite3_free _free;
    private nint _db;

    public SqlCipher(string dllPath)
    {
        _library = NativeLibrary.Load(dllPath);
        _open = GetDelegate<sqlite3_open_v2>("sqlite3_open_v2");
        _close = GetDelegate<sqlite3_close>("sqlite3_close");
        _exec = GetDelegate<sqlite3_exec>("sqlite3_exec");
        _errmsg = GetDelegate<sqlite3_errmsg>("sqlite3_errmsg");
        _free = GetDelegate<sqlite3_free>("sqlite3_free");
    }

    public void Open(string path, bool readWrite, bool create)
    {
        var flags = readWrite ? 0x00000002 : 0x00000001;
        if (create)
        {
            flags |= 0x00000004;
        }

        var rc = _open(NullTerminated(path), out _db, flags, 0);
        if (rc != SqliteOk)
        {
            throw new InvalidOperationException($"sqlite3_open_v2 failed rc={rc}");
        }
    }

    public void Exec(string sql, string operation)
    {
        var rc = _exec(_db, NullTerminated(sql), 0, 0, out var error);
        if (rc == SqliteOk)
        {
            return;
        }

        var message = error != 0 ? PtrToUtf8(error) : PtrToUtf8(_errmsg(_db));
        if (error != 0)
        {
            _free(error);
        }

        throw new InvalidOperationException($"{operation} failed rc={rc} msg={message}");
    }

    public void Dispose()
    {
        if (_db != 0)
        {
            _close(_db);
            _db = 0;
        }

        if (_library != 0)
        {
            NativeLibrary.Free(_library);
        }
    }

    private T GetDelegate<T>(string exportName) where T : Delegate
    {
        var address = NativeLibrary.GetExport(_library, exportName);
        return Marshal.GetDelegateForFunctionPointer<T>(address);
    }

    private static byte[] NullTerminated(string value)
    {
        return Encoding.UTF8.GetBytes(value + "\0");
    }

    private static string PtrToUtf8(nint ptr)
    {
        if (ptr == 0)
        {
            return "";
        }

        var length = 0;
        while (Marshal.ReadByte(ptr, length) != 0)
        {
            length++;
        }

        var bytes = new byte[length];
        Marshal.Copy(ptr, bytes, 0, length);
        return Encoding.UTF8.GetString(bytes);
    }

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int sqlite3_open_v2(byte[] filename, out nint db, int flags, nint vfs);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int sqlite3_close(nint db);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int sqlite3_exec(nint db, byte[] sql, nint callback, nint arg, out nint errmsg);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate nint sqlite3_errmsg(nint db);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void sqlite3_free(nint ptr);
}
