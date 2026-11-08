using System.Reflection;

using Hexalith.EventStore.Server.Extensions;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Extensions;

/// <summary>Optional module metadata follows declarations into refusals without breaking the existing declaration API.</summary>
public sealed class EventStoreExtensionOwnerDiagnosticTests
{
    [Fact]
    public void LegacyConstructorAndDeconstructionRemainAvailableWithoutModuleMetadata()
    {
        ConstructorInfo constructor = typeof(EventStoreExtensionDeclaration)
            .GetConstructor([typeof(string), typeof(string), typeof(int)]).ShouldNotBeNull();
        EventStoreExtensionDeclaration declaration = constructor.Invoke(["Probe.Legacy", "probe", 1])
            .ShouldBeOfType<EventStoreExtensionDeclaration>();
        declaration.ModuleId.ShouldBeNull();
        MethodInfo deconstruct = typeof(EventStoreExtensionDeclaration).GetMethod("Deconstruct",
            [typeof(string).MakeByRefType(), typeof(string).MakeByRefType(), typeof(int).MakeByRefType()]).ShouldNotBeNull();
        object?[] values = [null, null, null];
        deconstruct.Invoke(declaration, values);
        object?[] expected = ["Probe.Legacy", "probe", 1];
        values.ShouldBe(expected);
    }

    [Fact]
    public void ConfigurationRetainsOptionalModuleMetadataAndAcceptsLegacyEntries()
    {
        Dictionary<string, string?> values = Entry("Probe.Owned", "owner-module");
        const string second = "EventStore:Extensions:Packages:1:";
        values[second + "PackageId"] = "Probe.Legacy";
        values[second + "ExtensionId"] = "legacy";
        values[second + "ApiMajor"] = "1";
        IReadOnlyList<EventStoreExtensionDeclaration> declarations = EventStoreExtensionLoader.Read(Configuration(values));
        declarations.Count.ShouldBe(2);
        declarations[0].ShouldBe(new EventStoreExtensionDeclaration("Probe.Owned", "probe", 1) { ModuleId = "owner-module" });
        declarations[1].ShouldBe(new EventStoreExtensionDeclaration("Probe.Legacy", "legacy", 1));
    }

    [Theory]
    [InlineData("unsupported", EventStoreExtensionConditions.UnsupportedMajor)]
    [InlineData("drift-id", EventStoreExtensionConditions.DeclarationDrift)]
    [InlineData("drift-major", EventStoreExtensionConditions.DeclarationDrift)]
    [InlineData("attribute-missing", EventStoreExtensionConditions.AttributeMissing)]
    [InlineData("unavailable", EventStoreExtensionConditions.Unavailable)]
    [InlineData("wrong-assembly", EventStoreExtensionConditions.Unavailable)]
    [InlineData("foreign-source", EventStoreExtensionConditions.AdapterSourceUnavailable)]
    public void RuntimeRefusalsNameTheDeclaringModuleAndPackage(string fault, string condition)
    {
        string package = "Probe.Owned." + Guid.NewGuid().ToString("N");
        Assembly assembly = ExtensionProbeAssembly.Create(fault == "wrong-assembly" ? package + ".Other" : package,
            fault == "attribute-missing" ? null : fault == "drift-id" ? "other" : "probe",
            fault == "drift-major" ? 2 : 1, foreignSource: fault == "foreign-source");
        EventStoreExtensionDeclaration declaration = new(package, "probe", fault == "unsupported" ? 2 : 1)
        {
            ModuleId = "owner-module",
        };
        EventStoreExtensionException refusal = Should.Throw<EventStoreExtensionException>(() => new ServiceCollection()
            .AddEventStoreExtensions([declaration], _ => fault == "unavailable" ? throw new FileNotFoundException() : assembly));
        refusal.Condition.ShouldBe(condition);
        refusal.Message.ShouldContain("Module 'owner-module'");
        refusal.Message.ShouldContain(package);
    }

