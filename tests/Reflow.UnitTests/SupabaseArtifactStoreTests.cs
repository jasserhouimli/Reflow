using Reflow.Modules.DataProcessing.Artifacts;
using Xunit;

namespace Reflow.UnitTests;

public class SupabaseArtifactStoreTests
{
    [Theory]
    [InlineData("https://abcdef.supabase.co/storage/v1/s3", "https://abcdef.supabase.co")]
    [InlineData("https://bzrmkfutnbcgbktwdyry.storage.supabase.co/storage/v1/s3", "https://bzrmkfutnbcgbktwdyry.supabase.co")]
    [InlineData("https://bzrmkfutnbcgbktwdyry.storage.supabase.co", "https://bzrmkfutnbcgbktwdyry.supabase.co")]
    [InlineData("https://abcdef.supabase.co/storage/v1", "https://abcdef.supabase.co")]
    [InlineData("https://abcdef.supabase.co/", "https://abcdef.supabase.co")]
    [InlineData("https://abcdef.supabase.co", "https://abcdef.supabase.co")]
    [InlineData("https://abcdef.supabase.co/storage/v1/s3/", "https://abcdef.supabase.co")]
    [InlineData("http://localhost:54321/storage/v1/s3", "http://localhost:54321")]
    public void SanitizeBaseUrl_StripsS3AndStorageSuffixes(string input, string expected)
    {
        var result = SupabaseArtifactStore.SanitizeBaseUrl(input);
        Assert.Equal(expected, result);
    }
}
