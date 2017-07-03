using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading;
using EditorUtils;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Tagging;
using System.Diagnostics;

namespace WordUnderCaret
{
    internal sealed class WordUnderCaretTagger : AsyncTaggerSource<string, TextMarkerTag>
    {
        #region BackgroundWorker

        sealed class BackgroundWorker
        {
            private readonly string _word;
            private readonly CancellationToken _cancellationToken;

            internal BackgroundWorker(string word, CancellationToken cancellationToken)
            {
                _word = word;
                _cancellationToken = cancellationToken;
            }

            internal ReadOnlyCollection<ITagSpan<TextMarkerTag>> GetTags(SnapshotSpan span)
            {
                var lineRange = SnapshotLineRange.CreateForSpan(span);
                Debug.WriteLine("WordUnderCaret Version {0}, Lines {1} - {2}", span.Snapshot.Version.VersionNumber, lineRange.StartLineNumber, lineRange.LastLineNumber);

                var tags = new List<ITagSpan<TextMarkerTag>>();
                foreach (var snapshotLine in lineRange.Lines)
                {
                    AddWordsOnLine(tags, snapshotLine);
                    _cancellationToken.ThrowIfCancellationRequested();
                }

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

        private readonly ITextView _textView;
        private bool _enabled;
        private string _lastWord;

        private bool Enabled
        {
            get { return _enabled; }
            set
            {
                var changed = _enabled != value;
                _enabled = value;
                if (changed)
                {
                    base.RaiseChanged();
                }
            }
        }

        internal WordUnderCaretTagger(ITextView textView)
            : base(textView)
        {
            _textView = textView;
            _enabled = true;
            _textView.Selection.SelectionChanged += OnSelectionChanged;
            _textView.Closed += OnTextViewClosed;
        }

        private void OnSelectionChanged(object sender, EventArgs e)
        {
            string word;
            Enabled = TryGetWordUnderCaret(out word);
            if (Enabled && word != _lastWord)
            {
                RaiseChanged();
            }

            _lastWord = word;
        }

        private void OnTextViewClosed(object sender, EventArgs e)
        {
            _textView.Selection.SelectionChanged -= OnSelectionChanged;
            _textView.Closed -= OnTextViewClosed;
        }

        private bool TryGetWordUnderCaret(out string word)
        {
            word = null;

            var point = _textView.GetCaretPoint();

            // If we are at the end of the snasphot there is no word
            if (point.Position == _textView.TextSnapshot.Length)
            {
                return false;
            }

            var c = point.GetChar();
            if (!Char.IsLetter(c))
            {
                return false;
            }

            var start = point;
            while (start.Position > 0 && Char.IsLetter(start.Subtract(1).GetChar()))
            {
                start = start.Subtract(1);
            }

            var snapshot = point.Snapshot;
            var end = point.Add(1);
            while (end.Position < snapshot.Length && Char.IsLetter(end.GetChar()))
            {
                end = end.Add(1);
            }

            var span = new SnapshotSpan(start, end);
            word = span.GetText();
            return true;
        }

        #region AsyncTaggerSource

        protected override bool TryGetTagsPrompt(SnapshotSpan span, out IEnumerable<ITagSpan<TextMarkerTag>> tags)
        {
            if (!Enabled)
            {
                tags = new TagSpan<TextMarkerTag>[] { };
                return true;
            }

            tags = null;
            return false;
        }

        protected override string GetDataForSnapshot(ITextSnapshot snapshot)
        {
            string word;
            if (!TryGetWordUnderCaret(out word))
            {
                return null;
            }

            return word;
        }

        protected override ReadOnlyCollection<ITagSpan<TextMarkerTag>> GetTagsInBackground(string data, SnapshotSpan span, CancellationToken cancellationToken)
        {
            if (String.IsNullOrEmpty(data))
            {
                return new ReadOnlyCollection<ITagSpan<TextMarkerTag>>(new List<ITagSpan<TextMarkerTag>>());
            }

            var backgroundWorker = new BackgroundWorker(data, cancellationToken);
            return backgroundWorker.GetTags(span);
        }

        #endregion
    }
}
