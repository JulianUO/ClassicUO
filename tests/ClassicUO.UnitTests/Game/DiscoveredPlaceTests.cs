// SPDX-License-Identifier: BSD-2-Clause

using System;
using System.IO;
using ClassicUO.Game.UI.Gumps;
using FluentAssertions;
using Xunit;

namespace ClassicUO.UnitTests.Game
{
    public class DiscoveredPlaceTests
    {
        [Fact]
        public void DiscoveredPlace_TTF_Files_Exist()
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string avatarPath = Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "..", "..", "Avatar.ttf"));
            File.Exists(avatarPath).Should().BeTrue($"Avatar.ttf should exist at {avatarPath}");

            string clfPath = Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "..", "..", "CLF-UoClassic.ttf"));
            File.Exists(clfPath).Should().BeTrue($"CLF-UoClassic.ttf should exist at {clfPath}");
        }
    }
}
