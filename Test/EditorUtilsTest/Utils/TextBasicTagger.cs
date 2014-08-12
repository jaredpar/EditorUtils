using Microsoft.VisualStudio.Text.Tagging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.VisualStudio.Text;
using System.Collections.ObjectModel;

namespace EditorUtils.UnitTest.Utils
{
    /// <summary>
    /// Tags all occurences of the specified text in the buffer 
    /// </summary>
    internal sealed class TextBasicTagger<T> : IBasicTaggerSource<T>
        where T : ITag
    {
        private readonly T _tag;
        private string _text;

        internal string Text
        {
            get { return _text; }
            set
            {
                if (!StringComparer.Ordinal.Equals(_text, value))
                {
                    _text = value;
                    RaiseChanged();
                }
            }
        }

        internal TextBasicTagger(T tag)
        {
            _tag = tag;
        }

        internal void RaiseChanged()
        {
            var list = Changed;
            if (list != null)
            {
                list(this, EventArgs.Empty);
            }
        }

        internal bool IsMatch(ITextSnapshot snapshot, int position)
        {
            if (position + _text.Length > snapshot.Length || string.IsNullOrEmpty(_text))
            {
                return false;
            }

            for (int i = 0; i < _text.Length; i++)
            {
                if (snapshot[i + position] != _text[i])
                {
                    return false;
                }
            }

            return true;
        }

        public event EventHandler Changed;

        public ReadOnlyCollection<ITagSpan<T>> GetTags(SnapshotSpan span)
        {
            var list = new List<ITagSpan<T>>();
            var position = span.Start.Position;
            var snapshot = span.Snapshot;

            while (position < span.Length)
            {
                if (IsMatch(snapshot, position))
                {
                    var tagSpan = new SnapshotSpan(snapshot, start: position, length: _text.Length);
                    list.Add(new TagSpan<T>(tagSpan, _tag));
                    position += _text.Length;
                }
                else
                {
                    position++;
                }
            }

            return list.ToReadOnlyCollectionShallow();
        }
    }
}
