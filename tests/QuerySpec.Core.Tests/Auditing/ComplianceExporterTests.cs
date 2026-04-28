using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using QuerySpec.Core.Auditing;
using Xunit;
using Moq;

namespace QuerySpec.Core.Tests.Auditing
{
    /// <summary>
    /// Unit tests for ComplianceExporter.
    /// </summary>
    [RequiresUnreferencedCode("Test exercises ComplianceExporter, which uses System.Text.Json reflection-based serialisation over AuditLogEntry.")]
    [RequiresDynamicCode("Test exercises ComplianceExporter, which uses System.Text.Json reflection-based serialisation that emits IL at runtime.")]
    public class ComplianceExporterTests
    {
        /// <summary>Tests that GetUserDataAsync filters by tenant ID.</summary>
        [Fact]
        public async Task GetUserDataAsync_FiltersByTenantId()
        {
            var logs = new List<AuditLogEntry>
            {
                new AuditLogEntry { UserId="u1", TenantId="t1" },
                new AuditLogEntry { UserId="u1", TenantId="t2" },
                new AuditLogEntry { UserId="u2", TenantId="t1" },
            };
            var reader = new Mock<IAuditReader>();
            reader.Setup(r => r.GetAuditsByUserAsync("u1", null))
                  .ReturnsAsync(logs.Where(x => x.UserId == "u1"));

            var exporter = new ComplianceExporter(reader.Object);

            var result = await exporter.GetUserDataAsync("u1", "t1");

            Assert.Single(result);
            Assert.Equal("t1", result.First().TenantId);
        }

        /// <summary>Tests that UserHasAccessedFieldAsync returns true if field was accessed.</summary>
        [Fact]
        public async Task UserHasAccessedFieldAsync_ReturnsTrue_IfFieldAccessed()
        {
            var logs = new List<AuditLogEntry>
            {
                new AuditLogEntry { UserId="u1", TenantId="t1", AccessedSensitiveFields = new List<string>{"f1","f2"}},
            };
            var reader = new Mock<IAuditReader>();
            reader.Setup(r => r.GetAuditsByUserAsync("u1", It.IsAny<DateTime>()))
                  .ReturnsAsync(logs);
            var exporter = new ComplianceExporter(reader.Object);

            var result = await exporter.UserHasAccessedFieldAsync("u1", "f2", new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc));
            Assert.True(result);
        }

        /// <summary>Tests that GenerateGdprExportAsync writes valid JSON to the stream.</summary>
        [Fact]
        public async Task GenerateGdprExportAsync_WritesJsonToStream()
        {
            var logs = new List<AuditLogEntry>
            {
                new AuditLogEntry { UserId="u1", TenantId="t1" },
            };
            var reader = new Mock<IAuditReader>();
            reader.Setup(r => r.GetAuditsByUserAsync("u1", null))
                  .ReturnsAsync(logs);
            var exporter = new ComplianceExporter(reader.Object);

            using var ms = new MemoryStream();
            await exporter.GenerateGdprExportAsync("u1", "t1", ms);
            ms.Position = 0;
            var json = Encoding.UTF8.GetString(ms.ToArray());

            Assert.Contains("u1", json);
            using var doc = JsonDocument.Parse(json);
            Assert.Equal(JsonValueKind.Array, doc.RootElement.ValueKind);
        }

        /// <summary>
        /// Tests that GenerateGdprExportAsync streaming output is byte-for-byte equivalent
        /// to the synchronous Serialize path with the same options.
        /// </summary>
        [Fact]
        public async Task GenerateGdprExportAsync_MatchesSynchronousSerializeOutput()
        {
            var logs = new List<AuditLogEntry>
            {
                new AuditLogEntry { UserId="u1", TenantId="t1" },
                new AuditLogEntry { UserId="u2", TenantId="t2", AccessedSensitiveFields = new List<string> { "email", "ssn" } },
            };
            var reader = new Mock<IAuditReader>();
            reader.Setup(r => r.GetAuditsByUserAsync(It.IsAny<string>(), null))
                  .ReturnsAsync((string uid, DateTime? _) => logs.Where(x => x.UserId == uid));
            reader.Setup(r => r.GetAuditsByUserAsync("u1", null))
                  .ReturnsAsync(logs);
            var exporter = new ComplianceExporter(reader.Object);

            var options = new JsonSerializerOptions { WriteIndented = true };
            var expectedBytes = JsonSerializer.SerializeToUtf8Bytes(logs.Where(x => x.TenantId == "t1"), options);

            using var ms = new MemoryStream();
            await exporter.GenerateGdprExportAsync("u1", "t1", ms);

            Assert.Equal(expectedBytes, ms.ToArray());
        }

        /// <summary>
        /// Tests that a cancelled CancellationToken causes OperationCanceledException
        /// rather than completing the export.
        /// </summary>
        [Fact]
        public async Task GenerateGdprExportAsync_CancelledToken_ThrowsOperationCanceledException()
        {
            var logs = Enumerable.Range(0, 100)
                .Select(i => new AuditLogEntry { UserId = "u1", TenantId = "t1" })
                .ToList();
            var reader = new Mock<IAuditReader>();
            reader.Setup(r => r.GetAuditsByUserAsync("u1", null))
                  .ReturnsAsync(logs);
            var exporter = new ComplianceExporter(reader.Object);

            using var cts = new CancellationTokenSource();
            cts.Cancel();

            using var ms = new MemoryStream();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => exporter.GenerateGdprExportAsync("u1", "t1", ms, cts.Token));
        }

        /// <summary>
        /// Verifies the obsolete <c>GenerateGDPRExportAsync</c> overloads still produce identical
        /// output to <c>GenerateGdprExportAsync</c> until they are removed in 3.0.
        /// </summary>
        [Fact]
        public async Task ObsoleteGenerateGDPRExportAsync_DelegatesToRenamedMethod()
        {
            var logs = new List<AuditLogEntry>
            {
                new AuditLogEntry { UserId="u1", TenantId="t1" },
            };
            var reader = new Mock<IAuditReader>();
            reader.Setup(r => r.GetAuditsByUserAsync("u1", null))
                  .ReturnsAsync(logs);
            var exporter = new ComplianceExporter(reader.Object);

            using var msNew = new MemoryStream();
            using var msOld = new MemoryStream();
            await exporter.GenerateGdprExportAsync("u1", "t1", msNew);
#pragma warning disable CS0618
            await exporter.GenerateGDPRExportAsync("u1", "t1", msOld);
#pragma warning restore CS0618

            Assert.Equal(msNew.ToArray(), msOld.ToArray());
        }
    }
}
