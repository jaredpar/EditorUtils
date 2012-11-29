using Xunit;
using Microsoft.VisualStudio.Text.Editor;
using System;
using EditorUtils.Implementation.BasicUndo;

namespace EditorUtils.UnitTest
{
    public abstract class BasicUndoHistoryTest
    {
        protected readonly object _context;
        protected readonly IBasicUndoHistory _basicUndoHistory;
        internal readonly BasicUndoHistory _basicUndoHistoryRaw;

        public BasicUndoHistoryTest()
        {
            _context = new object();
            _basicUndoHistoryRaw = new BasicUndoHistory(_context);
            _basicUndoHistory = _basicUndoHistoryRaw;
        }

        public sealed class ClearTest : BasicUndoHistoryTest
        {
            /// <summary>
            /// The IEditorOperations implementation likes to put ITextView instances into 
            /// the ITextUndoHistory implementation in order to implement caret undo / redo
            /// operations.  The Clear method clears the undo stack and also should remove
            /// this value to make memory leak testing sane
            /// </summary>
            [Fact]
            public void RemoveTextView()
            {
                _basicUndoHistory.Properties[typeof(ITextView)] = 42;
                _basicUndoHistory.Clear();
                Assert.False(_basicUndoHistory.Properties.ContainsProperty(typeof(ITextView)));
            }

            [Fact]
            public void ClearTransactions()
            {
                using (var transaction = _basicUndoHistory.CreateTransaction("Test"))
                {
                    transaction.Complete();
                }

                _basicUndoHistory.Clear();
                Assert.Equal(0, _basicUndoHistoryRaw.UndoStack.Count);
                Assert.Equal(0, _basicUndoHistoryRaw.RedoStack.Count);
            }

            /// <summary>
            /// Can't perform a clear in the middle of a transaction
            /// </summary>
            [Fact]
            public void OpenTransaction()
            {
                using (var transaction = _basicUndoHistory.CreateTransaction("Test"))
                {
                    Assert.Throws<InvalidOperationException>(() => _basicUndoHistory.Clear());
                    transaction.Complete();
                }
            }
        }

        public sealed class CreateTransactionTest : BasicUndoHistoryTest
        {
            [Fact]
            public void UpdatesUndoStack()
            {
                var transaction = _basicUndoHistory.CreateTransaction("Test");
                transaction.Complete();
                Assert.Equal(1, _basicUndoHistoryRaw.UndoStack.Count);
                Assert.Same(transaction, _basicUndoHistoryRaw.UndoStack.Peek());
            }

            [Fact]
            public void UpdatesCurrentTransaction()
            {
                using (var transaction = _basicUndoHistory.CreateTransaction("Test"))
                {
                    Assert.Same(transaction, _basicUndoHistory.CurrentTransaction);
                    transaction.Complete();
                }

                Assert.Null(_basicUndoHistory.CurrentTransaction);

            }
        }
    }
}
