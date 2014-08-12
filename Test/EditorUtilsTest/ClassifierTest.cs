using EditorUtils.UnitTest.Utils;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Text.Tagging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Xunit;

namespace EditorUtils.UnitTest
{
    public abstract class ClassifierTest : EditorHostTest
    {
        public sealed class BasicTest : ClassifierTest
        {
            private readonly ITextBuffer _textBuffer;
            private readonly TextBasicTagger<IClassificationTag> _textBasicTagger;
            private readonly IClassifier _classifier;

            public BasicTest()
            {
                _textBuffer = CreateTextBuffer();

                var classificationType = EditorHost.ClassificationTypeRegistryService.GetOrCreateClassificationType("classifier test");
                _textBasicTagger = new TextBasicTagger<IClassificationTag>(new ClassificationTag(classificationType));
                _classifier = EditorUtilsFactory.CreateClassifierRaw(_textBasicTagger);
            }

            [Fact]
            public void SimpleGet()
            {
                _textBasicTagger.Text = "cat";
                _textBuffer.SetText("cat a cat");
                var list = _classifier.GetClassificationSpans(_textBuffer.GetExtent());
                Assert.Equal(2, list.Count);
                Assert.Equal(
                    new [] { new Span(0, 3), new Span(6, 3) },
                    list.Select(x => x.Span.Span));
            }

            [Fact]
            public void ChangeEvent()
            {
                int count = 0;
                _textBasicTagger.Text = "dog";
                _textBasicTagger.Changed += delegate { count++; };
                _textBasicTagger.Text = "bar";
                Assert.Equal(1, count);
                _textBasicTagger.Text = "bar";
                Assert.Equal(1, count);
            }
        }
    }
}