    [Theory]
    [InlineData("identity", true, true)]
    [InlineData("identity", true, false)]
    [InlineData("identity", false, true)]
    [InlineData("identity", false, false)]
    [InlineData("package", true, true)]
    [InlineData("package", true, false)]
    [InlineData("package", false, true)]
    [InlineData("package", false, false)]
    public void DuplicateRefusalsAssociateSuppliedModulesAndPreserveLegacyMessages(string duplicate, bool firstOwned, bool secondOwned)
    {
        EventStoreExtensionDeclaration first = new("Probe.First", "probe", 1) { ModuleId = firstOwned ? "first-module" : null };
        EventStoreExtensionDeclaration second = new(duplicate == "package" ? "probe.first" : "Probe.Second",
            duplicate == "identity" ? "probe" : "other", 1) { ModuleId = secondOwned ? "second-module" : null };
        EventStoreExtensionException refusal = Should.Throw<EventStoreExtensionException>(() => new ServiceCollection()
            .AddEventStoreExtensions([first, second], _ => throw new FileNotFoundException()));
        refusal.Condition.ShouldBe(EventStoreExtensionConditions.Duplicate);
        refusal.Message.ShouldContain("package 'Probe.First' ('probe')" + (firstOwned ? " from module 'first-module'" : string.Empty));
        refusal.Message.ShouldContain($"package '{second.PackageId}' ('{second.ExtensionId}')" + (secondOwned ? " from module 'second-module'" : string.Empty));
        if (!firstOwned) refusal.Message.ShouldNotContain("first-module");
        if (!secondOwned) refusal.Message.ShouldNotContain("second-module");
        if (!firstOwned && !secondOwned)
            refusal.Message.ShouldBe(duplicate == "identity"
                ? "eventstore-extension-duplicate: Extension identity 'probe' is declared twice, by package 'Probe.First' ('probe') and package 'Probe.Second' ('probe')."
                : "eventstore-extension-duplicate: Extension package 'probe.first' is declared twice, by package 'Probe.First' ('probe') and package 'probe.first' ('other').");
    }

    [Theory]
    [InlineData("owner")]
    [InlineData("0")]
    public void ObjectOrArrayModuleMetadataIsRefusedInsteadOfDiscarded(string child)
    {
        Dictionary<string, string?> values = Entry("Probe.Owned", "owner-module");
        values.Remove("EventStore:Extensions:Packages:0:ModuleId");
        values["EventStore:Extensions:Packages:0:ModuleId:" + child] = "owner-module";
        EventStoreExtensionException refusal = Should.Throw<EventStoreExtensionException>(() =>
            new ServiceCollection().AddEventStoreExtensions(Configuration(values)));
        refusal.Condition.ShouldBe(EventStoreExtensionConditions.InvalidDeclaration);
        refusal.Message.ShouldContain("scalar ModuleId");
        refusal.Message.ShouldContain("Probe.Owned");
    }

    [Fact]
    public void MalformedEntryWithValidOwnershipNamesTheInvalidMajorAndAllowsOptionalMetadata()
    {
        Dictionary<string, string?> values = Entry("Probe.Owned", "owner-module");
        values["EventStore:Extensions:Packages:0:ApiMajor"] = "zero";
        EventStoreExtensionException refusal = Should.Throw<EventStoreExtensionException>(() =>
            new ServiceCollection().AddEventStoreExtensions(Configuration(values)));
        refusal.Condition.ShouldBe(EventStoreExtensionConditions.InvalidDeclaration);
        refusal.Message.ShouldContain("Module 'owner-module'");
        refusal.Message.ShouldContain("ApiMajor 'zero'");
        refusal.Message.ShouldContain("optional scalar ModuleId");
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(" owner-module ")]
    public void SuppliedModuleMetadataMustBeNonblankAndTrimmed(string module)
    {
        EventStoreExtensionException refusal = Should.Throw<EventStoreExtensionException>(() =>
            new ServiceCollection().AddEventStoreExtensions(Configuration(Entry("Probe.Owned", module))));
        refusal.Condition.ShouldBe(EventStoreExtensionConditions.InvalidDeclaration);
        refusal.Message.ShouldContain("ModuleId");
        refusal.Message.ShouldContain("Probe.Owned");
    }

    private static Dictionary<string, string?> Entry(string package, string module) => new()
    {
        ["EventStore:Extensions:Packages:0:PackageId"] = package,
        ["EventStore:Extensions:Packages:0:ExtensionId"] = "probe",
        ["EventStore:Extensions:Packages:0:ApiMajor"] = "1",
        ["EventStore:Extensions:Packages:0:ModuleId"] = module,
    };

    private static IConfiguration Configuration(Dictionary<string, string?> values) => new ConfigurationBuilder().AddInMemoryCollection(values).Build();
}
