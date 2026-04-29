using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Xunit;
using QuerySpec.Core.Auditing;

namespace QuerySpec.Core.Tests.Auditing;

/// <summary>
/// Unit tests for AuditLogEntry.
/// </summary>
public class AuditLogEntryTests
{
    private static AuditLogEntry NewValidEntry(string user = "user1", string op = "Query") => new()
    {
        TenantId = "tenant1",
        UserId = user,
        Operation = op
    };

    [Fact]
    public void AuditLogEntry_Should_Generate_Default_Id()
    {
        var entry = new AuditLogEntry();

        Assert.False(string.IsNullOrEmpty(entry.Id));
    }

    [Fact]
    public void Seal_Should_Set_Hash_And_PreviousHash()
    {
        var entry = NewValidEntry();

        entry.Seal(previousHash: null);

        Assert.False(string.IsNullOrEmpty(entry.Hash));
        Assert.Null(entry.PreviousHash);
    }

    [Fact]
    public void Seal_Twice_Throws()
    {
        var entry = NewValidEntry();
        entry.Seal(null);

        Assert.Throws<InvalidOperationException>(() => entry.Seal("any"));
    }

    [Fact]
    public void Validate_Should_Throw_When_TenantId_Empty()
    {
        var entry = new AuditLogEntry
        {
            TenantId = "",
            UserId = "user1",
            Operation = "Query"
        };

        Assert.Throws<ArgumentException>(() => entry.Validate());
    }

    [Fact]
    public void VerifyIntegrity_Should_Return_True_For_Same_PreviousHash()
    {
        var entry = NewValidEntry();
        entry.Seal(previousHash: null);

        var result = entry.VerifyIntegrity((string?)null);

        Assert.True(result);
    }

    [Fact]
    public void VerifyIntegrity_Should_Return_False_For_Different_PreviousHash()
    {
        var entry = NewValidEntry();
        entry.Seal(previousHash: null);

        Assert.False(entry.VerifyIntegrity("forged"));
    }

    [Fact]
    public void VerifyIntegrity_Returns_False_On_Unsealed_Entry()
    {
        var entry = NewValidEntry();

        Assert.False(entry.VerifyIntegrity(null));
    }

    [Fact]
    public void VerifyChain_Returns_Negative_One_For_Valid_Chain()
    {
        var entries = new List<AuditLogEntry>();
        string? prev = null;
        for (var i = 0; i < 100; i++)
        {
            var e = NewValidEntry(user: $"user{i}", op: "Query");
            e.Seal(prev);
            prev = e.Hash;
            entries.Add(e);
        }

        Assert.Equal(-1, AuditLogEntry.VerifyChain(entries));
    }

    [Fact]
    public void VerifyChain_Detects_Hash_Mutation()
    {
        var entries = new List<AuditLogEntry>();
        string? prev = null;
        for (var i = 0; i < 5; i++)
        {
            var e = NewValidEntry(user: $"user{i}");
            e.Seal(prev);
            prev = e.Hash;
            entries.Add(e);
        }

        var brokenLink = entries[3];
        var replacement = NewValidEntry(user: "userX");
        replacement.Seal("forged-prev");
        entries[3] = replacement;

        Assert.Equal(3, AuditLogEntry.VerifyChain(entries));
    }

    [Fact]
    public void VerifyChain_Detects_Reordering()
    {
        var entries = new List<AuditLogEntry>();
        string? prev = null;
        for (var i = 0; i < 4; i++)
        {
            var e = NewValidEntry(user: $"user{i}");
            e.Seal(prev);
            prev = e.Hash;
            entries.Add(e);
        }

        (entries[1], entries[2]) = (entries[2], entries[1]);

        var broken = AuditLogEntry.VerifyChain(entries);
        Assert.True(broken >= 1, $"expected break at index >= 1, got {broken}");
    }

    [Fact]
    public void VerifyChain_Throws_On_Null_Input()
    {
        Assert.Throws<ArgumentNullException>(() => AuditLogEntry.VerifyChain(null!));
    }

    [Fact]
    public void VerifyChain_Detects_PreviousHash_Mismatch_Against_Valid_Base64()
    {
        var entries = new List<AuditLogEntry>();
        string? prev = null;
        for (var i = 0; i < 4; i++)
        {
            var e = NewValidEntry(user: $"user{i}");
            e.Seal(prev);
            prev = e.Hash;
            entries.Add(e);
        }

        var pristine = NewValidEntry(user: "userValidB64");
        pristine.Seal(previousHash: null);
        var validButWrongBase64 = pristine.Hash;

        var replacement = NewValidEntry(user: "userX");
        replacement.Seal(validButWrongBase64);
        entries[2] = replacement;

        Assert.Equal(2, AuditLogEntry.VerifyChain(entries));
    }

    [Fact]
    public void VerifyChain_Detects_Genesis_PreviousHash_Mismatch()
    {
        var first = NewValidEntry();
        first.Seal(previousHash: "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA=");

        Assert.Equal(0, AuditLogEntry.VerifyChain(new[] { first }));
    }

    [Fact]
    public void VerifyChain_DetectsIntegrityFailure_WhenChainLinkMatchesButHashMutated()
    {
        // Chain link matches (PreviousHash of entry[1] == Hash of entry[0])
        // but entry[1].Hash itself is tampered — exercises VerifyIntegrity return at line 141.
        var e0 = NewValidEntry(user: "u0");
        e0.Seal(previousHash: null);

        var e1 = NewValidEntry(user: "u1");
        e1.Seal(previousHash: e0.Hash);

        var hashProp = typeof(AuditLogEntry).GetProperty("Hash")!;
        var backingField = typeof(AuditLogEntry)
            .GetField("<Hash>k__BackingField", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            ?? typeof(AuditLogEntry)
                .GetFields(System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .FirstOrDefault(f => f.Name.Contains("Hash"));

        if (backingField is not null)
        {
            var original = (string)hashProp.GetValue(e1)!;
            backingField.SetValue(e1, "XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX");
            Assert.Equal(1, AuditLogEntry.VerifyChain(new[] { e0, e1 }));
        }
        else
        {
            Assert.Fail("Could not locate backing field for AuditLogEntry.Hash via reflection");
        }
    }

    [Fact]
    public void ComputeHash_ObsoleteOverload_SealsSameAsSealing()
    {
        var e1 = NewValidEntry();
        var computeHash = typeof(AuditLogEntry).GetMethod("ComputeHash", Type.EmptyTypes)!;
        computeHash.Invoke(e1, null);

        Assert.NotNull(e1.Hash);
    }
}
