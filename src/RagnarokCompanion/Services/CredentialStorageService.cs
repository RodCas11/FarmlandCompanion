using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace RagnarokCompanion.Services;

public interface ICredentialStorageService
{
    bool HasKey { get; }
    void Store(string key);
    string? Read();
    void Delete();
}
public sealed class CredentialStorageService : ICredentialStorageService
{
    private readonly string path;
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("RagnarokCompanion/RagnaPlace/v1");
    public CredentialStorageService(string root) => path = Path.Combine(root, "ragnaplace-key.dpapi");
    public bool HasKey => File.Exists(path);
    public void Store(string key)
    {
        if (string.IsNullOrWhiteSpace(key) || key.Length > 4096) throw new ArgumentException("Chave vazia ou inválida.");
        foreach (char c in key.Trim()) if (c < 33 || c > 126) throw new ArgumentException("A chave contém caracteres inválidos.");
        byte[] clear = Encoding.UTF8.GetBytes(key.Trim());
        try
        {
            byte[] encrypted = ProtectedData.Protect(clear, Entropy, DataProtectionScope.CurrentUser);
            File.WriteAllBytes(path + ".tmp", encrypted);
            File.Move(path + ".tmp", path, true);
        }
        finally { CryptographicOperations.ZeroMemory(clear); }
    }
    public string? Read()
    {
        if (!HasKey) return null;
        byte[] clear = ProtectedData.Unprotect(File.ReadAllBytes(path), Entropy, DataProtectionScope.CurrentUser);
        try { return Encoding.UTF8.GetString(clear); }
        finally { CryptographicOperations.ZeroMemory(clear); }
    }
    public void Delete() { File.Delete(path); File.Delete(path + ".tmp"); }
}
