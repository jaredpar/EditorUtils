using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EditorUtils.Implementation.Utilities;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Tagging;

namespace EditorUtils.Implementation.Tagging
{
    internal sealed class AsyncTagger<TData, TTag> : ITagger<TTag>, IDisposable
        where TTag : ITag
    {
        #region CompleteReason

        private enum CompleteReason
        {
            Finished,
            Cancelled,
            Error
        }

        #endregion

        #region BackgroundCacheData

        internal struct BackgroundCacheData
        {
            internal readonly SnapshotSpan Span;
            internal readonly ReadOnlyCollection<ITagSpan<TTag>> TagList;

            internal ITextSnapshot Snapshot
            {
                get { return Span.Snapshot; }
            }

            internal BackgroundCacheData(SnapshotSpan span, ReadOnlyCollection<ITagSpan<TTag>> tagList)
            {
                Span = span;
                TagList = tagList;
            }

            /// <summary>
            /// Create a TrackingCacheData instance from this BackgroundCacheData
            /// </summary>
            /// <param name="snapshot"></param>
            /// <returns></returns>
            internal TrackingCacheData CreateTrackingCacheData()
            {
                // Create the list.  Initiate an ITrackingSpan for every SnapshotSpan present
                var trackingList = TagList.Select(
                    tagSpan =>
                    {
                        var snapshot = tagSpan.Span.Snapshot;
                        var trackingSpan = snapshot.CreateTrackingSpan(tagSpan.Span, SpanTrackingMode.EdgeExclusive);
                        return Tuple.Create(trackingSpan, tagSpan.Tag);
                    })
                    .ToReadOnlyCollection();

                return new TrackingCacheData(
                    Span.Snapshot.CreateTrackingSpan(Span, SpanTrackingMode.EdgeInclusive),
                    trackingList);
            }
        }

        #endregion

        #region TrackingCacheData

        internal struct TrackingCacheData
        {
            internal readonly ITrackingSpan TrackingSpan;
            internal readonly ReadOnlyCollection<Tuple<ITrackingSpan, TTag>> TrackingList;

            internal TrackingCacheData(ITrackingSpan trackingSpan, ReadOnlyCollection<Tuple<ITrackingSpan, TTag>> trackingList)
            {
                TrackingSpan = trackingSpan;
                TrackingList = trackingList;
            }

            internal TrackingCacheData Merge(ITextSnapshot snapshot, TrackingCacheData trackingCacheData)
            {
                var left = TrackingSpan.GetSpanSafe(snapshot);
                var right = trackingCacheData.TrackingSpan.GetSpanSafe(snapshot);
                SnapshotSpan span;
                if (left.HasValue && right.HasValue)
                {
                    span = left.Value.CreateOverarching(right.Value);
                }
                else if (left.HasValue)
                {
                    span = left.Value;
                }
                else if (right.HasValue)
                {
                    span = right.Value;
                }
                else
                {
                    span = new SnapshotSpan(snapshot, 0, 0);
                }
                var trackingSpan = snapshot.CreateTrackingSpan(span, SpanTrackingMode.EdgeInclusive);

                var tagList = TrackingList
                    .Concat(trackingCacheData.TrackingList)
                    .Distinct(EqualityUtility.Create<Tuple<ITrackingSpan, TTag>>(
                        (x, y) => x.Item1.GetSpanSafe(snapshot) == y.Item1.GetSpanSafe(snapshot),
                        tuple => tuple.Item1.GetSpanSafe(snapshot).GetHashCode()))
                    .ToReadOnlyCollection();

                return new TrackingCacheData(trackingSpan, tagList);
            }
        }

        #endregion

        #region TagCache

        internal struct TagCache
        {
            internal BackgroundCacheData? BackgroundCacheData;
            internal TrackingCacheData? TrackingCacheData;

            internal bool IsEmpty
            {
                get { return !BackgroundCacheData.HasValue && !TrackingCacheData.HasValue; }
            }

            internal TagCache(BackgroundCacheData? backgroundCacheData, TrackingCacheData? trackingCacheData)
            {
                BackgroundCacheData = backgroundCacheData;
                TrackingCacheData = trackingCacheData;
            }

            internal static TagCache Empty
            {
                get { return new TagCache(null, null); }
            }
        }

        #endregion

        #region TagLookupResult

        internal enum TagLookupResultKind
        {
            None,
            Partial,
            Complete
        }

        internal struct TagLookupResult
        {
            internal readonly TagLookupResultKind Kind;
            internal readonly ReadOnlyCollection<ITagSpan<TTag>> TagList;

            internal bool IsComplete
            {
                get { return Kind == TagLookupResultKind.Complete; }
            }

            internal bool IsPartial
            {
                get { return Kind == TagLookupResultKind.Partial; }
            }

            private TagLookupResult(TagLookupResultKind kind, ReadOnlyCollection<ITagSpan<TTag>> tagList)
            {
                Kind = kind;
                TagList = tagList;
            }

            internal static TagLookupResult Empty
            {
                get { return new TagLookupResult(TagLookupResultKind.None, EmptyTagList); }
            }

            internal static TagLookupResult CreateComplete(ReadOnlyCollection<ITagSpan<TTag>> tagList)
            {
                return new TagLookupResult(TagLookupResultKind.Complete, tagList);
            }

            internal static TagLookupResult CreatePartial(ReadOnlyCollection<ITagSpan<TTag>> tagList)
            {
                return new TagLookupResult(TagLookupResultKind.Partial, tagList);
            }
        }

        #endregion

        #region AsyncBackgroundRequest

        internal struct AsyncBackgroundRequest
        {
            internal readonly ITextSnapshot Snapshot;
            internal readonly CancellationTokenSource CancellationTokenSource;
            internal readonly ThreadedLineRangeStack ThreadedLineRangeStack;
            internal readonly Task Task;

            internal AsyncBackgroundRequest(
                ITextSnapshot snapshot,
                CancellationTokenSource cancellationTokenSource,
                ThreadedLineRangeStack threadedLineRangeStack,
                Task task)
            {
                Snapshot = snapshot;
                CancellationTokenSource = cancellationTokenSource;
                ThreadedLineRangeStack = threadedLineRangeStack;
                Task = task;
            }
        }

        #endregion

        /// This number was chosen virtually at random.  In extremely large files it's legal
        /// to ask for the tags for the entire file (and sadly very often done).  When this 
        /// happens even an async tagger breaks down a bit.  It won't cause the UI to hang but
        /// it will appear the tagger is broken because it's not giving back any data.  So 
        /// we break the non-visible sections into chunks and process the chunks one at a time
        ///
        /// Note: Even though a section is not visible we must still provide tags.  Gutter 
        /// margins and such still need to see tags for non-visible portions of the buffer
        internal const int DefaultChunkCount = 500;

        /// <summary>
        /// Cached empty tag list
        /// </summary>
        private static readonly ReadOnlyCollection<ITagSpan<TTag>> EmptyTagList = new ReadOnlyCollection<ITagSpan<TTag>>(new List<ITagSpan<TTag>>());

        private readonly IAsyncTaggerSource<TData, TTag> _asyncTaggerSource;
        private event EventHandler<SnapshotSpanEventArgs> _tagsChanged;

        /// <summary>
        /// The one and only active AsyncBackgroundRequest instance.  There can be several
        /// in flight at once.  But we will cancel the earlier ones should a new one be 
        /// requested
        /// </summary>
        private AsyncBackgroundRequest? _asyncBackgroundRequest;

        /// <summary>
        /// The current cache of tags we've provided to our consumer
        /// </summary>
        private TagCache _tagCache;

        /// <summary>
        /// The SnapshotSpan for which we've provided information via GetTags
        /// </summary>
        private SnapshotSpan? _cachedRequestSpan = null;

        private int _chunkCount = DefaultChunkCount;

        /// <summary>
        /// The SnapshotSpan for which we have given out tags
        /// </summary>
        internal SnapshotSpan? CachedRequestSpan
        {
            get { return _cachedRequestSpan; }
            set { _cachedRequestSpan = value; }
        }

        /// <summary>
        /// The cache of ITag<'TTag> values
        /// </summary>
        internal TagCache TagCacheData
        {
            get { return _tagCache; }
            set { _tagCache = value; }
        }

        /// <summary>
        /// If there is a background request active this holds the information about it 
        /// </summary>
        internal AsyncBackgroundRequest? AsyncBackgroundRequestData
        {
            get { return _asyncBackgroundRequest; }
            set { _asyncBackgroundRequest = value; }
        }

        internal int ChunkCount
        {
            get { return _chunkCount; }
            set { _chunkCount = value; }
        }

        internal AsyncTagger(IAsyncTaggerSource<TData, TTag> asyncTaggerSource)
        {
            _asyncTaggerSource = asyncTaggerSource;
            _asyncTaggerSource.Changed += OnAsyncTaggerSourceChanged;

            // If there is an ITextView associated with the IAsyncTaggerSource then we want to 
            // listen to LayoutChanges.  If the layout changes while we are getting tags we want
            // to prioritize the visible lines
            if (_asyncTaggerSource.TextViewOptional != null)
            {
                _asyncTaggerSource.TextViewOptional.LayoutChanged += OnLayoutChanged;
            }
        }

        /// <summary>
        /// Given a new tag list determine if the results differ from what we would've been 
        /// returning from our TrackingCacheData over the same SnapshotSpan
        /// </summary>
        internal bool DidTagsChange(SnapshotSpan span, ReadOnlyCollection<ITagSpan<TTag>> tagList)
        {
            if (!_tagCache.TrackingCacheData.HasValue)
            {
                // Nothing in the tracking cache so it changed if there is anything in the new
                // collection
                return tagList.Count > 0;
            }

            var trackingCacheData = _tagCache.TrackingCacheData.Value;
            var trackingTagList = GetTagsFromCache(span, trackingCacheData).TagList;

            if (trackingTagList.Count != tagList.Count)
            {
                return true;
            }

            var trackingSet = trackingTagList
                .Select(tagSpan => tagSpan.Span)
                .ToHashSet();

            return tagList.Any(x => !trackingSet.Contains(x.Span));
        }

        /// <summary>
        /// Get the tags for the specified NormalizedSnapshotSpanCollection.  Use the cache if 
        /// possible and possibly go to the background if necessary
        /// </summary>
        internal IEnumerable<ITagSpan<TTag>> GetTags(NormalizedSnapshotSpanCollection col)
        {
            var span = col.GetOverarchingSpan();
            var lineRange = SnapshotLineRange.CreateForSpan(span);
            EditorUtilsTrace.TraceInfo("AsyncTagger::GetTags {0} - {1}", lineRange.StartLineNumber, lineRange.LastLineNumber);

            AdjustRequestSpan(span);

            IEnumerable<ITagSpan<TTag>> tagList;

            // First try and see if the tagger can provide prompt data.  We want to avoid 
            // creating Task<T> instances if possible.  
            if (!TryGetTagsPrompt(span, out tagList))
            {
                var tagLookupResult = GetTagsFromCache(span);
                switch (tagLookupResult.Kind)
                {
                    case TagLookupResultKind.None:
                        // Nothing was in the cache.  Kick off a background request to get it
                        // and return and empty list for now
                        GetTagsInBackground(span);
                        tagList = EmptyTagList;
                        break;
                    case TagLookupResultKind.Partial:

                        // Tag list was partially avaliable.  Kick off a request to get the
                        // complete data and return what we have for now
                        GetTagsInBackground(span);
                        tagList = tagLookupResult.TagList;
                        break;

                    case TagLookupResultKind.Complete:

                        // No need to go through the background
                        tagList = tagLookupResult.TagList;
                        break;
                    default:
                        Contract.Fail();
                        tagList = EmptyTagList;
                        break;
                }
            }

            // Now filter the set of returned ITagSpan values to those which are part of the 
            // requested NormalizedSnapshotSpanCollection.  The cache lookups don't dig down and 
            // instead return all available tags.  We filter down the collection here to what's 
            // necessary.
            return tagList.Where(tagSpan => tagSpan.Span.IntersectsWith(span));
        }

        private void Dispose()
        {
            RemoveHandlers();
            var disposable = _asyncTaggerSource as IDisposable;
            if (disposable != null)
            {
                disposable.Dispose();
            }
        }

        private void RemoveHandlers()
        {
            _asyncTaggerSource.Changed -= OnAsyncTaggerSourceChanged;
            if (_asyncTaggerSource.TextViewOptional != null)
            {
                _asyncTaggerSource.TextViewOptional.LayoutChanged -= OnLayoutChanged;
            }
        }

        /// <summary>
        /// Try and get the tags promptly from the IAsyncTaggerSource
        /// </summary>
        private bool TryGetTagsPrompt(SnapshotSpan span, out IEnumerable<ITagSpan<TTag>> tagList)
        {
            return _asyncTaggerSource.TryGetTagsPrompt(span, out tagList);
        }

        /// <summary>
        /// Get the tags from our cache
        /// </summary>
        private TagLookupResult GetTagsFromCache(SnapshotSpan span)
        {
            // Update the cache to the given ITextSnapshot
            MaybeUpdateTagCacheToSnapshot(span.Snapshot);

            if (_tagCache.BackgroundCacheData.HasValue)
            {
                var tagLookupResult = GetTagsFromCache(span, _tagCache.BackgroundCacheData.Value);
                if (tagLookupResult.IsComplete)
                {
                    return tagLookupResult;
                }

                // We are in the middle of processing a background request.  We have both the
                // tracking data and the partial background data.  During the transition we should
                // pull first from background and then from tracking 
                if (_tagCache.TrackingCacheData.HasValue)
                {
                    var tagLookupResult2 = GetTagsFromCache(span, _tagCache.TrackingCacheData.Value);
                    return TagLookupResult.CreatePartial(tagLookupResult.TagList.Concat(tagLookupResult2.TagList).ToReadOnlyCollection());
                }

                return tagLookupResult;
            }

            if (_tagCache.TrackingCacheData.HasValue)
            {
                return GetTagsFromCache(span, _tagCache.TrackingCacheData.Value);
            }

            return TagLookupResult.Empty;
        }

        /// <summary>
        /// Get the tags from the BackgroundCacheData
        /// </summary>
        private TagLookupResult GetTagsFromCache(SnapshotSpan span, BackgroundCacheData backgroundCacheData)
        {
            var cachedSpan = backgroundCacheData.Span;
            if (cachedSpan.Contains(span))
            {
                return TagLookupResult.CreateComplete(backgroundCacheData.TagList);
            }

            if (cachedSpan.IntersectsWith(span))
            {
                // The requested span is at least partially within the cached region.  Return 
                // the data that is available and schedule a background request to get the 
                // rest
                return TagLookupResult.CreatePartial(backgroundCacheData.TagList);
            }

            return TagLookupResult.Empty;
        }

        /// <summary>
        /// Get the tags from the edit tracking data which intersect with the requested SnapshotSpan
        /// </summary>
        private TagLookupResult GetTagsFromCache(SnapshotSpan span, TrackingCacheData trackingCacheData)
        {
            // If this SnapshotSpan is coming from a different snapshot which is ahead of 
            // our current one we need to take special steps.  If we simply return nothing
            // and go to the background the tags will flicker on screen.  
            //
            // To work around this we try to map the tags to the requested ITextSnapshot. If
            // it succeeds then we use the mapped values and simultaneously kick off a background
            // request for the correct ones
            var snapshot = span.Snapshot;
            var trackingSpan = trackingCacheData.TrackingSpan.GetSpanSafe(snapshot);
            if (!trackingSpan.HasValue)
            {
                return TagLookupResult.Empty;
            }

            var mappedSpan = trackingSpan.Value;
            if (mappedSpan.IntersectsWith(span))
            {
                // Mapping gave us at least partial information.  Will work for the transition
                // period
                var tagList =
                    trackingCacheData.TrackingList
                    .Select(
                        tuple =>
                        {
                            var itemSpan = tuple.Item1.GetSpanSafe(snapshot);
                            return itemSpan.HasValue
                                ? (ITagSpan<TTag>)new TagSpan<TTag>(itemSpan.Value, tuple.Item2)
                                : null;
                        })
                    .Where(tagSpan => tagSpan != null)
                    .ToReadOnlyCollection();
                return TagLookupResult.CreatePartial(tagList);
            }

            return TagLookupResult.Empty;
        }

        /// <summary>
        /// Get the tags for the specified SnapshotSpan in a background task.  If there are outstanding
        /// requests for SnapshotSpan values then this one will take priority over those 
        /// </summary>
        private void GetTagsInBackground(SnapshotSpan span)
        {
            var synchronizationContext = SynchronizationContext.Current;
            if (null == synchronizationContext)
            {
                return;
            }

            // Our caching and partitioning of data is all done on a line range
            // basis.  Just expand the requested SnapshotSpan to the encompassing
            // SnaphotlineRange
            var lineRange = SnapshotLineRange.CreateForSpan(span);
            span = lineRange.ExtentIncludingLineBreak;

            // If there is an existing background request then just enqueue our data
            // onto this request if it's on the same ITextSnapshot.  If they are on
            // different ITextSnapshot values then cancel the request and start a 
            // new one 
            if (_asyncBackgroundRequest.HasValue)
            {
                var asyncBackgroundRequest = _asyncBackgroundRequest.Value;
                if (asyncBackgroundRequest.Snapshot == span.Snapshot)
                {
                    EditorUtilsTrace.TraceInfo("AsyncTagger Background Existing {0} - {1}", lineRange.StartLineNumber, lineRange.LastLineNumber);
                    asyncBackgroundRequest.ThreadedLineRangeStack.Push(lineRange);
                    return;
                }

                CancelAsyncBackgroundRequest();
            }

            Contract.Assert(!_asyncBackgroundRequest.HasValue);
            EditorUtilsTrace.TraceInfo("AsyncTagger Background New {0} - {1}", lineRange.StartLineNumber, lineRange.LastLineNumber);

            // Create the data which is needed by the background request
            var data = _asyncTaggerSource.GetDataForSpan(span);
            var cancellationTokenSource = new CancellationTokenSource();
            var cancellationToken = cancellationTokenSource.Token;
            var threadedLineRangeStack = new ThreadedLineRangeStack();
            threadedLineRangeStack.Push(lineRange);

            // If there is an ITextView then make sure it is requested as well.  If the source provides an 
            // ITextView then it is always prioritized on requests for a new snapshot
            if (_asyncTaggerSource.TextViewOptional != null)
            {
                var visibleLineRange = _asyncTaggerSource.TextViewOptional.GetVisibleSnapshotLineRange();
                if (visibleLineRange.HasValue)
                {
                    threadedLineRangeStack.Push(visibleLineRange.Value);
                }
            }

            // Function which finally gets the tags.  This is run on a background thread and can
            // throw as the implementor is encouraged to use CancellationToken::ThrowIfCancelled
            var localAsyncTaggerSource = _asyncTaggerSource;
            var localChunkCount = _chunkCount;
            Action getTags = () => GetTagsInBackgroundCore(
                localAsyncTaggerSource,
                data,
                localChunkCount,
                threadedLineRangeStack,
                cancellationToken,
                (completeReason) => synchronizationContext.Post(_ => OnGetTagsInBackgroundComplete(completeReason, threadedLineRangeStack, cancellationTokenSource), null),
                (processedLineRange, tagList) => synchronizationContext.Post(_ => OnGetTagsInBackgroundProgress(cancellationTokenSource, processedLineRange, tagList), null));

            // Create the Task which will handle the actual gathering of data.  If there is a delay
            // specified use it
            Task startTask;
            Task endTask;
            if (_asyncTaggerSource.Delay.HasValue)
            {
                var delay = _asyncTaggerSource.Delay.Value;
                startTask = new Task(() => Thread.Sleep(delay), cancellationToken);
                endTask = startTask.ContinueWith(_ => getTags(), cancellationToken);
            }
            else
            {
                startTask = new Task(getTags, cancellationToken);
                endTask = startTask;
            }

            _asyncBackgroundRequest = new AsyncBackgroundRequest(
                span.Snapshot,
                cancellationTokenSource,
                threadedLineRangeStack,
                endTask);

            startTask.Start();
        }

        [UsedInBackgroundThread]
        private static void GetTagsInBackgroundCore(
            IAsyncTaggerSource<TData, TTag> asyncTaggerSource,
            TData data,
            int chunkCount, 
            ThreadedLineRangeStack threadedLineRangeStack,
            CancellationToken cancellationToken,
            Action<CompleteReason> onComplete,
            Action<SnapshotLineRange, ReadOnlyCollection<ITagSpan<TTag>>> onProgress)
        {
            CompleteReason completeReason;
            try
            {
                // Keep track of the LineRange values which we've already provided tags for.  Don't 
                // duplicate the work
                var visited = new NormalizedLineRangeCollection();
                var toProcess = new Queue<SnapshotLineRange>();

                // *** This value can be wrong *** 
                // This is the version number we expect the ThreadedLineRangeStack to have.  It's used
                // as a hueristic to determine if we should prioritize a value off of the stack or our
                // local stack.  If it's wrong it means we prioritize the wrong value.  Not a bug it
                // just changes the order in which values will appear
                var versionNumber = threadedLineRangeStack.CurrentVersion;

                // Take one value off of the threadedLineRangeStack value.  If the value is bigger than
                // our chunking increment then we will add the value in chunks to the toProcess queue
                Action popOne =
                    () =>
                    {
                        var value = threadedLineRangeStack.Pop();
                        if (!value.HasValue)
                        {
                            return;
                        }

                        versionNumber++;
                        var lineRange = value.Value;
                        if (lineRange.Count <= chunkCount)
                        {
                            toProcess.Enqueue(lineRange);
                            return;
                        }

                        var snapshot = lineRange.Snapshot;
                        var startLineNumber = lineRange.StartLineNumber;
                        while (startLineNumber <= lineRange.LastLineNumber)
                        {
                            var startLine = snapshot.GetLineFromLineNumber(startLineNumber);
                            var localRange = SnapshotLineRange.CreateForLineAndMaxCount(startLine, chunkCount);
                            toProcess.Enqueue(localRange);
                            startLineNumber += chunkCount;
                        }
                    };

                // Get the tags for the specified SnapshotLineRange and return the results.  No chunking is done here,
                // the data is just directly processed
                Action<SnapshotLineRange> getTags =
                    tagLineRange =>
                    {
                        var unvisited = visited.GetUnvisited(tagLineRange.LineRange);
                        if (unvisited.HasValue)
                        {
                            var tagList = EmptyTagList;
                            try
                            {
                                tagLineRange = SnapshotLineRange.CreateForLineNumberRange(tagLineRange.Snapshot, unvisited.Value.StartLineNumber, unvisited.Value.LastLineNumber).Value;
                                tagList = asyncTaggerSource.GetTagsInBackground(data, tagLineRange.ExtentIncludingLineBreak, cancellationToken);
                            }
                            catch
                            {
                                // Ignore
                            }

                            visited.Add(tagLineRange.LineRange);
                            onProgress(tagLineRange, tagList);
                        }
                    };

                do
                {
                    versionNumber = threadedLineRangeStack.CurrentVersion;
                    popOne();

                    // We've drained both of the sources of input hence we are done
                    if (0 == toProcess.Count)
                    {
                        break;
                    }

                    while (0 != toProcess.Count)
                    {
                        // If at any point the threadLineRangeStack value changes we consider the new values to have 
                        // priority over the old ones
                        if (versionNumber != threadedLineRangeStack.CurrentVersion)
                        {
                            break;
                        }

                        cancellationToken.ThrowIfCancellationRequested();

                        var lineRange = toProcess.Dequeue();
                        getTags(lineRange);
                    }

                } while (!cancellationToken.IsCancellationRequested);

                completeReason = cancellationToken.IsCancellationRequested
                    ? CompleteReason.Cancelled
                    : CompleteReason.Finished;
            }
            catch (OperationCanceledException)
            {
                // Don't report cancellation exceptions.  These are thrown during cancellation for fast
                // break from the operation.  It's really a control flow mechanism
                completeReason = CompleteReason.Cancelled;
            }
            catch (Exception e)
            {
                // Handle cancellation exceptions and everything else.  Don't want an errant 
                // exception thrown by the IAsyncTaggerSource to crash the process
                EditorUtilsTrace.TraceInfo("AsyncTagger Exception in background processing {0}", e);
                completeReason = CompleteReason.Error;
            }
            
            onComplete(completeReason);
        }

        /// <summary>
        /// Cancel the pending AsyncBackgoundRequest if one is currently running
        /// </summary>
        private void CancelAsyncBackgroundRequest()
        {
            if (_asyncBackgroundRequest.HasValue)
            {
                // Use a try / with to protect the Cancel from throwing and taking down the process
                try
                {
                    var asyncBackgroundRequest = _asyncBackgroundRequest.Value;
                    if (!asyncBackgroundRequest.CancellationTokenSource.IsCancellationRequested)
                    {
                        asyncBackgroundRequest.CancellationTokenSource.Cancel();
                    }
                }
                catch (Exception)
                {

                }

                _asyncBackgroundRequest = null;
            }
        }

        private void AdjustRequestSpan(SnapshotSpan requestSpan)
        {
            _cachedRequestSpan = TaggerUtil.AdjustRequestedSpan(_cachedRequestSpan, requestSpan);
        }

        /// <summary>
        /// Potentially update the TagCache to the given ITextSnapshot
        /// </summary>
        private void MaybeUpdateTagCacheToSnapshot(ITextSnapshot snapshot)
        {
            if (!_tagCache.BackgroundCacheData.HasValue ||
                _tagCache.BackgroundCacheData.Value.Snapshot == snapshot)
            {
                // No background cache or it's on the current ITextSnapshot.  Nothing to do
                return;
            }

            var backgroundCacheData = _tagCache.BackgroundCacheData.Value;
            var trackingCacheData = backgroundCacheData.CreateTrackingCacheData();
            if (_tagCache.TrackingCacheData.HasValue)
            {
                trackingCacheData = trackingCacheData.Merge(snapshot, _tagCache.TrackingCacheData.Value);
            }

            _tagCache = new TagCache(null, trackingCacheData);
        }

        private void RaiseTagsChanged(SnapshotSpan span)
        {
            var lineRange = SnapshotLineRange.CreateForSpan(span);
            EditorUtilsTrace.TraceInfo("AsyncTagger::RaiseTagsChanged {0} - {1}", lineRange.StartLineNumber, lineRange.LastLineNumber);

            if (_tagsChanged != null)
            {
                _tagsChanged(this, new SnapshotSpanEventArgs(span));
            }
        }

        /// <summary>
        /// Called when the IAsyncTaggerSource raises a Changed event.  Clear out the 
        /// cache, pass on the event to the ITagger and wait for the next request
        /// </summary>
        private void OnAsyncTaggerSourceChanged(object sender, EventArgs e)
        {
            // Clear out the cache.  It's no longer valid.
            _tagCache = TagCache.Empty;
            CancelAsyncBackgroundRequest();

            // Now if we've previously had a SnapshotSpan requested via GetTags go ahead
            // and tell the consumers that it's changed.  Use the entire cached request
            // span here.  We're pessimistic when we have a Changed call because we have
            // no information on what could've changed
            if (_cachedRequestSpan.HasValue)
            {
                RaiseTagsChanged(_cachedRequestSpan.Value);
            }
        }

        /// <summary>
        /// If the Layout changes while we are in the middle of getting tags we want to 
        /// prioritize the new set of visible lines.
        /// </summary>
        private void OnLayoutChanged(object sender, EventArgs e)
        {
            if (!_asyncBackgroundRequest.HasValue || _asyncTaggerSource.TextViewOptional == null)
            {
                return;
            }

            var visibleLineRange = _asyncTaggerSource.TextViewOptional.GetVisibleSnapshotLineRange();
            var asyncBackgroundRequest = _asyncBackgroundRequest.Value;
            if (visibleLineRange.HasValue && visibleLineRange.Value.Snapshot == asyncBackgroundRequest.Snapshot)
            {
                GetTagsInBackground(visibleLineRange.Value.Extent);
            }
        }

        /// <summary>
        /// Is the async operation with the specified CancellationTokenSource the active 
        /// background request
        /// </summary>
        private bool IsActiveBackgroundRequest(CancellationTokenSource cancellationTokenSource)
        {
            return _asyncBackgroundRequest.HasValue && _asyncBackgroundRequest.Value.CancellationTokenSource == cancellationTokenSource;
        }

        /// <summary>
        /// Called on the main thread when the request for tags has processed at least a small 
        /// section of the file.  This funtion may be called many times for a single background 
        /// request
        ///
        /// Called on the main thread
        /// </summary>
        private void OnGetTagsInBackgroundProgress(CancellationTokenSource cancellationTokenSource, SnapshotLineRange lineRange, ReadOnlyCollection<ITagSpan<TTag>> tagList)
        {
            if (!IsActiveBackgroundRequest(cancellationTokenSource))
            {
                return;
            }

            var span = lineRange.ExtentIncludingLineBreak;
            var newData = new BackgroundCacheData(span, tagList);

            // Merge the existing background data if it's present
            if (_tagCache.BackgroundCacheData.HasValue && _tagCache.BackgroundCacheData.Value.Snapshot == lineRange.Snapshot)
            {
                var backgroundCacheData = _tagCache.BackgroundCacheData.Value;
                newData = new BackgroundCacheData(
                    newData.Span.CreateOverarching(backgroundCacheData.Span),
                    newData.TagList.Concat(backgroundCacheData.TagList).ToReadOnlyCollection());
            }

            _tagCache = new TagCache(newData, _tagCache.TrackingCacheData);

            // Determine if the tags changed on the given Span.  In an edit it's very possible and likely
            // that the ITagSpan we returned by simply mapping the SnapshotSpan forward was correct.  If 
            // so then for a given SnapshotSpan we've already returned a result which was correct.  Raising
            // TagsChanged again for that SnapshotSpan will cause needless work to ocur (and potentially
            // more layouts
            if (DidTagsChange(span, tagList))
            {
                RaiseTagsChanged(span);
            }
        }

        /// <summary>
        /// Called when the background request is completed
        ///
        /// Called on the main thread
        /// </summary>
        private void OnGetTagsInBackgroundComplete(CompleteReason reason, ThreadedLineRangeStack threadedLineRangeStack, CancellationTokenSource cancellationTokenSource)
        {
            if (!IsActiveBackgroundRequest(cancellationTokenSource))
            {
                return;
            }

            // The request is complete.  Reset the active request information
            CancelAsyncBackgroundRequest();

            // Update the tag cache to indicate we are no longer doing any tracking edits
            _tagCache = new TagCache(_tagCache.BackgroundCacheData, null);

            // There is one race condition we must deal with here.  It is possible to get requests in the following
            // order 
            //
            //  - F GetTags span1
            //  - B Process span1 
            //  - B Complete span1
            //  - F GetTags span2 (adds to existing queue)
            //  - F Get notified that background complete
            //
            // The good news is any data that is missed will still be in threadedLineRangeStack.  So we just need to
            // drain this value and re-request the data 
            //
            // We own the stack at this point so just access it directly
            var stack = threadedLineRangeStack.CurrentStack;
            if (!stack.IsEmpty && reason == CompleteReason.Finished)
            {
                var list = new List<SnapshotSpan>();
                while (!stack.IsEmpty)
                {
                    GetTags(new NormalizedSnapshotSpanCollection(stack.Value.Extent));
                    stack = stack.Pop();
                }
            }
        }

        #region ITagger<TTag>

        IEnumerable<ITagSpan<TTag>> ITagger<TTag>.GetTags(NormalizedSnapshotSpanCollection col)
        {
            return GetTags(col);
        }

        event EventHandler<SnapshotSpanEventArgs> ITagger<TTag>.TagsChanged
        {
            add { _tagsChanged += value; }
            remove { _tagsChanged -= value; }
        }

        #endregion

        #region IDisposable

        void IDisposable.Dispose()
        {
            Dispose();
        }

        #endregion
    }
}
