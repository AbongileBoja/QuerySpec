using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using QuerySpec.Core.Auditing;
using Xunit;
using Moq;

namespace QuerySpec.Core.Tests.Auditing
{
    /// <summary>
    /// Unit tests for ComplianceExporter.
    /// </summary>
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

        /// <summary>Tests that GenerateGDPRExportAsync writes JSON to the stream.</summary>
        [Fact]
        public async Task GenerateGDPRExportAsync_WritesJsonToStream()
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
            await exporter.GenerateGDPRExportAsync("u1", "t1", ms);
            var json = Encoding.UTF8.GetString(ms.ToArray());
            Assert.Contains("u1", json);
        }
    }
}
