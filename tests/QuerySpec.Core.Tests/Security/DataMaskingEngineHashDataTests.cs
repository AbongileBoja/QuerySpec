using System;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Xunit;
using QuerySpec.Core.Security;

namespace QuerySpec.Core.Tests.Security;

public class DataMaskingEngineHashDataTests
{
    private static byte[] FixedKey() => new byte[32]
    {
        0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08,
        0x09, 0x0A, 0x0B, 0x0C, 0x0D, 0x0E, 0x0F, 0x10,
        0x11, 0x12, 0x13, 0x14, 0x15, 0x16, 0x17, 0x18,
        0x19, 0x1A, 0x1B, 0x1C, 0x1D, 0x1E, 0x1F, 0x20,
    };

    [Fact]
    public void MaskHash_KnownFixture_MatchesReferenceOutput()
    {
        var key = FixedKey();
        var expected = ComputeExpectedHmac(key, "123-45-6789");

        var engine = new DataMaskingEngine(key);
        engine.RegisterFieldMask("Ssn", MaskingStrategy.HashMask);

        var actual = engine.Mask("Ssn", "123-45-6789");

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void MaskHash_KnownFixture_WithTenant_MatchesReferenceOutput()
    {
        var key = FixedKey();
        var tenantKey = ComputeExpectedHmacBytes(key, "tenant-A");
        var expected = ComputeExpectedHmac(tenantKey, "123-45-6789");

        var engine = new DataMaskingEngine(key);
        engine.RegisterFieldMask("Ssn", MaskingStrategy.HashMask);

        var actual = engine.Mask("Ssn", "123-45-6789", tenantId: "tenant-A");

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void MaskHash_LongValue_OverStackAllocThreshold_ProducesCorrectOutput()
    {
        var key = FixedKey();
        var longValue = new string('x', 300);
        var expected = ComputeExpectedHmac(key, longValue);

        var engine = new DataMaskingEngine(key);
        engine.RegisterFieldMask("Field", MaskingStrategy.HashMask);

        var actual = engine.Mask("Field", longValue);

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void TenantCache_MultipleDistinctTenants_ProducesDeterministicResults()
    {
        var key = FixedKey();
        var engine = new DataMaskingEngine(key);
        engine.RegisterFieldMask("Ssn", MaskingStrategy.HashMask);

        var tenants = new[] { "alpha", "beta", "gamma", "delta", "epsilon" };
        var firstPass = new string[tenants.Length];
        var secondPass = new string[tenants.Length];

        for (var i = 0; i < tenants.Length; i++)
            firstPass[i] = engine.Mask("Ssn", "123-45-6789", tenantId: tenants[i]);

        for (var i = 0; i < tenants.Length; i++)
            secondPass[i] = engine.Mask("Ssn", "123-45-6789", tenantId: tenants[i]);

        Assert.Equal(firstPass, secondPass);

        for (var i = 0; i < tenants.Length; i++)
            for (var j = i + 1; j < tenants.Length; j++)
                Assert.NotEqual(firstPass[i], firstPass[j]);
    }

    [Fact]
    public void TenantCache_HundredDistinctTenants_AllResultsDeterministic()
    {
        var key = FixedKey();
        var engine = new DataMaskingEngine(key);
        engine.RegisterFieldMask("Ssn", MaskingStrategy.HashMask);

        const int count = 100;
        var results = new string[count];

        for (var i = 0; i < count; i++)
            results[i] = engine.Mask("Ssn", "123-45-6789", tenantId: $"tenant-{i}");

        for (var i = 0; i < count; i++)
        {
            var repeated = engine.Mask("Ssn", "123-45-6789", tenantId: $"tenant-{i}");
            Assert.Equal(results[i], repeated);
        }
    }

    [Fact]
    public void TenantCache_EvictionPath_ContinuesToProduceCorrectResults()
    {
        var key = FixedKey();
        var engine = new DataMaskingEngine(key);
        engine.RegisterFieldMask("Ssn", MaskingStrategy.HashMask);

        const int tenantCount = 1025;
        var firstResult = engine.Mask("Ssn", "123-45-6789", tenantId: "tenant-0");

        for (var i = 1; i < tenantCount; i++)
            engine.Mask("Ssn", "123-45-6789", tenantId: $"tenant-{i}");

        var afterEviction = engine.Mask("Ssn", "123-45-6789", tenantId: "tenant-0");
        Assert.Equal(firstResult, afterEviction);
    }

    private static string ComputeExpectedHmac(byte[] key, string value)
        => Convert.ToBase64String(ComputeExpectedHmacBytes(key, value));

    private static byte[] ComputeExpectedHmacBytes(byte[] key, string value)
    {
        var data = Encoding.UTF8.GetBytes(value);
        var result = new byte[HMACSHA256.HashSizeInBytes];
        HMACSHA256.HashData(key, data, result);
        return result;
    }
}
