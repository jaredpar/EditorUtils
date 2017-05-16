using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Xunit;

namespace EditorUtils.UnitTest
{
    public sealed class EditorHostFactoryTest
    {
        [Fact]
        public void GetShortVersionStringAll()
        {
            foreach (var e in Enum.GetValues(typeof(EditorVersion)).Cast<EditorVersion>())
            {
                var value = EditorHostFactory.GetShortVersionString(e);
                Assert.NotNull(value);
            }
        }

        [Fact]
        public void GetVersionNumberAll()
        {
            Assert.Equal(10, EditorHostFactory.GetVersionNumber(EditorVersion.Vs2010));
            Assert.Equal(11, EditorHostFactory.GetVersionNumber(EditorVersion.Vs2012));
            Assert.Equal(12, EditorHostFactory.GetVersionNumber(EditorVersion.Vs2013));
            Assert.Equal(14, EditorHostFactory.GetVersionNumber(EditorVersion.Vs2015));
            Assert.Equal(15, EditorHostFactory.GetVersionNumber(EditorVersion.Vs2017));
        }

        [Fact]
        public void MaxEditorVersionIsMax()
        {
            var max = EditorHostFactory.GetVersionNumber(EditorHostFactory.MaxEditorVersion);
            foreach (var e in Enum.GetValues(typeof(EditorVersion)).Cast<EditorVersion>())
            {
                var number = EditorHostFactory.GetVersionNumber(e);
                Assert.True(number <= max);
            }
        }
    }
}
