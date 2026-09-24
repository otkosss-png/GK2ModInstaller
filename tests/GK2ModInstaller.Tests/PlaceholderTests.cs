using GK2ModInstaller.Core;
using Xunit;

namespace GK2ModInstaller.Tests
{
    public class PlaceholderTests
    {
        [Fact]
        public void Scaffold_builds() => Assert.Equal("0.0.0", Placeholder.Version);
    }
}
