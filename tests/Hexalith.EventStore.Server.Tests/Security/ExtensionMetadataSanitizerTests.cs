namespace Hexalith.EventStore.Server.Tests.Security;

using Hexalith.EventStore.CommandApi.Configuration;
using Hexalith.EventStore.CommandApi.Validation;

using Microsoft.Extensions.Options;

using Shouldly;

/// <summary>
/// Story 5.4, Task 8: Extension metadata sanitizer tests (AC #3, #10).
/// Validates size limits, character sets, and injection pattern detection.
/// </summary>
public class ExtensionMetadataSanitizerTests
{
    private static ExtensionMetadataSanitizer CreateSanitizer(ExtensionMetadataOptions? options = null)
    {
        options ??= new ExtensionMetadataOptions();
        return new ExtensionMetadataSanitizer(Options.Create(options));
    }

    // --- Task 8.2: Null/empty extensions ---

    [Fact]
    public void Sanitize_NullExtensions_ReturnsSuccess()
    {
        ExtensionMetadataSanitizer sanitizer = CreateSanitizer();
        SanitizeResult result = sanitizer.Sanitize(null);
        result.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public void Sanitize_EmptyExtensions_ReturnsSuccess()
    {
        ExtensionMetadataSanitizer sanitizer = CreateSanitizer();
        SanitizeResult result = sanitizer.Sanitize(new Dictionary<string, string>());
        result.IsSuccess.ShouldBeTrue();
    }

    // --- Task 8.3: Valid extensions ---

    [Fact]
    public void Sanitize_ValidExtensions_ReturnsSuccess()
    {
        ExtensionMetadataSanitizer sanitizer = CreateSanitizer();

        var extensions = new Dictionary<string, string>
        {
            ["trace-id"] = "abc-123",
            ["source.system"] = "billing-service",
            ["request_version"] = "2.0",
        };

        SanitizeResult result = sanitizer.Sanitize(extensions);
        result.IsSuccess.ShouldBeTrue();
    }

    // --- Task 8.4: Oversized total ---

    [Fact]
    public void Sanitize_OversizedTotal_ReturnsFailure()
    {
        ExtensionMetadataSanitizer sanitizer = CreateSanitizer(new ExtensionMetadataOptions { MaxTotalSizeBytes = 100 });

        var extensions = new Dictionary<string, string>
        {
            ["key1"] = new string('a', 60),
            ["key2"] = new string('b', 60),
        };

        SanitizeResult result = sanitizer.Sanitize(extensions);
        result.IsSuccess.ShouldBeFalse();
        result.RejectionReason!.ShouldContain("exceeds maximum");
    }

    // --- Task 8.5: Oversized key ---

    [Fact]
    public void Sanitize_OversizedKey_ReturnsFailure()
    {
        ExtensionMetadataSanitizer sanitizer = CreateSanitizer(new ExtensionMetadataOptions { MaxKeyLength = 10 });

        var extensions = new Dictionary<string, string>
        {
            [new string('a', 11)] = "value",
        };

        SanitizeResult result = sanitizer.Sanitize(extensions);
        result.IsSuccess.ShouldBeFalse();
        result.RejectionReason!.ShouldContain("key length");
    }

    // --- Task 8.6: Oversized value ---

    [Fact]
    public void Sanitize_OversizedValue_ReturnsFailure()
    {
        ExtensionMetadataSanitizer sanitizer = CreateSanitizer(new ExtensionMetadataOptions { MaxValueLength = 10 });

        var extensions = new Dictionary<string, string>
        {
            ["key"] = new string('a', 11),
        };

        SanitizeResult result = sanitizer.Sanitize(extensions);
        result.IsSuccess.ShouldBeFalse();
        result.RejectionReason!.ShouldContain("value length");
    }

    // --- Task 8.7: Too many extensions ---

    [Fact]
    public void Sanitize_TooManyExtensions_ReturnsFailure()
    {
        ExtensionMetadataSanitizer sanitizer = CreateSanitizer(new ExtensionMetadataOptions { MaxExtensionCount = 2 });

        var extensions = new Dictionary<string, string>
        {
            ["key1"] = "val1",
            ["key2"] = "val2",
            ["key3"] = "val3",
        };

        SanitizeResult result = sanitizer.Sanitize(extensions);
        result.IsSuccess.ShouldBeFalse();
        result.RejectionReason!.ShouldContain("count");
    }

    // --- Task 8.8: Control characters ---

    [Theory]
    [InlineData("\x00")]
    [InlineData("\x01")]
    [InlineData("\x1F")]
    [InlineData("value\x00injected")]
    public void Sanitize_ControlCharacters_ReturnsFailure(string value)
    {
        ExtensionMetadataSanitizer sanitizer = CreateSanitizer();

        var extensions = new Dictionary<string, string>
        {
            ["key"] = value,
        };

        SanitizeResult result = sanitizer.Sanitize(extensions);
        result.IsSuccess.ShouldBeFalse();
        result.RejectionReason!.ShouldContain("control characters");
    }

    [Fact]
    public void Sanitize_AllowedWhitespace_ReturnsSuccess()
    {
        ExtensionMetadataSanitizer sanitizer = CreateSanitizer();

        var extensions = new Dictionary<string, string>
        {
            ["key"] = "value\twith\ttabs\nand\nnewlines\rand\rreturns",
        };

        SanitizeResult result = sanitizer.Sanitize(extensions);
        result.IsSuccess.ShouldBeTrue();
    }

    // --- Task 8.9: Script tag injection ---

    [Theory]
    [InlineData("<script>alert(1)</script>")]
    [InlineData("<SCRIPT>alert(1)</SCRIPT>")]
    [InlineData("test<script src='evil.js'>")]
    [InlineData("javascript:alert(1)")]
    [InlineData("<iframe src='evil.html'>")]
    [InlineData("<object data='evil.swf'>")]
    [InlineData("<embed src='evil.swf'>")]
    [InlineData("x onclick=alert(1)")]
    public void Sanitize_ScriptTagInjection_ReturnsFailure(string value)
    {
        ExtensionMetadataSanitizer sanitizer = CreateSanitizer();

        var extensions = new Dictionary<string, string>
        {
            ["key"] = value,
        };

        SanitizeResult result = sanitizer.Sanitize(extensions);
        result.IsSuccess.ShouldBeFalse();
        result.RejectionReason!.ShouldContain("XSS");
    }

    // --- Task 8.10: SQL injection ---

    [Theory]
    [InlineData("'; DROP TABLE users; --")]
    [InlineData("'; DELETE FROM orders; --")]
    [InlineData("x UNION SELECT * FROM passwords")]
    public void Sanitize_SqlInjection_ReturnsFailure(string value)
    {
        ExtensionMetadataSanitizer sanitizer = CreateSanitizer();

        var extensions = new Dictionary<string, string>
        {
            ["key"] = value,
        };

        SanitizeResult result = sanitizer.Sanitize(extensions);
        result.IsSuccess.ShouldBeFalse();
        result.RejectionReason!.ShouldContain("SQL");
    }

    // --- Task 8.11: LDAP injection ---

    [Theory]
    [InlineData(")(cn=*)")]
    [InlineData("*)(uid=*)")]
    public void Sanitize_LdapInjection_ReturnsFailure(string value)
    {
        ExtensionMetadataSanitizer sanitizer = CreateSanitizer();

        var extensions = new Dictionary<string, string>
        {
            ["key"] = value,
        };

        SanitizeResult result = sanitizer.Sanitize(extensions);
        result.IsSuccess.ShouldBeFalse();
        result.RejectionReason!.ShouldContain("LDAP");
    }

    // --- Task 8.12: Unicode normalization ---

    [Fact]
    public void Sanitize_UnicodeNormalization_HandlesConsistently()
    {
        ExtensionMetadataSanitizer sanitizer = CreateSanitizer();

        // NFC and NFD forms of "é"
        string nfc = "\u00E9"; // é as single codepoint
        string nfd = "\u0065\u0301"; // e + combining accent

        var extensionsNfc = new Dictionary<string, string> { ["key"] = $"value-{nfc}" };
        var extensionsNfd = new Dictionary<string, string> { ["key"] = $"value-{nfd}" };

        SanitizeResult resultNfc = sanitizer.Sanitize(extensionsNfc);
        SanitizeResult resultNfd = sanitizer.Sanitize(extensionsNfd);

        // Both should be treated consistently (both accepted as common UTF-8)
        resultNfc.IsSuccess.ShouldBe(resultNfd.IsSuccess);
    }

    // --- Task 8.13: Default options ---

    [Fact]
    public void ExtensionMetadataOptions_DefaultValues_AreReasonable()
    {
        var options = new ExtensionMetadataOptions();

        options.MaxTotalSizeBytes.ShouldBe(4096);
        options.MaxKeyLength.ShouldBe(128);
        options.MaxValueLength.ShouldBe(2048);
        options.MaxExtensionCount.ShouldBe(32);
    }

    // --- Additional: Key character validation ---

    [Theory]
    [InlineData("key with spaces")]
    [InlineData("key<inject>")]
    [InlineData("key;drop")]
    [InlineData("")]
    public void Sanitize_InvalidKeyCharacters_ReturnsFailure(string key)
    {
        ExtensionMetadataSanitizer sanitizer = CreateSanitizer();

        var extensions = new Dictionary<string, string>
        {
            [key] = "value",
        };

        SanitizeResult result = sanitizer.Sanitize(extensions);
        result.IsSuccess.ShouldBeFalse();
    }

    // --- Path traversal ---

    [Theory]
    [InlineData("../../etc/passwd")]
    [InlineData("..\\windows\\system32")]
    public void Sanitize_PathTraversal_ReturnsFailure(string value)
    {
        ExtensionMetadataSanitizer sanitizer = CreateSanitizer();

        var extensions = new Dictionary<string, string>
        {
            ["key"] = value,
        };

        SanitizeResult result = sanitizer.Sanitize(extensions);
        result.IsSuccess.ShouldBeFalse();
        result.RejectionReason!.ShouldContain("path traversal");
    }
}
