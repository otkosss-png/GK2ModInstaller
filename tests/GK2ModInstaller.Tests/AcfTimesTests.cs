using System.Linq;
using GK2ModInstaller.Core;
using Xunit;

namespace GK2ModInstaller.Tests
{
    public class AcfTimesTests
    {
        private const string Fixture = @"""AppWorkshop""
{
	""appid""		""4358690""
	""WorkshopItemsInstalled""
	{
		""3807023815""
		{
			""size""		""1234""
			""timeupdated""		""1758600000""
		}
		""3807346541""
		{
			""timeupdated""		""1758700000""
			""size""		""999""
		}
	}
	""WorkshopItemDetails""
	{
		""timetouched""		""111""
	}
}";

        [Fact]
        public void Parses_timeupdated_per_item()
        {
            var map = AcfTimes.Parse(Fixture);
            Assert.Equal(2, map.Count);
            Assert.Equal(1758600000L, map["3807023815"]);
            Assert.Equal(1758700000L, map["3807346541"]);
        }

        [Fact]
        public void Garbage_text_gives_empty_map()
        {
            Assert.Empty(AcfTimes.Parse("нет тут ничего"));
            Assert.Empty(AcfTimes.Parse(null));
            Assert.Empty(AcfTimes.Parse(""));
        }
    }
}
