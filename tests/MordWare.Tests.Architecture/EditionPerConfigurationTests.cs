using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

namespace MordWare.Tests.Architecture;

/// <summary>
/// Guardrail 7, spec scenario "Automated check of the edition per configuration": the App built in the
/// configuration of this test run carries <c>[assembly: AssemblyMetadata("MordWare.Edition", ...)]</c>
/// = DEV for every configuration except Release, which is PROD. Read from the build output by metadata
/// only: the App is never loaded or run.
/// </summary>
public class EditionPerConfigurationTests
{
    [Fact]
    public void App_build_is_DEV_except_in_Release()
    {
        var configuration = new DirectoryInfo(AppContext.BaseDirectory).Parent!.Name;
        var expected = string.Equals(configuration, "Release", StringComparison.OrdinalIgnoreCase) ? "PROD" : "DEV";

        var app = MordWareArchitecture.FindBuildOutput(Path.Combine("src", "MordWare.App"), "MordWare.App.dll");

        Assert.Equal(expected, ReadAssemblyMetadata(app, "MordWare.Edition"));
    }

    /// <summary>The value of the assembly's <c>AssemblyMetadata(key, value)</c> attribute, or null.</summary>
    private static string? ReadAssemblyMetadata(string assemblyPath, string key)
    {
        using var stream = File.OpenRead(assemblyPath);
        using var pe = new PEReader(stream);
        var reader = pe.GetMetadataReader();

        string? found = null;
        foreach (var handle in reader.GetAssemblyDefinition().GetCustomAttributes())
        {
            var attribute = reader.GetCustomAttribute(handle);
            if (attribute.Constructor.Kind != HandleKind.MemberReference)
            {
                continue;
            }

            var constructor = reader.GetMemberReference((MemberReferenceHandle)attribute.Constructor);
            if (constructor.Parent.Kind != HandleKind.TypeReference)
            {
                continue;
            }

            var type = reader.GetTypeReference((TypeReferenceHandle)constructor.Parent);
            if (reader.GetString(type.Namespace) != "System.Reflection" || reader.GetString(type.Name) != "AssemblyMetadataAttribute")
            {
                continue;
            }

            // Blob: prolog 0x0001, then two SerString arguments (key, value).
            var blob = reader.GetBlobReader(attribute.Value);
            if (blob.ReadUInt16() != 1)
            {
                continue;
            }

            if (blob.ReadSerializedString() == key)
            {
                Assert.Null(found); // one value only, never two contradicting stamps
                found = blob.ReadSerializedString();
            }
        }

        return found;
    }
}
