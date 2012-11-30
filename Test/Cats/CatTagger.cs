using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading;
using EditorUtils;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Tagging;

namespace Cats
{
    /// <summary>
    /// Tag all occurences of "cat" in the code base
    /// </summary>
    internal sealed class CatTagger : AsyncTaggerSource<string, TextMarkerTag>
    {
        #region BackgroundWorker

        sealed class BackgroundWorker
        {
            private readonly string _word = "cat";
            private readonly CancellationToken _cancellationToken;

            internal BackgroundWorker(CancellationToken cancellationToken)
            {
                _cancellationToken = cancellationToken;
            }

            internal ReadOnlyCollection<ITagSpan<TextMarkerTag>> GetTags(SnapshotSpan span)
            {
                var tags = new List<ITagSpan<TextMarkerTag>>();
                var lineRange = SnapshotLineRange.CreateForSpan(span);
                foreach (var snapshotLine in lineRange.Lines)
                {
                    AddWordsOnLine(tags, snapshotLine);
                    _cancellationToken.ThrowIfCancellationRequested();
                }

                // Cats need naps
                Thread.Sleep(TimeSpan.FromSeconds(1));

                return tags.ToReadOnlyCollectionShallow();
            }

            private void AddWordsOnLine(List<ITagSpan<TextMarkerTag>> tags, ITextSnapshotLine snapshotLine)
            {
                var snapshot = snapshotLine.Snapshot;
                var tag = new TextMarkerTag(Constants.FormatName);

                var i = 0;
                while (i < snapshotLine.Length)
                {
                    var point = snapshot.GetPoint(i + snapshotLine.Start.Position);
                    if (IsWord(point))
                    {
                        var span = new SnapshotSpan(snapshot, snapshotLine.Start.Position + i, _word.Length);
                        tags.Add(new TagSpan<TextMarkerTag>(span, tag));
                        i += _word.Length;
                    }
                    else
                    {
                        i++;
                    }
                }
            }

            private bool IsWord(SnapshotPoint point)
            {
                var snapshot = point.Snapshot;
                int i;
                for (i = 0; i + point.Position < snapshot.Length && i < _word.Length; i++)
                {
                    if (snapshot.GetChar(i + point.Position) != _word[i])
                    {
                        return false;
                    }
                }

                if (i < snapshot.Length && Char.IsLetter(snapshot.GetChar(i + point.Position)))
                {
                    return false;
                }

                return true;
            }
        }

        #endregion

        internal CatTagger(ITextView textView)
            : base(textView)
        {

        }

        #region AsyncTaggerSource

        protected override bool TryGetTagsPrompt(SnapshotSpan span, out IEnumerable<ITagSpan<TextMarkerTag>> tags)
        {
            tags = null;
            return false;
        }

        protected override string GetDataForSpan(SnapshotSpan span)
        {
            return string.Empty;
        }

        protected override ReadOnlyCollection<ITagSpan<TextMarkerTag>> GetTagsInBackground(string data, SnapshotSpan span, CancellationToken cancellationToken)
        {
            var backgroundWorker = new BackgroundWorker(cancellationToken);
            return backgroundWorker.GetTags(span);
        }

        #endregion
    }
}
